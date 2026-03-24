#!/usr/bin/env python3
"""Unit-like tests for dlisio_lis_bridge behavior.

These tests do not require real LIS fixtures and are focused on
security/validation behavior.
"""

from __future__ import annotations

import importlib.util
import json
import tempfile
from pathlib import Path
from typing import Callable


def _load_bridge():
    path = Path(__file__).with_name("dlisio_lis_bridge.py")
    spec = importlib.util.spec_from_file_location("dlisio_lis_bridge", path)
    if spec is None or spec.loader is None:
        raise RuntimeError("Не удалось загрузить dlisio_lis_bridge.py")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def _run_test(name: str, fn: Callable[[], None]) -> bool:
    try:
        fn()
        print(f"[PASS] {name}")
        return True
    except Exception as exc:
        print(f"[FAIL] {name}: {exc}")
        return False


def test_write_raw_copy_rejects_same_path() -> None:
    bridge = _load_bridge()
    with tempfile.TemporaryDirectory(prefix="lis_port_pytest_") as td:
        p = Path(td) / "a.lis"
        p.write_bytes(b"\x01\x02")
        try:
            bridge.write_raw_copy(str(p), str(p))
        except RuntimeError:
            return
        raise AssertionError("Ожидалось RuntimeError для одинаковых input/output.")


def test_write_raw_copy_copies_bytes() -> None:
    bridge = _load_bridge()
    with tempfile.TemporaryDirectory(prefix="lis_port_pytest_") as td:
        src = Path(td) / "in.lis"
        dst = Path(td) / "out.lis"
        payload = b"abc\x00xyz"
        src.write_bytes(payload)
        result = bridge.write_raw_copy(str(src), str(dst))
        assert dst.exists(), "Файл назначения не создан"
        assert dst.read_bytes() == payload, "Содержимое после raw-copy не совпало"
        assert result["bytes"] == len(payload), "Некорректный размер bytes"


def test_write_from_summary_uses_summary_path() -> None:
    bridge = _load_bridge()
    with tempfile.TemporaryDirectory(prefix="lis_port_pytest_") as td:
        src = Path(td) / "src.lis"
        dst = Path(td) / "dst.lis"
        summary = Path(td) / "summary.json"
        src.write_bytes(b"hello")
        summary.write_text(json.dumps({"path": str(src)}), encoding="utf-8")
        result = bridge.write_from_summary(str(summary), str(dst))
        assert dst.read_bytes() == b"hello"
        assert result["mode"] == "write-from-summary"


def test_write_from_summary_requires_path_field() -> None:
    bridge = _load_bridge()
    with tempfile.TemporaryDirectory(prefix="lis_port_pytest_") as td:
        summary = Path(td) / "summary.json"
        dst = Path(td) / "dst.lis"
        summary.write_text(json.dumps({"x": 1}), encoding="utf-8")
        try:
            bridge.write_from_summary(str(summary), str(dst))
        except RuntimeError:
            return
        raise AssertionError("Ожидалось RuntimeError при отсутствии поля 'path'.")


def main() -> int:
    tests = [
        ("write_raw_copy отклоняет одинаковые пути", test_write_raw_copy_rejects_same_path),
        ("write_raw_copy копирует байты", test_write_raw_copy_copies_bytes),
        ("write_from_summary использует path из summary", test_write_from_summary_uses_summary_path),
        ("write_from_summary требует поле path", test_write_from_summary_requires_path_field),
    ]
    ok = True
    for name, fn in tests:
        ok = _run_test(name, fn) and ok
    return 0 if ok else 1


if __name__ == "__main__":
    raise SystemExit(main())
