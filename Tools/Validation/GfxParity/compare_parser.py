import pathlib,json
root=pathlib.Path(__file__).resolve().parents[3]/'.validation/gfx-parity'
def diff(a,b,path=''):
 if type(a)!=type(b):return [{'path':path,'godot':a,'unity':b}]
 if isinstance(a,dict):return sum((diff(a.get(k),b.get(k),path+'/'+k) for k in sorted(a.keys()|b.keys())),[])
 if isinstance(a,list):
  if len(a)!=len(b):return [{'path':path+'/length','godot':len(a),'unity':len(b)}]+sum((diff(x,y,path+'/'+str(i)) for i,(x,y) in enumerate(zip(a,b))),[])
  return sum((diff(x,y,path+'/'+str(i)) for i,(x,y) in enumerate(zip(a,b))),[])
 return [] if a==b else [{'path':path,'godot':a,'unity':b}]
g=json.loads((root/'godot-parser.json').read_text(encoding='utf-8'));u=json.loads((root/'unity-parser.json').read_text(encoding='utf-8'));meta=json.loads((root/'corpus-manifest.json').read_text(encoding='utf-8'));assert len(g)==len(u)==len(meta)
rows=[]
for a,b,m in zip(g,u,meta):
 assert a['path']==b['path']==m['path']
 rows.append({'name':m['name'],'kind':m['kind'],'elements':len(a['parsed']['elements']),'differences':diff(a['parsed'],b['parsed']),'warnings':b['warnings']})
(root/'parser-comparison.json').write_text(json.dumps(rows,indent=2,ensure_ascii=True))
for r in rows:print(r['kind'],r['name'].encode('ascii','backslashreplace').decode(),'elements',r['elements'],'differences',len(r['differences']))
