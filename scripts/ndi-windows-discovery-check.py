#!/usr/bin/env python3
"""
NDI Windows Discovery Validator
Loads the NDI SDK DLL and queries for all available sources via the
configured discovery server, then prints the results.

Usage:
  python ndi-windows-discovery-check.py --discovery-server 10.10.0.53
  python ndi-windows-discovery-check.py --discovery-server 10.10.0.53:5959
"""

import argparse
import ctypes
import ctypes.wintypes
import os
import sys
import time

NDI_DLL_PATH = r"C:\Program Files\NDI\NDI 6 SDK\Bin\x64\Processing.NDI.Lib.x64.dll"

# ── NDI struct / type definitions ─────────────────────────────────────────────

class NDIlib_find_create_t(ctypes.Structure):
    _fields_ = [
        ("show_local_sources", ctypes.c_bool),
        ("p_groups",           ctypes.c_char_p),
        ("p_extra_ips",        ctypes.c_char_p),
    ]

class NDIlib_source_t(ctypes.Structure):
    _fields_ = [
        ("p_ndi_name",   ctypes.c_char_p),
        ("p_url_address", ctypes.c_char_p),
    ]

# ── helpers ───────────────────────────────────────────────────────────────────

def load_ndi(dll_path: str) -> ctypes.CDLL:
    if not os.path.exists(dll_path):
        print(f"[ERROR] NDI SDK DLL not found: {dll_path}")
        sys.exit(1)
    return ctypes.CDLL(dll_path)


def setup_signatures(ndi: ctypes.CDLL) -> None:
    """Attach proper argtypes/restypes to every NDI function we call."""
    ndi.NDIlib_initialize.restype  = ctypes.c_bool
    ndi.NDIlib_initialize.argtypes = []

    ndi.NDIlib_destroy.restype  = None
    ndi.NDIlib_destroy.argtypes = []

    ndi.NDIlib_find_create_v2.restype  = ctypes.c_void_p
    ndi.NDIlib_find_create_v2.argtypes = [ctypes.POINTER(NDIlib_find_create_t)]

    ndi.NDIlib_find_destroy.restype  = None
    ndi.NDIlib_find_destroy.argtypes = [ctypes.c_void_p]

    ndi.NDIlib_find_wait_for_sources.restype  = ctypes.c_bool
    ndi.NDIlib_find_wait_for_sources.argtypes = [ctypes.c_void_p, ctypes.c_uint32]

    ndi.NDIlib_find_get_current_sources.restype  = ctypes.POINTER(NDIlib_source_t)
    ndi.NDIlib_find_get_current_sources.argtypes = [ctypes.c_void_p,
                                                     ctypes.POINTER(ctypes.c_uint32)]

    ndi.NDIlib_version.restype  = ctypes.c_char_p
    ndi.NDIlib_version.argtypes = []


# ── main ──────────────────────────────────────────────────────────────────────

def main() -> None:
    parser = argparse.ArgumentParser(description="NDI Windows Source Discovery Validator")
    parser.add_argument("--discovery-server", default="10.10.0.53",
                        help="NDI discovery server host[:port] (default: 10.10.0.53)")
    parser.add_argument("--timeout-sec", type=int, default=15,
                        help="Total discovery timeout in seconds (default: 15)")
    parser.add_argument("--wait-ms", type=int, default=5000,
                        help="Per-tick NDIlib_find_wait_for_sources wait in ms (default: 5000)")
    args = parser.parse_args()

    discovery_server = args.discovery_server
    # Normalise: if no port given and no colon present, append default NDI port.
    if ":" not in discovery_server:
        discovery_server = f"{discovery_server}:5959"

    print(f"[INFO] NDI SDK DLL : {NDI_DLL_PATH}")
    print(f"[INFO] Discovery   : {discovery_server}")
    print(f"[INFO] Timeout     : {args.timeout_sec}s")
    print()

    # Point SDK at discovery server BEFORE initialising the library.
    os.environ["NDI_DISCOVERY_SERVER"] = discovery_server
    print(f"[INFO] NDI_DISCOVERY_SERVER={os.environ['NDI_DISCOVERY_SERVER']}")

    # Load DLL
    ndi = load_ndi(NDI_DLL_PATH)
    setup_signatures(ndi)

    # Initialise
    if not ndi.NDIlib_initialize():
        print("[ERROR] NDIlib_initialize() failed")
        sys.exit(1)

    version = ndi.NDIlib_version()
    print(f"[INFO] NDI version : {version.decode() if version else 'unknown'}")
    print()

    # Create finder — show_local_sources=True, no group filter, no extra IPs.
    create_desc = NDIlib_find_create_t(
        show_local_sources=True,
        p_groups=None,
        p_extra_ips=None,
    )
    finder = ndi.NDIlib_find_create_v2(ctypes.byref(create_desc))
    if not finder:
        print("[ERROR] NDIlib_find_create_v2() failed")
        ndi.NDIlib_destroy()
        sys.exit(1)

    print("[INFO] Finder created — waiting for sources …")
    deadline = time.time() + args.timeout_sec
    all_sources: dict[str, str] = {}   # ndi_name -> url_address

    while time.time() < deadline:
        changed = ndi.NDIlib_find_wait_for_sources(finder, args.wait_ms)
        count = ctypes.c_uint32(0)
        sources_ptr = ndi.NDIlib_find_get_current_sources(finder, ctypes.byref(count))

        current: dict[str, str] = {}
        for i in range(count.value):
            name = sources_ptr[i].p_ndi_name
            url  = sources_ptr[i].p_url_address
            name_str = name.decode() if name else ""
            url_str  = url.decode()  if url  else ""
            current[name_str] = url_str

        if current != all_sources:
            all_sources = current
            print(f"[UPDATE] {len(all_sources)} source(s) now visible:")
            for idx, (name, url) in enumerate(sorted(all_sources.items()), 1):
                print(f"  {idx:>2}. {name}  [{url}]")
        elif not changed:
            print(f"[TICK]   No change — still {len(all_sources)} source(s)")
        
        remaining = deadline - time.time()
        if remaining <= 0:
            break

    # ── Summary ──────────────────────────────────────────────────────────────
    print()
    print("=" * 60)
    print(f"FINAL RESULT — {len(all_sources)} NDI source(s) found via {discovery_server}")
    print("=" * 60)
    if not all_sources:
        print("  (none)")
    else:
        for idx, (name, url) in enumerate(sorted(all_sources.items()), 1):
            print(f"  {idx:>2}. {name}")
            if url:
                print(f"       URL: {url}")

    # Aggregate by sender machine (NDI name format: "MACHINE (stream name)")
    senders: dict[str, list[str]] = {}
    for name in all_sources:
        if " (" in name and name.endswith(")"):
            machine, _, stream = name.partition(" (")
            stream = stream.rstrip(")")
        else:
            machine = name
            stream  = "(default)"
        senders.setdefault(machine, []).append(stream)

    print()
    print(f"Unique senders : {len(senders)}")
    for machine, streams in sorted(senders.items()):
        print(f"  {machine}: {len(streams)} stream(s) — {', '.join(streams)}")

    ndi.NDIlib_find_destroy(finder)
    ndi.NDIlib_destroy()


if __name__ == "__main__":
    main()
