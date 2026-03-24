#!/usr/bin/env python3
"""LIS bridge over upstream dlisio.

Modes:
  - read-summary       : read LIS and print canonical JSON summary
  - write-raw-copy     : byte-for-byte copy input LIS to output
  - write-from-summary : write LIS using source path from summary json
"""

from __future__ import annotations

import argparse
import json
import shutil
import sys
from pathlib import Path
from typing import Any, Dict, List


def _import_dlisio():
    try:
        from dlisio import lis  # type: ignore
    except Exception as exc:
        raise RuntimeError(
            "Python пакет 'dlisio' не установлен. "
            "Установите его: pip install dlisio"
        ) from exc
    return lis


def _curve_fields(logical_file, dfsr) -> List[str]:
    from dlisio import lis  # type: ignore

    try:
        arr = lis.curves(logical_file, dfsr)
    except Exception:
        return []

    if hasattr(arr, "dtype") and getattr(arr.dtype, "names", None):
        return list(arr.dtype.names or ())
    return []


def _logical_file_to_dict(logical_file) -> Dict[str, Any]:
    dfsrs = logical_file.data_format_specs()
    return {
        "repr": repr(logical_file),
        "explicits_count": len(logical_file.explicits()),
        "dfsr_count": len(dfsrs),
        "curve_field_sets": [list(_curve_fields(logical_file, d)) for d in dfsrs],
    }


def read_summary(path: str) -> Dict[str, Any]:
    lis = _import_dlisio()
    with lis.load(path) as files:
        return {
            "path": str(Path(path).resolve()),
            "logical_files_count": len(files),
            "logical_files": [_logical_file_to_dict(f) for f in files],
        }


def write_raw_copy(input_path: str, output_path: str) -> Dict[str, Any]:
    src = Path(input_path)
    dst = Path(output_path)
    dst.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(str(src), str(dst))
    return {
        "mode": "write-raw-copy",
        "input": str(src.resolve()),
        "output": str(dst.resolve()),
        "bytes": dst.stat().st_size,
    }


def write_from_summary(summary_path: str, output_path: str) -> Dict[str, Any]:
    data = json.loads(Path(summary_path).read_text(encoding="utf-8"))
    src_path = data.get("path")
    if not src_path:
        raise RuntimeError("В summary отсутствует поле 'path'")
    result = write_raw_copy(src_path, output_path)
    result["mode"] = "write-from-summary"
    result["summary"] = str(Path(summary_path).resolve())
    return result


def main(argv: List[str]) -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--mode",
        choices=["read-summary", "write-raw-copy", "write-from-summary"],
        required=True,
    )
    parser.add_argument("--input", required=True)
    parser.add_argument("--output", default="")
    args = parser.parse_args(argv)

    if args.mode == "read-summary":
        print(json.dumps(read_summary(args.input), ensure_ascii=False))
        return 0

    if args.mode == "write-raw-copy":
        if not args.output:
            raise RuntimeError("Для write-raw-copy требуется --output")
        print(json.dumps(write_raw_copy(args.input, args.output), ensure_ascii=False))
        return 0

    if args.mode == "write-from-summary":
        if not args.output:
            raise RuntimeError("Для write-from-summary требуется --output")
        print(json.dumps(write_from_summary(args.input, args.output), ensure_ascii=False))
        return 0

    print("Unsupported mode", file=sys.stderr)
    return 2


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
