import copy, itertools, json, pathlib, random, math
root=pathlib.Path(__file__).resolve().parents[3]
out=root/'.validation/gfx-parity'; out.mkdir(parents=True,exist_ok=True)
data={'timing':[], 'keypoints':[], 'curves':[]}
for start,total,repeat,delay in itertools.product([0,100],[0,1000],[-1,0,1,2],[0,200]):
 for t in [-1,0,0.01,99.99,100,100.01,999.99,1000,1000.01,1199.99,1200,1200.01,2400,3000,99999]:
  data['timing'].append({'id':f'timing-{len(data["timing"])}','args':[t,3000,start,total,repeat,delay]})
def kp(ctrls=[],interp=0,span=1000):
 return {'time_span':span,'interp':interp,'pos':[1,2,3],'dir':[0,0,0,1],'color':[0.2,0.4,0.6,0.8],'scale':1,'rad':0.1,'ctrls':ctrls}
def ctrl(t):
 return {'type':t,'start':0.1,'end':0.8,'dir':[1,2,-1],'pos':[0.2,-1,0.5],'axis':[0,1,0],'vel':2,'acc':-0.3,'delta':[0.1,-0.2,0.3,-0.4],'scale_change':[-0.5,0.1,2],'base_color':[0.2,0.4,0.6,0.8],'amplitude':0.5,'wave_len':10,'octaves':3,'persistence':0.25,'count':2,'colors':[[1,0,0,1],[0,1,0,0.5],[0,0,1,0]],'times':[200,300],'alpha_only':False,'scales':[1,0.5,2],'verts':[[0,0,0],[1,2,0],[3,0,1]],'calc_dir':True}
sets=[('ctrl'+str(t),[kp([ctrl(t)])]) for t in range(100,112)]
sets += [('mixed-order-'+str(i),[kp([ctrl(t) for t in ts])]) for i,ts in enumerate([[100,102,110],[110,100,103],[105,108,106,109],[109,106,101]])]
for mode in [0,1,2]:
 for span in [-1,0,1000]:
  a=kp([ctrl(100)],mode,span);b=kp([ctrl(101)],0,500);b['pos']=[4,-1,2];b['dir']=[0,0.7071067812,0,0.7071067812];b['color']=[1,0,0,0.1];b['scale']=2
  sets.append((f'interp-{mode}-span-{span}',[a,b]))
for name,ks in sets:
 for t in [-1,0,99.99,100,100.01,199.99,200,200.01,499.99,500,799.99,800,800.01,999.99,1000,1000.01,1500,2500]:
  data['keypoints'].append({'id':f'{name}@{t}','time':t,'keypoints':ks})
# Independently verify GLM's near-parallel quaternion and long-time noise claims.
for angle in [0.1,2,3.5]:
 for t in [250,500,750]:
  a=kp([],1);b=kp([]);b['dir']=[0,math.sin(math.radians(angle)/2),0,math.cos(math.radians(angle)/2)]
  data['keypoints'].append({'id':f'glm-slerp-{angle}@{t}','time':t,'keypoints':[a,b]})
for kind,wavelength,t in itertools.product([107,109],[.01,.5,10],[100000.05,167000.005,167000.125,600000.1]):
 c=ctrl(kind);c['start']=0;c['end']=-1;c['wave_len']=wavelength
 data['keypoints'].append({'id':f'glm-noise-{kind}-{wavelength}@{t}','time':t,'keypoints':[kp([c],span=-1)]})
for pts in [[[0,0,0],[1,2,0],[3,0,1]],[[0,0,0],[0,0,0],[0,0,0]],[[0,0,0],[0,1,0],[0,2,0]],[[1,0,0],[1,0,0],[2,0,0]],[[0,0,0],[1,0,0],[0,0,0]]]:
 for ratio in [-0.1,0,0.001,0.25,0.5,0.75,0.999,1,1.1]:
  data['curves'].append({'id':f'curve-{len(data["curves"])}','verts':pts,'ratio':ratio})
(out/'cases.json').write_text(json.dumps(data,indent=2),encoding='utf-8')
print({k:len(v) for k,v in data.items()})
