#!/usr/bin/env python3
"""Downloads the emulator-diagnostics-* artifacts (which carry retry-log.ndjson) from the last
N completed runs of a workflow, via `gh`. Used to compute a rolling flake rate across nights,
not just the current run.
"""
import argparse
import json
import os
import subprocess
import sys


def gh_json(args):
    result = subprocess.run(["gh"] + args, capture_output=True, text=True, check=True)
    return json.loads(result.stdout)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo", required=True)
    parser.add_argument("--workflow-file", required=True)
    parser.add_argument("--pattern", required=True)
    parser.add_argument("--max-runs", type=int, default=10)
    parser.add_argument("--out-dir", required=True)
    args = parser.parse_args()

    os.makedirs(args.out_dir, exist_ok=True)

    runs = gh_json([
        "run", "list",
        "--repo", args.repo,
        "--workflow", args.workflow_file,
        "--status", "completed",
        "--limit", str(args.max_runs),
        "--json", "databaseId",
    ])

    for run in runs:
        run_id = run["databaseId"]
        dest = os.path.join(args.out_dir, str(run_id))
        os.makedirs(dest, exist_ok=True)
        try:
            subprocess.run(
                ["gh", "run", "download", str(run_id),
                 "--repo", args.repo,
                 "--pattern", args.pattern,
                 "--dir", dest],
                check=True, capture_output=True, text=True,
            )
        except subprocess.CalledProcessError as exc:
            print(f"note: run {run_id} had no matching artifact: {exc.stderr.strip()}", file=sys.stderr)


if __name__ == "__main__":
    main()
