#!/usr/bin/env python3
"""Smoke parity checks for LIS bridge.

This script validates the most important contract:
1) Read LIS summary via bridge script.
2) Write raw copy via bridge script.
3) Read copied LIS and compare logical file count.
"""

from __future__ import annotations

import json
import os
import subprocess
import sys
import tempfile
from typing import Dict, Any, List


def run_bridge(repo_root: str, args: List[str]) -> Dict[str, Any]:
    script = os.path.join(repo_root, "tools", "python_bridge", "dlisio_lis_bridge.py")
    cmd = [sys.executable, script] + args
    proc = subprocess.run(cmd, capture_output=True, text=True)
    if proc.returncode != 0:
        raise RuntimeError(
            "bridge завершился с ошибкой\n"
            f"code={proc.returncode}\n"
            f"stdout=\n{proc.stdout}\n"
            f"stderr=\n{proc.stderr}"
        )
    return json.loads(proc.stdout)


def main(argv: List[str]) -> int:
    if len(argv) < 2:
        print("usage: smoke_parity.py <repo-root> <lis-path>", file=sys.stderr)
        return 2

    repo_root = os.path.abspath(argv[0])
    lis_path = os.path.abspath(argv[1])
    if not os.path.exists(lis_path):
        print(f"LIS файл не найден: {lis_path}", file=sys.stderr)
        return 2

    before = run_bridge(repo_root, ["--mode", "read-summary", "--input", lis_path])

    with tempfile.TemporaryDirectory(prefix="lis_port_smoke_") as temp_dir:
        copied = os.path.join(temp_dir, "copied.lis")
        run_bridge(
            repo_root,
            ["--mode", "write-raw-copy", "--input", lis_path, "--output", copied],
        )
        after = run_bridge(repo_root, ["--mode", "read-summary", "--input", copied])

    if before.get("logical_files_count") != after.get("logical_files_count"):
        print("Smoke parity failure: logical_files_count differs", file=sys.stderr)
        print(f"before={before.get('logical_files_count')}", file=sys.stderr)
        print(f"after={after.get('logical_files_count')}", file=sys.stderr)
        return 1

    print(
        json.dumps(
            {
                "status": "ok",
                "logical_files_count": before.get("logical_files_count"),
                "path": lis_path,
            },
            ensure_ascii=False,
        )
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
