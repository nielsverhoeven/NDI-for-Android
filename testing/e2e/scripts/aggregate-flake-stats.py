#!/usr/bin/env python3
"""Aggregates retry-log.ndjson files from one or more directories into a per-test flake-rate
Markdown table, written to stdout (redirect to $GITHUB_STEP_SUMMARY from the workflow step).

A "run" is one (test, attempt=1) record — i.e. one execution of the suite that reached this test.
A run "needed a retry" if any attempt>1 record exists for that test within the same file (each CI
leg writes its own retry-log.ndjson, so grouping by source file approximates grouping by run).
"""
import argparse
import glob
import json
import os
import sys
from collections import defaultdict


def find_ndjson_files(scan_dirs):
    files = []
    for scan_dir in scan_dirs:
        files.extend(glob.glob(os.path.join(scan_dir, "**", "retry-log.ndjson"), recursive=True))
    return sorted(set(files))


def load_records(path):
    records = []
    try:
        with open(path, "r", encoding="utf-8") as fh:
            for line in fh:
                line = line.strip()
                if not line:
                    continue
                try:
                    records.append(json.loads(line))
                except json.JSONDecodeError:
                    continue
    except OSError as exc:
        print(f"warning: could not read {path}: {exc}", file=sys.stderr)
    return records


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--scan-dir", action="append", required=True)
    parser.add_argument("--flake-threshold", type=float, default=0.01)
    args = parser.parse_args()

    files = find_ndjson_files(args.scan_dir)

    runs = defaultdict(int)
    retried = defaultdict(int)
    failed = defaultdict(int)

    for path in files:
        by_test = defaultdict(list)
        for record in load_records(path):
            test = record.get("test")
            if test:
                by_test[test].append(record)

        for test, attempts in by_test.items():
            runs[test] += 1
            if any(a.get("attempt", 1) > 1 for a in attempts):
                retried[test] += 1
            final = max(attempts, key=lambda a: a.get("attempt", 1))
            if not final.get("passed", True):
                failed[test] += 1

    print("### Flake report\n")
    print(f"Scanned {len(files)} retry-log.ndjson file(s) across {', '.join(args.scan_dir)}.\n")

    if not runs:
        print("No retry-log.ndjson files found — nothing to report.")
        return

    print("| Test | Runs | Needed retry | Ultimately failed | Flake rate | Status |")
    print("|---|---|---|---|---|---|")

    any_above = False
    for test in sorted(runs):
        total = runs[test]
        flaky_runs = max(retried[test], failed[test])
        rate = flaky_runs / total if total else 0.0
        status = "OK"
        if rate > args.flake_threshold:
            status = "**ABOVE TARGET — consider quarantine.json**"
            any_above = True
        print(f"| `{test}` | {total} | {retried[test]} | {failed[test]} | {rate:.1%} | {status} |")

    print(f"\nTarget: < {args.flake_threshold:.0%} flake rate per test.")
    if any_above:
        print("\n**One or more tests are above target.** Add an entry to "
              "`tests/MauiApp.UITests/quarantine.json` with an owner and an expiry, "
              "or fix the underlying flake.")


if __name__ == "__main__":
    main()
