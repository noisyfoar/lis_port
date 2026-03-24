#!/usr/bin/env python3
"""Integration tests over LIS fixtures copied from upstream dlisio."""

from __future__ import annotations

import importlib.util
import json
import tempfile
from pathlib import Path
from typing import Callable, List
def _known_broken_or_error_expected() -> List[str]:
    # Fixtures intentionally containing structural problems and expected
    # to raise in strict mode (default behavior).
    return [
        "truncated_15.lis",
        "wrong_06.lis",
    ]



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


def _fixture_paths(repo_root: Path) -> List[Path]:
    fixture_root = repo_root / "tests" / "fixtures" / "lis"
    candidates = sorted(
        list(fixture_root.glob("*.LIS"))
        + list(fixture_root.glob("layouts/*.lis"))
        + list(fixture_root.glob("records/*.lis"))
    )
    if not candidates:
        raise RuntimeError("Не найдены LIS фикстуры в tests/fixtures/lis")
    return candidates


def _read_summary(bridge, path: Path) -> dict:
    return bridge.read_summary(str(path))


def test_read_summary_on_all_fixtures() -> None:
    bridge = _load_bridge()
    repo_root = Path(__file__).resolve().parents[2]
    skip_names = set(_known_broken_or_error_expected())
    for fixture in _fixture_paths(repo_root):
        if fixture.name in skip_names:
            continue
        summary = _read_summary(bridge, fixture)
        if "logical_files_count" not in summary:
            raise AssertionError(f"В summary нет logical_files_count: {fixture}")


def test_raw_copy_roundtrip_on_stable_subset() -> None:
    bridge = _load_bridge()
    repo_root = Path(__file__).resolve().parents[2]
    stable = [
        repo_root / "tests" / "fixtures" / "lis" / "MUD_LOG_1.LIS",
        repo_root / "tests" / "fixtures" / "lis" / "layouts" / "layout_00.lis",
        repo_root / "tests" / "fixtures" / "lis" / "records" / "inforec_01.lis",
    ]
    for fixture in stable:
        original = _read_summary(bridge, fixture)
        with tempfile.TemporaryDirectory(prefix="lis_port_fixture_") as td:
            copied = Path(td) / "copied.lis"
            bridge.write_raw_copy(str(fixture), str(copied))
            reread = _read_summary(bridge, copied)
        if original.get("logical_files_count") != reread.get("logical_files_count"):
            raise AssertionError(f"Roundtrip mismatch for {fixture}")


def test_write_from_summary_on_stable_subset() -> None:
    bridge = _load_bridge()
    repo_root = Path(__file__).resolve().parents[2]
    stable = repo_root / "tests" / "fixtures" / "lis" / "layouts" / "layout_01.lis"
    summary = _read_summary(bridge, stable)
    with tempfile.TemporaryDirectory(prefix="lis_port_fixture_") as td:
        summary_path = Path(td) / "summary.json"
        summary_path.write_text(json.dumps(summary), encoding="utf-8")
        out = Path(td) / "from_summary.lis"
        bridge.write_from_summary(str(summary_path), str(out))
        reread = _read_summary(bridge, out)
    if summary.get("logical_files_count") != reread.get("logical_files_count"):
        raise AssertionError("write_from_summary logical_files_count mismatch")


def main() -> int:
    tests = [
        ("read_summary на всех фикстурах", test_read_summary_on_all_fixtures),
        ("raw-copy roundtrip на стабильном поднаборе", test_raw_copy_roundtrip_on_stable_subset),
        ("write_from_summary на стабильном поднаборе", test_write_from_summary_on_stable_subset),
    ]
    ok = True
    for name, fn in tests:
        ok = _run_test(name, fn) and ok
    return 0 if ok else 1


if __name__ == "__main__":
    raise SystemExit(main())
