# -*- coding: utf-8 -*-
"""Пакетний запуск Unity покадрового захоплення GFX через Unity pipeline server.

Читає .unity-pipeline-port (порт + evalToken), формує батчі ID, де C++ захоплення
завершилось успішно (run.json status=ok, є effect.json), і викликає
GfxCppUnityBatchCapture.Run із параметром ids (у C# це позиції в manifest).

Використання:
  python run_unity_frame_captures.py [--batch 15] [--from 0] [--to 100] [--force]
"""
import argparse
import json
import os
import sys
import time
import urllib.request
from pathlib import Path

PROJECT_DEFAULT = Path("A:/Github/Unity GFX Validation")
OUT_DEFAULT = Path(r"B:\Git\Godot_Forsaken_World_Editor\.validation\gfx-cpp-unity-check")
MANIFEST_DEFAULT = OUT_DEFAULT / "manifest.json"
SCRIPT_FILE = r"B:/Git/Godot_Forsaken_World_Editor/Tools/Validation/GfxParity/GfxCppUnityBatchCapture.cs"
ENTRY = "GfxCppUnityBatchCapture.Run"


def call_pipeline(port_info: dict, body: dict, http_timeout: float) -> dict:
    url = f"http://127.0.0.1:{port_info['port']}/api/exec"
    data = json.dumps(body).encode("utf-8")
    req = urllib.request.Request(url, data=data, method="POST", headers={
        "Content-Type": "application/json",
        "Authorization": "Bearer " + port_info["evalToken"],
    })
    with urllib.request.urlopen(req, timeout=http_timeout) as resp:
        return json.loads(resp.read().decode("utf-8"))


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", type=Path, default=MANIFEST_DEFAULT)
    parser.add_argument("--out-root", type=Path, default=OUT_DEFAULT)
    parser.add_argument("--project-path", type=Path,
                        default=Path(os.environ.get("UNITY_PROJECT_PATH") or PROJECT_DEFAULT))
    parser.add_argument("--batch", type=int, default=15)
    parser.add_argument("--from", dest="first", type=int, default=0)
    parser.add_argument("--to", dest="last", type=int, default=None)
    parser.add_argument("--timeout-ms", type=int, default=1_800_000, help="таймаут команди pipeline, мс")
    parser.add_argument("--force", action="store_true", help="переписати наявні unity-capture.json")
    args = parser.parse_args()

    descriptor = args.project_path / "Library" / "Pipeline" / ".unity-pipeline-port"
    port_info = json.loads(descriptor.read_text(encoding="utf-8"))
    manifest = json.loads(args.manifest.read_text(encoding="utf-8"))
    out_root = args.out_root

    # Лише ID з успішним C++ захопленням.
    ready = []
    for position, entry in enumerate(manifest["entries"]):
        gfx_id = entry["id"]
        run_path = out_root / gfx_id / "run.json"
        if not run_path.exists():
            continue
        try:
            run = json.loads(run_path.read_text(encoding="utf-8"))
        except Exception:
            continue
        if run.get("status") != "ok":
            continue
        if not (out_root / gfx_id / "effect.json").exists():
            continue
        ready.append((position, gfx_id))

    last = args.last if args.last is not None else len(manifest["entries"])
    ready = [(p, i) for p, i in ready if args.first <= p < last]
    print(f"Unity готовий: {port_info['unityVersion']} порт {port_info['port']} · {args.project_path}", flush=True)
    print(f"ID з C++ артефактами у діапазоні [{args.first};{last}): {len(ready)}", flush=True)

    all_results = []
    for start in range(0, len(ready), args.batch):
        chunk = ready[start:start + args.batch]
        ids = [i for _, i in chunk]
        body = {
            "command": "script run",
            "parameters": {
                "file": SCRIPT_FILE,
                "entry": ENTRY,
                "args": [json.dumps({
                    "manifest": str(args.manifest).replace("\\", "/"),
                    "outRoot": str(out_root).replace("\\", "/"),
                    "ids": ids,
                    "skip_existing": not args.force,
                }, ensure_ascii=False)],
            },
            "timeout": args.timeout_ms,
        }
        print(f"Батч {start // args.batch + 1}: {ids[0]}..{ids[-1]} ({len(ids)} ID)", flush=True)
        started = time.time()
        try:
            response = call_pipeline(port_info, body, http_timeout=args.timeout_ms / 1000.0 + 60.0)
        except Exception as ex:
            print(f"  помилка запиту: {ex}", flush=True)
            all_results.append({"batch": ids, "error": str(ex)})
            continue
        elapsed = round(time.time() - started, 1)
        if response.get("success"):
            result = response.get("result", {})
            inner = result.get("result")
            records = inner.get("results") if isinstance(inner, dict) else None
            if not isinstance(records, list) or len(records) != len(ids):
                error = result.get("error") or "Unity returned an incomplete capture result"
                details = result.get("diagnostics") or result.get("errorDetails")
                print(f"  помилка захоплення: {error} / {details}", flush=True)
                all_results.append({"batch": ids, "error": error, "details": details})
                continue
            print(f"  ok за {elapsed}s execute={result.get('executeMs')}ms → {json.dumps(inner, ensure_ascii=False)[:300]}", flush=True)
            all_results.extend(records)
        else:
            print(f"  помилка команди: {response.get('error')} / {response.get('errorDetails')}", flush=True)
            all_results.append({"batch": ids, "error": response.get("error"), "details": response.get("errorDetails")})

    counts = {"ok": 0, "skipped": 0, "failed": 0}
    for r in all_results:
        if "error" in r and "id" not in r:
            counts["failed"] += len(r.get("batch", []))
        elif r.get("skipped"):
            counts["skipped"] += 1
        elif r.get("error"):
            counts["failed"] += 1
        else:
            counts["ok"] += 1
    summary = {"counts": counts, "results": all_results}
    (out_root / "unity-run-summary.json").write_text(
        json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(counts, ensure_ascii=False), flush=True)
    return 0 if counts["failed"] == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
