# -*- coding: utf-8 -*-
"""Пакетний запуск C++ покадрового захоплення GFX (GFXAngelica2.2Checker --frames-out).

Для кожного запису manifest.json:
  - запускає exe з --frames-out <outRoot>/<ID>;
  - stdout/stderr → checker-stdout.txt / checker-stderr.txt у папці ID;
  - копіює checker.log (exe перезаписує його щоразу) → checker.log у папці ID;
  - перевіряє артефакти (camera.json, effect.json, frame-XX.png);
  - пише run.json {статус, час, версії}.

Використання:
  python run_cpp_frame_captures.py [--from 0] [--to 100] [--force]
"""
import argparse
import hashlib
import json
import shutil
import subprocess
import sys
import time
from pathlib import Path

EXE_DEFAULT = Path(r"B:\Git\GFXAngelica2.2Checker\bin\x64\Release\GFXAngelica2.2Checker.exe")
ROOT_DEFAULT = Path(r"B:\Git\PW146RUUnpackedAssets")
OUT_DEFAULT = Path(r"B:\Git\Godot_Forsaken_World_Editor\.validation\gfx-cpp-unity-check")
MANIFEST_DEFAULT = OUT_DEFAULT / "manifest.json"


def sha256_file(path: Path) -> str:
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def prepare_gfx_path(entry: dict, gfx_root: Path) -> tuple[str, dict]:
    """Шлях для exe: GBK-недосяжні назви копіюємо поруч з ASCII-назвою.

    Повертає (relative_path, workaround_info). Вміст файлу не змінюється.
    """
    rel = entry["rel"].replace("/", "\\")
    try:
        rel.encode("gbk")
        return rel, {}
    except UnicodeEncodeError:
        full = Path(entry["full"])
        try:
            str(full.parent).encode("gbk")
            copy_rel = f"{full.parent.name}\\__audit_{entry['id'][-5:]}.gfx"
            copy_path = full.parent / f"__audit_{entry['id'][-5:]}.gfx"
        except UnicodeEncodeError:
            # Теж недосяжна й папка — копіюємо в ASCII-підпапку кореня gfx.
            audit_dir = gfx_root / "__audit"
            audit_dir.mkdir(exist_ok=True)
            copy_rel = f"__audit\\__audit_{entry['id'][-5:]}.gfx"
            copy_path = audit_dir / f"__audit_{entry['id'][-5:]}.gfx"
        if not copy_path.exists():
            shutil.copyfile(full, copy_path)
        info = {"original_rel": rel, "copy_rel": copy_rel,
                "copy_sha256": sha256_file(copy_path), "reason": "назва файлу недосяжна в GBK (ANSI)"}
        return copy_rel, info


def capture_one(entry: dict, exe: Path, asset_root: Path, width: int, height: int,
                timeout_s: float, out_root: Path) -> dict:
    gfx_id = entry["id"]
    gfx_dir = out_root / gfx_id
    gfx_dir.mkdir(parents=True, exist_ok=True)
    rel, workaround = prepare_gfx_path(entry, asset_root / "gfx")

    run_path = gfx_dir / "run.json"
    if run_path.exists():
        try:
            previous = json.loads(run_path.read_text(encoding="utf-8"))
            if previous.get("status") == "ok" and not args.force:
                return {"id": gfx_id, "status": "skipped"}
        except Exception:
            pass

    started = time.time()
    cmd = [
        str(exe), "--frames-out", str(gfx_dir),
        "--root", str(asset_root), "--gfx", rel,
        "--width", str(width), "--height", str(height),
    ]
    try:
        proc = subprocess.run(
            cmd, capture_output=True, timeout=timeout_s,
            creationflags=subprocess.CREATE_NO_WINDOW,
        )
        rc = proc.returncode
        stdout = proc.stdout.decode("gbk", errors="replace")
        stderr = proc.stderr.decode("gbk", errors="replace")
    except subprocess.TimeoutExpired as ex:
        rc = -1
        stdout = (ex.stdout or b"").decode("gbk", errors="replace")
        stderr = ((ex.stderr or b"") + "\n[TIMEOUT after %ss]" % timeout_s).decode("gbk", errors="replace")

    (gfx_dir / "checker-stdout.txt").write_text(stdout, encoding="utf-8")
    (gfx_dir / "checker-stderr.txt").write_text(stderr, encoding="utf-8")
    checker_log = exe.parent / "checker.log"
    if checker_log.exists():
        shutil.copyfile(checker_log, gfx_dir / "checker.log")

    frames = sorted(gfx_dir.glob("frame-*.png"))
    times = []
    effect_path = gfx_dir / "effect.json"
    if effect_path.exists():
        try:
            effect = json.loads(effect_path.read_text(encoding="utf-8"))
            times = effect.get("time_points", [])
        except Exception:
            pass

    ok = (
        rc == 0
        and (gfx_dir / "camera.json").exists()
        and effect_path.exists()
        and len(frames) == len(times) and len(times) > 0
    )
    result = {
        "id": gfx_id,
        "rel": entry["rel"],
        "sha256": entry["sha256"],
        "status": "ok" if ok else "error",
        "returncode": rc,
        "frames": len(frames),
        "time_points": times,
        "exe_sha256": sha256_file(exe),
        "exe_path": str(exe),
        "duration_s": round(time.time() - started, 2),
        "cmd": cmd,
    }
    if workaround:
        result["path_workaround"] = workaround
    run_path.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
    return result


def main() -> int:
    global args
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", type=Path, default=MANIFEST_DEFAULT)
    parser.add_argument("--out-root", type=Path, default=OUT_DEFAULT)
    parser.add_argument("--exe", type=Path, default=EXE_DEFAULT)
    parser.add_argument("--asset-root", type=Path, default=ROOT_DEFAULT)
    parser.add_argument("--width", type=int, default=512)
    parser.add_argument("--height", type=int, default=512)
    parser.add_argument("--timeout", type=float, default=600.0)
    parser.add_argument("--from", dest="first", type=int, default=0)
    parser.add_argument("--to", dest="last", type=int, default=None)
    parser.add_argument("--force", action="store_true", help="повторно захопити навіть якщо run.json ok")
    args = parser.parse_args()

    manifest = json.loads(args.manifest.read_text(encoding="utf-8"))
    entries = manifest["entries"]
    last = args.last if args.last is not None else len(entries)
    selection = entries[args.first:last]

    exe_sha = sha256_file(args.exe)
    print(f"exe: {args.exe} sha256={exe_sha}", flush=True)
    print(f"capture {len(selection)} effects: {args.first}..{last - 1}", flush=True)

    counts = {"ok": 0, "error": 0, "skipped": 0}
    errors = []
    for i, entry in enumerate(selection):
        result = capture_one(entry, args.exe, args.asset_root, args.width, args.height,
                             args.timeout, args.out_root)
        counts[result["status"]] = counts.get(result["status"], 0) + 1
        marker = {"ok": "OK  ", "error": "FAIL", "skipped": "SKIP"}[result["status"]]
        print(f"[{args.first + i + 1:3d}/{last}] {marker} {entry['id']} "
              f"({result.get('frames', 0)} frames, {result.get('duration_s', 0)}s)", flush=True)
        if result["status"] == "error":
            tail = result.get("stderr_tail", "")
            if not tail:
                log = args.out_root / entry["id"] / "checker-stderr.txt"
                if log.exists():
                    tail = log.read_text(encoding="utf-8", errors="replace")[-400:]
            errors.append({"id": entry["id"], "returncode": result.get("returncode"), "tail": tail})

    summary = {"exe_sha256": exe_sha, "counts": counts, "errors": errors}
    (args.out_root / "cpp-run-summary.json").write_text(
        json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(counts, ensure_ascii=False), flush=True)
    return 0 if counts["error"] == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
