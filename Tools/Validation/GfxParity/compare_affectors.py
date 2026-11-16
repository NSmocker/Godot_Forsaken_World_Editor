import json,pathlib,math
root=pathlib.Path(__file__).resolve().parents[3]/'.validation/gfx-parity'
g=json.loads((root/'godot-affectors.json').read_text());u=json.loads((root/'unity-affectors.json').read_text());cases=json.loads((root/'affector-cases.json').read_text())
assert len(g)==len(u)==len(cases)
fail=[];pool_differences=[]
for a,b,c in zip(g,u,cases):
 assert a['id']==b['id']==c['id']
 if len(a['slots'])!=len(b['slots']):pool_differences.append({'id':c['id'],'godot':len(a['slots']),'unity':len(b['slots'])})
 differences=[]
 # Compare living emissions by identity: different pool sizes reorder circular slots.
 aa={p['e_idx']:p for p in a['slots'] if p is not None};bb={p['e_idx']:p for p in b['slots'] if p is not None}
 identities=sorted(aa.keys()|bb.keys())
 a['slots']=[aa.get(i) for i in identities];b['slots']=[bb.get(i) for i in identities]
 for i,(x,y) in enumerate(zip(a['slots'],b['slots'])):
  if x is None or y is None:
   if x!=y:differences.append({'slot':i,'field':'alive','godot':x,'unity':y})
   continue
  for k in x:
   xx=x[k] if isinstance(x[k],list) else [x[k]];yy=y[k] if isinstance(y[k],list) else [y[k]]
   assert len(xx)==len(yy)
   if k=='rot' and sum((v+w)**2 for v,w in zip(xx,yy))<sum((v-w)**2 for v,w in zip(xx,yy)):yy=[-v for v in yy]
   if any(not math.isfinite(v+w) or abs(v-w)>1e-5+1e-4*abs(v) for v,w in zip(xx,yy)):differences.append({'slot':i,'field':k,'godot':x[k],'unity':y[k]})
 if differences:fail.append({'case':c,'differences':differences})
report={'total':len(g),'passed':len(g)-len(fail),'failed':len(fail),'tolerance':'1e-5 + 1e-4*abs(Godot); living emissions matched by e_idx','pool_differences':pool_differences,'failures':fail}
(root/'affector-comparison.json').write_text(json.dumps(report,indent=2))
print({k:v for k,v in report.items() if k not in ['failures','pool_differences']});print(json.dumps(fail[:1],indent=2))
