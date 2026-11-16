import itertools,json,pathlib
out=pathlib.Path(__file__).resolve().parents[3]/'.validation/gfx-parity'
cases=[]
for kind,bind,surface,seed,time,pose in itertools.product(['122','125'],[False,True],[False,True],[0,41],[100,250,500,700,800],[False,True]):
 for mode in range(3 if kind=='122' else 4):
  cases.append(dict(id=len(cases),kind=kind,bind=bind,surface=surface,seed=seed,time=time,affector=0,quota=8,normal=mode>0,perp=mode!=2,average=mode>0,sequential=mode!=2,radius=0 if mode==0 else (.001 if mode==3 else .4),pose=pose))
(out/'geometry-cases.json').write_text(json.dumps(cases),encoding='utf-8')
print(len(cases))
