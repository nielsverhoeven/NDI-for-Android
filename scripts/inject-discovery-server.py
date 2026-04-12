#!/usr/bin/env python3
"""
Inject NDI discovery server into the Android app's Room database.
Runs sqlite3 commands directly via adb shell using the app's run-as access.

Usage:
  python inject-discovery-server.py --host 10.10.0.53 --port 5959
"""

import argparse
import subprocess
import sys
import time


APP_PKG = "com.ndi.app.debug"
DB_PATH = "databases/ndi_database"


def adb(args: list[str], capture: bool = True) -> tuple[int, str]:
    cmd = ["adb", "shell"] + args
    result = subprocess.run(cmd, capture_output=capture, text=True)
    return result.returncode, (result.stdout.strip() if capture else "")


def run_as(sql: str) -> tuple[int, str]:
    """Run a sqlite3-compatible SQL statement via the app's data dir through python sqlite3."""
    # We'll use Python's own sqlite3 module by piping via adb shell python3 if available,
    # otherwise we'll use a workaround via ContentProvider or direct DB manipulation.
    # Since sqlite3 binary isn't on device, we use exec-out to stream + modify locally.
    # This function is a placeholder; actual implementation below uses local Python.
    pass


def push_db_from_python(host: str, port: int) -> None:
    """Create a stub discovery-server db patch using Python sqlite3 and push via adb."""
    import sqlite3
    import os
    import tempfile

    # Pull the current database from the device
    print(f"[*] Pulling database from device ({APP_PKG})...")
    
    with tempfile.NamedTemporaryFile(suffix=".sqlite", delete=False) as tmp:
        tmp_path = tmp.name

    # Stream the database out
    result = subprocess.run(
        ["adb", "exec-out", f"run-as {APP_PKG} cat {DB_PATH}"],
        capture_output=True
    )
    if result.returncode != 0 or len(result.stdout) == 0:
        print("[!] Could not pull database. Checking if app is running...")
        # Try stopping app first
        subprocess.run(["adb", "shell", "am", "force-stop", APP_PKG])
        time.sleep(1)
        result = subprocess.run(
            ["adb", "exec-out", f"run-as {APP_PKG} cat {DB_PATH}"],
            capture_output=True
        )

    if len(result.stdout) < 100:
        print(f"[!] Database pull failed or too small ({len(result.stdout)} bytes). Creating fresh DB.")
        # Create a minimal database from scratch
        create_fresh = True
    else:
        with open(tmp_path, "wb") as f:
            f.write(result.stdout)
        print(f"[+] Pulled {len(result.stdout)} bytes to {tmp_path}")
        create_fresh = False

    # Modify the database
    conn = sqlite3.connect(tmp_path)
    cursor = conn.cursor()

    if create_fresh:
        # Create the discovery_servers table (schema from NdiDatabase.kt)
        cursor.execute("""
            CREATE TABLE IF NOT EXISTS discovery_servers (
                id TEXT NOT NULL PRIMARY KEY,
                hostOrIp TEXT NOT NULL,
                port INTEGER NOT NULL,
                enabled INTEGER NOT NULL,
                orderIndex INTEGER NOT NULL,
                createdAtEpochMillis INTEGER NOT NULL,
                updatedAtEpochMillis INTEGER NOT NULL
            )
        """)
        # Room internal metadata table
        cursor.execute("""
            CREATE TABLE IF NOT EXISTS room_master_table (
                id INTEGER PRIMARY KEY, identity_hash TEXT
            )
        """)

    # Delete existing entries for this host:port to avoid duplicates
    cursor.execute("DELETE FROM discovery_servers WHERE hostOrIp = ? AND port = ?", (host, port))

    # Also remove any old entries to keep the list clean (optional)
    # cursor.execute("DELETE FROM discovery_servers")

    now_ms = int(time.time() * 1000)
    entry_id = f"injected-{host}-{port}"
    
    cursor.execute(
        "INSERT OR REPLACE INTO discovery_servers "
        "(id, hostOrIp, port, enabled, orderIndex, createdAtEpochMillis, updatedAtEpochMillis) "
        "VALUES (?, ?, ?, ?, ?, ?, ?)",
        (entry_id, host, port, 1, 0, now_ms, now_ms)
    )
    
    conn.commit()

    # Verify
    cursor.execute("SELECT * FROM discovery_servers")
    rows = cursor.fetchall()
    print(f"[+] discovery_servers table now has {len(rows)} row(s):")
    for row in rows:
        print(f"    {row}")
    conn.close()

    # Push back to device
    print("[*] Pushing modified database back to device...")

    # Stop app before writing
    subprocess.run(["adb", "shell", "am", "force-stop", APP_PKG], capture_output=True)
    time.sleep(1)

    with open(tmp_path, "rb") as f:
        db_bytes = f.read()

    # Push via stdin using exec-in workaround
    # Use Python to write via adb shell + dd
    result2 = subprocess.run(
        ["adb", "exec-in", f"run-as {APP_PKG} sh -c 'cat > {DB_PATH}'"],
        input=db_bytes,
        capture_output=True
    )
    
    if result2.returncode != 0:
        # Alternative: push to /sdcard first if writable
        print(f"[!] exec-in failed: {result2.stderr.decode()}")
        print("[*] Trying push via adb push + cp...")
        
        # Write to a temp file
        local_tmp = tmp_path + "_push.sqlite"
        import shutil
        shutil.copy(tmp_path, local_tmp)
        
        r = subprocess.run(["adb", "push", local_tmp, "/data/local/tmp/ndi_inject.sqlite"],
                           capture_output=True, text=True)
        print(f"    push result: {r.returncode} {r.stdout} {r.stderr}")
        
        r2 = subprocess.run(["adb", "shell", f"run-as {APP_PKG} cp /data/local/tmp/ndi_inject.sqlite {DB_PATH}"],
                            capture_output=True, text=True)
        print(f"    cp result: {r2.returncode} {r2.stdout} {r2.stderr}")
        os.unlink(local_tmp)
    else:
        print("[+] Database pushed successfully via exec-in")

    os.unlink(tmp_path)

    # Delete WAL/SHM files to avoid conflicts
    for suffix in ["-wal", "-shm"]:
        subprocess.run(["adb", "shell", f"run-as {APP_PKG} rm -f {DB_PATH}{suffix}"],
                      capture_output=True)

    print("[+] Done. WAL/SHM files removed.")

    # Restart app
    print("[*] Relaunching app...")
    time.sleep(0.5)
    subprocess.run(
        ["adb", "shell", "am", "start", "-n", f"{APP_PKG}/com.ndi.app.MainActivity"],
        capture_output=True
    )
    print("[+] App relaunched. Wait ~10s for discovery to run.")


def main() -> None:
    parser = argparse.ArgumentParser(description="Inject NDI discovery server into Android app database")
    parser.add_argument("--host", default="10.10.0.53", help="Discovery server host/IP")
    parser.add_argument("--port", type=int, default=5959, help="Discovery server port")
    args = parser.parse_args()

    print(f"[INFO] Injecting discovery server {args.host}:{args.port} into {APP_PKG}")
    push_db_from_python(args.host, args.port)


if __name__ == "__main__":
    main()
