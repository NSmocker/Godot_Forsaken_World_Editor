# -*- coding: utf-8 -*-
"""Оновлює рядки GFX-00001..GFX-00100 у GFX_CPP_UNITY_CHECKLIST.md за артефактами перевірки.

Читає з .validation/gfx-cpp-unity-check/<ID>/:
  run.json (C++ статус), unity-capture.json (Unity захват), metrics.json/REPORT.md
  (результат порівняння, див. compare_frames_report.py).

Використання:
  python update_checklist.py            # оновити рядки за наявними артефактами
  python update_checklist.py --dry-run  # лише показати, як будуть виглядати рядки
"""
import argparse
import json
from pathlib import Path

REPO = Path(r"B:\Git\Godot_Forsaken_World_Editor")
CHECKLIST = REPO / "GFX_CPP_UNITY_CHECKLIST.md"
OUT_ROOT = REPO / ".validation" / "gfx-cpp-unity-check"
MANIFEST = OUT_ROOT / "manifest.json"


def load_json(path: Path):
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except Exception:
        return None


def row_for(entry: dict) -> tuple[str, bool]:
    gfx_id = entry["id"]
    d = OUT_ROOT / gfx_id
    run = load_json(d / "run.json")
    unity = load_json(d / "unity-capture.json")
    metrics = load_json(d / "metrics.json")
    report_exists = (d / "REPORT.md").exists()

    notes = []
    if run and run.get("path_workaround"):
        w = run["path_workaround"]
        notes.append(f"C++: копія з ASCII-назвою `{w['copy_rel']}` (вміст ідентичний, SHA оригіналу в run.json)")

    cpp_status = run.get("status") if run else None
    if metrics:
        cpp_col = metrics["verdict"]["cpp"]
        unity_col = metrics["verdict"]["unity"]
        verdict = metrics["verdict"]["verdict"]
        iou = metrics["verdict"].get("mean_iou")
        if iou is not None:
            notes.append(f"IoU {iou}, MAE {metrics['verdict'].get('mean_mae')}")
    else:
        if cpp_status == "error":
            cpp_col = "Помилка"
        elif cpp_status == "ok":
            cpp_col = "Відображається" if (d / "effect.json").exists() else "Помилка"
        else:
            cpp_col = "Не перевірено"
        if unity:
            unity_col = "Відображається"
        else:
            summary = load_json(OUT_ROOT / "unity-run-summary.json") or {}
            unity_err = [r for r in summary.get("results", []) if r.get("id") == gfx_id and r.get("error")]
            unity_col = "Помилка" if unity_err else ("Не перевірено" if cpp_status == "ok" else "Не перевірено")
        verdict = "—"

    if report_exists and metrics:
        stan = "Перевірено"
        done = "[x]"
    elif cpp_status == "error" and unity is None:
        stan = "Заблоковано"
        done = "[ ]"
    else:
        stan = "У роботі"
        done = "[ ]"

    if cpp_status == "error":
        notes.append(f"C++: помилка завантаження (returncode {run.get('returncode')})")

    link = f"[{gfx_id}](.validation/gfx-cpp-unity-check/{gfx_id}/REPORT.md)" if report_exists \
        else f".validation/gfx-cpp-unity-check/{gfx_id}/"
    tail = link if not notes else link + "; " + "; ".join(notes)
    row = f"| {gfx_id} | {done} | `{entry['rel']}` | {stan} | {cpp_col} | {unity_col} | {verdict} | {tail} |"
    return row, done == "[x]"


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
    by_id = {e["id"]: e for e in manifest["entries"]}

    lines = CHECKLIST.read_text(encoding="utf-8").splitlines()
    changed = 0
    out_lines = []
    for line in lines:
        stripped = line.strip()
        gfx_id = stripped[2:11] if stripped.startswith("| GFX-") else None
        if gfx_id and gfx_id in by_id:
            row, _ = row_for(by_id[gfx_id])
            if row != line:
                changed += 1
            out_lines.append(row)
            if args.dry_run:
                print(row)
        else:
            out_lines.append(line)

    print(f"рядків змінено: {changed}")
    if not args.dry_run and changed:
        CHECKLIST.write_text("\n".join(out_lines) + "\n", encoding="utf-8")
        print(f"записано: {CHECKLIST}")
    return 0


if __name__ == "__main__":
    import sys
    sys.exit(main())
