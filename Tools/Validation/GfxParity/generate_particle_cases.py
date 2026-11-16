import itertools,json,pathlib
out=pathlib.Path(__file__).resolve().parents[3]/'.validation/gfx-parity'
cases=[]
for kind,bind,surface,seed,time,affector in itertools.product(['120','121','123','124'],[False,True],[False,True],[0,41,65535],[0,99.99,100,100.01,500,699.99,700,799.99,800,1499.99,1500,3000],[0,100,101,102,103,104,106,109]):
 cases.append(dict(id=len(cases),kind=kind,bind=bind,surface=surface,seed=seed,time=time,affector=affector,quota=8))
for quota,time in itertools.product([-1,0,1],[0,99.99,100,100.01,500,699.99,700,799.99,800,1499.99,1500,3000]):
 cases.append(dict(id=len(cases),kind='120',bind=True,surface=False,seed=41,time=time,affector=0,quota=quota))
(out/'particle-cases.json').write_text(json.dumps(cases),encoding='utf-8')
print(len(cases))
