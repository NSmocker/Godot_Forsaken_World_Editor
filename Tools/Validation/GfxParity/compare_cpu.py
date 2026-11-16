import json,pathlib,math
root=pathlib.Path(__file__).resolve().parents[3]/'.validation/gfx-parity'
g=json.loads((root/'godot-cpu.json').read_text(encoding='utf-8-sig'));u=json.loads((root/'unity-cpu.json').read_text(encoding='utf-8-sig'))
report={'godot':g['engine'],'unity':u['engine'],'tolerance':'abs<=1e-5+1e-4*abs(reference); quaternion min(q/-q) component comparison','groups':{},'failures':[]}
for group in ['timing','keypoints','curves']:
 failures=0
 assert len(g[group])==len(u[group])
 for a,b in zip(g[group],u[group]):
  assert a['id']==b['id']
  diff={}
  for key in a:
   if key=='id':continue
   x=a[key];y=b[key]
   xs=x if isinstance(x,list) else [x];ys=y if isinstance(y,list) else [y]
   assert len(xs)==len(ys), (group,a['id'],key,'vector length mismatch')
   if key=='dir' and sum((xx+yy)**2 for xx,yy in zip(xs,ys))<sum((xx-yy)**2 for xx,yy in zip(xs,ys)):ys=[-v for v in ys]
   if any(not math.isfinite(xx) or not math.isfinite(yy) or abs(xx-yy)>1e-5+1e-4*abs(xx) for xx,yy in zip(xs,ys)):
    diff[key]={'godot':x,'unity':y}
  if diff:
   failures+=1;report['failures'].append({'group':group,'id':a['id'],'differences':diff})
 report['groups'][group]={'total':len(g[group]),'passed':len(g[group])-failures,'failed':failures}
(root/'cpu-comparison.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps(report['groups'],indent=2));print(json.dumps(report['failures'][:8],indent=2))
