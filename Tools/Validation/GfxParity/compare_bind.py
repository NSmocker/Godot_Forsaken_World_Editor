import json,pathlib
root=pathlib.Path(__file__).resolve().parents[3]/'.validation/gfx-parity'
g=json.loads((root/'godot-bind.json').read_text(encoding='utf-8-sig'));u=json.loads((root/'unity-bind.json').read_text(encoding='utf-8-sig'))
assert g['times']==u['times'],(g['times'],u['times'])
gr={(r['name'],r['element'],r['time']):r for r in g['records']}
ur={(r['name'],r['element'],r['time']):r for r in u['records']}
# Godot-бік: рендер 128px на 4 units -> 1px = 0.03125. Центроїд і ширина квада
# квантовані пікселем, тому допуски ширші за типовий CPU 1e-5.
TOL_X=0.03;TOL_W=0.06
fail=[]
for k in sorted(set(gr)|set(ur),key=str):
 if k not in gr or k not in ur:
  fail.append({'key':str(k),'missing':'godot' if k not in gr else 'unity'});continue
 a,b=gr[k],ur[k];diffs=[]
 if a['pixels']<=0:diffs.append({'field':'visible','godotPixels':a['pixels'],'unityVisible':b['visible']})
 if not b['visible']:diffs.append({'field':'visible','godotPixels':a['pixels'],'unityVisible':False})
 if a['boundShader']!=b['bindActive']:diffs.append({'field':'boundBranch','godot':a['boundShader'],'unity':b['bindActive']})
 if a['centroidWorldX']!=-999.0 and abs(a['centroidWorldX']-b['worldX'])>TOL_X+1e-4*abs(a['centroidWorldX']):
  diffs.append({'field':'x','godot':a['centroidWorldX'],'unity':b['worldX']})
 if a['centroidWorldY']!=-999.0 and abs(a['centroidWorldY']-b['worldY'])>TOL_X+1e-4*abs(a['centroidWorldY']):
  diffs.append({'field':'y','godot':a['centroidWorldY'],'unity':b['worldY']})
 if a['widthWorld']>0 and abs(a['widthWorld']-b['quadWidth'])>TOL_W+1e-4*abs(a['widthWorld']):
  diffs.append({'field':'width','godot':a['widthWorld'],'unity':b['quadWidth']})
 if diffs:fail.append({'key':str(k),'diffs':diffs})
report={'total':len(gr),'passed':len(gr)-len(fail),'failed':len(fail),
 'tolerance':{'x':TOL_X,'width':TOL_W,'note':'Godot render256px/8units: 1px=0.03125; CPU-порівняння тут неможливе'},
 'godotTimes':g['times'],'failures':fail}
(root/'bind-comparison.json').write_text(json.dumps(report,indent=2,ensure_ascii=False),encoding='utf-8')
print({k:v for k,v in report.items() if k!='failures'});print(json.dumps(fail[:3],indent=2,ensure_ascii=False))
