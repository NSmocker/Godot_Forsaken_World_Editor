"""Prepare an isolated Godot host without modifying the source project."""
import hashlib
import json
import pathlib
import shutil
import sys

root = pathlib.Path(__file__).resolve().parents[3]
host = root / '.validation/gfx-parity/godot-host'
host.mkdir(parents=True, exist_ok=True)
files = [root / 'gfx_importer' / name for name in ('gfx_keypoint_eval.gd', 'gfx_curve_path.gd')]
files.append(pathlib.Path(__file__).with_name('godot_cpu.gd'))
if '--extended' in sys.argv:
    files = list((root / 'gfx_importer').glob('*.gd'))
    files.append(root / 'gfx_importer/gbk_table.bin')
    files += list(pathlib.Path(__file__).parent.glob('godot_*.gd'))
hashes = {}
for source in files:
    target = host / source.name
    shutil.copyfile(source, target)
    hashes[str(source.relative_to(root))] = hashlib.sha256(target.read_bytes()).hexdigest()
(host / 'project.godot').write_text('config_version=5\n[application]\nconfig/name="GFX Parity Audit"\n[rendering]\nrenderer/rendering_method="gl_compatibility"\n', encoding='utf-8')
(host.parent / 'godot-source-hashes.json').write_text(json.dumps(hashes, indent=2), encoding='utf-8')
print(f'Prepared {host}')
