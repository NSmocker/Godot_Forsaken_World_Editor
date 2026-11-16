# -*- coding: utf-8 -*-
"""Порівняння покадрових захоплень C++ ↔ Unity для кожного ID + звіт REPORT.md.

Вхід: .validation/gfx-cpp-unity-check/GFX-XXXXX/ (frame-XX.png, unity-frame-XX.png,
effect.json, unity-capture.json, run.json, camera.json).
Вихід у тій самій папці: pair-XX.png, comparison.png, metrics.json, REPORT.md.
Зведення: comparison.json + overview-page-N.png у корені out-root.

Використання:
  python compare_frames_report.py [--from 0] [--to 100]
"""
import argparse
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

OUT_DEFAULT = Path(r"B:\Git\Godot_Forsaken_World_Editor\.validation\gfx-cpp-unity-check")
VIS_THRESHOLD = 12          # max(R,G,B) > 12 → видимий піксель (перевірений поріг)
MIN_VISIBLE_PIXELS = 50     # менше — вважаємо кадр порожнім
FONT = ImageFont.truetype("C:/Windows/Fonts/arial.ttf", 16)


def frame_metrics(cpp_im: Image.Image, unity_im: Image.Image) -> dict:
    cpp_px = list(cpp_im.getdata())
    unity_px = list(unity_im.getdata())
    cpp_mask = [max(p) > VIS_THRESHOLD for p in cpp_px]
    unity_mask = [max(p) > VIS_THRESHOLD for p in unity_px]
    cpp_count = sum(cpp_mask)
    unity_count = sum(unity_mask)
    intersection = sum(a and b for a, b in zip(cpp_mask, unity_mask))
    union = sum(a or b for a, b in zip(cpp_mask, unity_mask))
    mae = sum(sum(abs(a - b) for a, b in zip(c, u)) for c, u in zip(cpp_px, unity_px)) / (512 * 512 * 3 * 255)

    def bbox(mask):
        xy = [(n % 512, n // 512) for n, m in enumerate(mask) if m]
        if not xy:
            return None
        return [min(x for x, _ in xy), min(y for _, y in xy), max(x for x, _ in xy) + 1, max(y for _, y in xy) + 1]

    return {
        "cpp_pixels": cpp_count,
        "unity_pixels": unity_count,
        "mask_iou": round(intersection / union, 4) if union else None,
        "rgb_mae": round(mae, 5),
        "cpp_bbox": bbox(cpp_mask),
        "unity_bbox": bbox(unity_mask),
    }


def classify_effect(rows: list, unity_meta: dict, frames_ok: bool) -> dict:
    """Статуси стовпців C++/Unity та висновок згідно з протоколом реєстру."""
    cpp_visible = frames_ok and any(r["cpp_pixels"] >= MIN_VISIBLE_PIXELS for r in rows)
    unity_visible = frames_ok and any(r["unity_pixels"] >= MIN_VISIBLE_PIXELS for r in rows)
    stubs = len(unity_meta.get("stubs", []))
    warnings = len(unity_meta.get("warnings", []))

    if not frames_ok:
        return {"cpp": "Помилка", "unity": "Помилка", "verdict": "Недостатньо даних"}

    ious = [r["mask_iou"] for r in rows if r["mask_iou"] is not None and (r["cpp_pixels"] or r["unity_pixels"])]
    mean_iou = round(sum(ious) / len(ious), 4) if ious else None
    maes = [r["rgb_mae"] for r in rows if (r["cpp_pixels"] or r["unity_pixels"])]
    mean_mae = round(sum(maes) / len(maes), 5) if maes else None

    if not cpp_visible and not unity_visible:
        cpp_col, unity_col, verdict = "Порожньо", "Порожньо", "Недостатньо даних"
    elif cpp_visible and not unity_visible:
        cpp_col, unity_col, verdict = "Відображається", "Порожньо", "Є розбіжності"
    elif unity_visible and not cpp_visible:
        cpp_col, unity_col, verdict = "Порожньо", "Відображається", "Є розбіжності"
    else:
        cpp_col = "Відображається"
        unity_col = "Відображається"
        if stubs or warnings:
            unity_col = "Частково"
        if mean_iou is not None and mean_iou >= 0.55 and mean_mae is not None and mean_mae <= 0.08 and not stubs and not warnings:
            verdict = "Збігається в перевірених умовах"
        else:
            verdict = "Є розбіжності"
    return {"cpp": cpp_col, "unity": unity_col, "verdict": verdict,
            "mean_iou": mean_iou, "mean_mae": mean_mae}

def compare_effect(gfx_dir: Path) -> dict:
    run = json.loads((gfx_dir / "run.json").read_text(encoding="utf-8"))
    effect = json.loads((gfx_dir / "effect.json").read_text(encoding="utf-8"))
    unity_meta = json.loads((gfx_dir / "unity-capture.json").read_text(encoding="utf-8"))
    times = effect.get("time_points", [])
    rows = []
    pairs = []
    animation = []
    for i, t in enumerate(times, start=1):
        cpp_path = gfx_dir / f"frame-{i:02d}.png"
        unity_path = gfx_dir / f"unity-frame-{i:02d}.png"
        if not cpp_path.exists() or not unity_path.exists():
            rows.append({"time_ms": t, "missing": True})
            continue
        cpp_im = Image.open(cpp_path).convert("RGB")
        unity_im = Image.open(unity_path).convert("RGB")
        m = frame_metrics(cpp_im, unity_im)
        m["time_ms"] = t
        rows.append(m)

        pair = Image.new("RGB", (1024, 540), "#202020")
        pd = ImageDraw.Draw(pair)
        label = f"C++ | GFX {t} ms"
        pd.text((8, 6), label, font=FONT, fill="white")
        pair.paste(cpp_im, (0, 28))
        label = f"Unity | GFX {t} ms"
        pd.text((520, 6), label, font=FONT, fill="white")
        pair.paste(unity_im, (512, 28))
        pair.save(gfx_dir / f"pair-{i:02d}.png")
        pairs.append(pair)

    frames_ok = rows and not any(r.get("missing") for r in rows) and len(rows) == len(times)
    verdict = classify_effect([r for r in rows if not r.get("missing")], unity_meta, frames_ok)

    # Контактний лист: усі пари, стовпчик на пару.
    if pairs:
        sheet = Image.new("RGB", (1024, len(pairs) * 282), "#202020")
        for j, pair in enumerate(pairs):
            sheet.paste(pair.resize((512, 270), Image.Resampling.LANCZOS), (0, j * 282))
        sheet.save(gfx_dir / "comparison.png")

    cpp_particles = [f.get("particles", -1) for f in effect.get("frames", [])]
    unity_particles = []
    for s in unity_meta.get("samples", []):
        total = sum(e["particles"] for e in s.get("elements", []) if e.get("particles", -1) > 0)
        unity_particles.append(total)

    metrics = {
        "id": run["id"],
        "rel": run["rel"],
        "sha256": run.get("sha256"),
        "frames": len(rows),
        "rows": rows,
        "verdict": verdict,
        "cpp_particles": cpp_particles,
        "unity_particles": unity_particles,
        "unity_stubs": unity_meta.get("stubs", []),
        "unity_warnings": unity_meta.get("warnings", []),
        "texture_missing": [p for p in unity_meta.get("texture_paths", []) if p == "MISSING"],
        "unity_version": unity_meta.get("unity_version"),
        "cpp_exe_sha256": run.get("exe_sha256"),
        "camera": json.loads((gfx_dir / "camera.json").read_text(encoding="utf-8")),
        "time_points": times,
    }
    (gfx_dir / "metrics.json").write_text(json.dumps(metrics, ensure_ascii=False, indent=2), encoding="utf-8")

    # ---- REPORT.md (українською, компактний) ----
    rel_name = run["rel"].split("/")[-1]
    v = verdict
    verdict_line = {
        "Збігається в перевірених умовах": "Перевірено: у знятих умовах кадри збігаються.",
        "Є розбіжності": "Є розбіжності між C++ і Unity у знятих умовах.",
        "Недостатньо даних": "Недостатньо даних: обидві сторони порожні або кадри неповні.",
    }[v["verdict"]]
    lines = [
        f"# Перевірка {rel_name} ({run['id']})",
        "",
        f"Дата: порівняння автоматичне. **{verdict_line}**",
        "",
        f"Вхід: `{run['rel']}` (SHA-256 `{run.get('sha256')}`).",
        "",
        "## Умови зйомки",
        "",
        f"- Роздільність 512×512, чорне тло, очищення render target перед кожним кадром.",
        f"- Камера з C++ AABB-підбору: eye {metrics['camera']['eye']}, target {metrics['camera']['target']}, FOV 65°, near/far {metrics['camera']['near']}/{metrics['camera']['far']}; Unity відтворює її точно.",
        f"- Часові точки (внутрішній час GFX, мс): {times}; PlaySpeed=1 у C++. Тривалість за елементами: {effect.get('total_ms')} мс, endless={effect.get('any_endless')}.",
        f"- C++: GFXAngelica2.2Checker --frames-out (exe SHA-256 `{(run.get('exe_sha256') or '')[:16]}…`), оригінальні бібліотеки Angelica.",
        f"- Unity: {unity_meta.get('unity_version')} (проєкт A:/Github/Unity Native Reader), GfxSceneBuilder.BuildFromFile + GameDataIndex, прев'ю-сцена, керований Camera.main.",
        "",
        "## Покадрові метрики",
        "",
        "| t, мс | C++ px | Unity px | IoU маски | RGB MAE | частинки C++ | частинки Unity |",
        "|---|---|---|---|---|---|---|",
    ]
    for i, r in enumerate(rows):
        if r.get("missing"):
            lines.append(f"| {r['time_ms']} | — | — (кадр відсутній) | — | — | — | — |")
            continue
        lines.append(
            f"| {r['time_ms']} | {r['cpp_pixels']} | {r['unity_pixels']} | "
            f"{r['mask_iou'] if r['mask_iou'] is not None else '—'} | {r['rgb_mae']} | "
            f"{cpp_particles[i - 1] if i - 1 < len(cpp_particles) else '—'} | "
            f"{unity_particles[i - 1] if i - 1 < len(unity_particles) else '—'} |")
    lines += [
        "",
        "## Спостереження",
        "",
    ]
    if unity_meta.get("stubs"):
        lines.append(f"- Unity: заглушки драйверів: {unity_meta['stubs']}.")
    if unity_meta.get("warnings"):
        lines.append(f"- Unity: попередження збірки: {unity_meta['warnings']}.")
    if metrics["texture_missing"]:
        lines.append(f"- Unity: {len(metrics['texture_missing'])} рендерерів без розв'язаної текстури _BaseMap (може бути нормально для безтекстурних елементів; вплив видно через IoU/MAE).")
    if not unity_meta.get("stubs") and not unity_meta.get("warnings") and not metrics["texture_missing"]:
        lines.append("- Unity: без заглушок, попереджень та відсутніх текстур.")
    lines += [
        f"- Середній IoU маски по видимих парах: {v.get('mean_iou')}; середня RGB MAE: {v.get('mean_mae')}.",
        f"- Частинки (C++ ↔ Unity): {cpp_particles} ↔ {unity_particles}.",
        "",
        "## Артефакти",
        "",
        f"- `frame-01..{len(rows):02d}.png` (C++), `unity-frame-01..{len(rows):02d}.png` (Unity) — вихідні кадри.",
        f"- `pair-01..{len(rows):02d}.png` — пари кадрів; `comparison.png` — контактний лист усіх пар.",
        f"- `camera.json`, `effect.json` (C++: плани кадрів, елементи, час), `unity-capture.json` (Unity: камера, стаби, текстури, частинки), `run.json` (версії, команди), `checker.log`, `metrics.json`.",
        f"- Скрипти: `Tools/Validation/GfxParity/GfxCppUnityBatchCapture.cs`, `run_cpp_frame_captures.py`, `run_unity_frame_captures.py`, `compare_frames_report.py`.",
        "",
        "Висновок обмежений цими умовами зйомки (статична камера за AABB, PlaySpeed=1, без прив'язок до батьківських моделей).",
    ]
    (gfx_dir / "REPORT.md").write_text("\n".join(lines) + "\n", encoding="utf-8")
    return metrics


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--out-root", type=Path, default=OUT_DEFAULT)
    parser.add_argument("--manifest", type=Path, default=OUT_DEFAULT / "manifest.json")
    parser.add_argument("--from", dest="first", type=int, default=0)
    parser.add_argument("--to", dest="last", type=int, default=None)
    args = parser.parse_args()

    manifest = json.loads(args.manifest.read_text(encoding="utf-8"))
    entries = manifest["entries"]
    last = args.last if args.last is not None else len(entries)

    all_metrics = []
    for position, entry in enumerate(entries):
        if position < args.first or position >= last:
            continue
        gfx_dir = args.out_root / entry["id"]
        if not (gfx_dir / "run.json").exists() or not (gfx_dir / "unity-capture.json").exists():
            print(f"[{position + 1:3d}] SKIP {entry['id']} (немає cpp/unity артефактів)", flush=True)
            continue
        try:
            metrics = compare_effect(gfx_dir)
            all_metrics.append(metrics)
            print(f"[{position + 1:3d}] {entry['id']}: {metrics['verdict']['verdict']} "
                  f"(IoU {metrics['verdict'].get('mean_iou')})", flush=True)
        except Exception as ex:
            print(f"[{position + 1:3d}] FAIL {entry['id']}: {ex}", flush=True)

    # comparison.json: об'єднуємо з попередніми прогонами (часткові запуски не втрачають дані).
    merged = {}
    existing = args.out_root / "comparison.json"
    if existing.exists():
        try:
            for item in json.loads(existing.read_text(encoding="utf-8")):
                merged[item["id"]] = item
        except Exception:
            pass
    for item in all_metrics:
        merged[item["id"]] = item
    (args.out_root / "comparison.json").write_text(
        json.dumps([merged[k] for k in sorted(merged)], ensure_ascii=False, indent=2), encoding="utf-8")

    # Оглядові листи: по 10 рядків (найкраща видима пара на ефект).
    page, rows_in_page = 0, []
    for position, entry in enumerate(entries):
        if position < args.first or position >= last:
            continue
        gfx_dir = args.out_root / entry["id"]
        metrics_path = gfx_dir / "metrics.json"
        if not metrics_path.exists():
            continue
        m = json.loads(metrics_path.read_text(encoding="utf-8"))
        rows = [r for r in m["rows"] if not r.get("missing") and (r["cpp_pixels"] or r["unity_pixels"])]
        best = max(rows, key=lambda r: r["cpp_pixels"] + r["unity_pixels"], default=None)
        if best is None:
            continue
        i = m["rows"].index(best) + 1
        cpp_im = Image.open(gfx_dir / f"frame-{i:02d}.png").convert("RGB")
        unity_im = Image.open(gfx_dir / f"unity-frame-{i:02d}.png").convert("RGB")
        rows_in_page.append((entry["id"], m["rel"], best["time_ms"], m["verdict"], cpp_im, unity_im))
        if len(rows_in_page) == 10:
            page += 1
            save_overview(args.out_root, page, rows_in_page)
            rows_in_page = []
    if rows_in_page:
        page += 1
        save_overview(args.out_root, page, rows_in_page)

    print(f"Зведено {len(all_metrics)} ефектів → comparison.json", flush=True)
    return 0


def save_overview(out_root: Path, page: int, rows: list) -> None:
    sheet = Image.new("RGB", (1064, len(rows) * 142 + 26), "#202020")
    draw = ImageDraw.Draw(sheet)
    draw.text((8, 4), "Огляд: C++ (лів.) ↔ Unity (прав.), найкраща видима пара", font=FONT, fill="white")
    for j, (gfx_id, rel, t, verdict, cpp_im, unity_im) in enumerate(rows):
        y = j * 142 + 26
        draw.text((8, y + 2), f"{gfx_id} {rel} t={t}ms → {verdict['verdict']} (IoU {verdict.get('mean_iou')})",
                  font=FONT, fill="white")
        sheet.paste(cpp_im.resize((256, 256), Image.Resampling.LANCZOS), (0, y + 20))
        sheet.paste(unity_im.resize((256, 256), Image.Resampling.LANCZOS), (256, y + 20))
        draw.rectangle((256, y + 20, 511, y + 275), outline="#404040")
        draw.text((516, y + 40), f"{gfx_id}\n{rel}\nt={t} мс\n{verdict['verdict']}", font=FONT, fill="white")
    sheet.save(out_root / f"overview-page-{page:02d}.png")


if __name__ == "__main__":
    import sys
    sys.exit(main())
