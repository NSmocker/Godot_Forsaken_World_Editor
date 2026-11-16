"""Fixtures derived from GLM's delegated work package, with complete stage arrays."""
import json,pathlib,itertools
out=pathlib.Path(__file__).resolve().parents[3]/'.validation/gfx-parity'
def ctrl(t,**kw):return dict(CtrlType=str(t),StartTime='0',EndTime='-1',**{k:str(v) for k,v in kw.items()})
a105=ctrl(105,ColorDelta='20, -30, 40, -128')
a107=ctrl(107,BaseColor=-8388353,Amplitude=200,WaveLen=2,OctaveNum=3,Persistence=.6)
default107=ctrl(107,BaseColor=-8388353,Amplitude=200,WaveLen=2)
a108=ctrl(108,Count=2,Color=-1,Color_2=-65536,Color_3=-16776961,TimeSpan=500,TimeSpan_2=750,AlphaOnly=0)
a111=ctrl(111,Count=2,Scale=.5,Scale_2=2,Scale_3=1,TimeSpan=1000,TimeSpan_2=1500)
a110=ctrl(110,Count=2,Pos='1, 0, 0',Pos_2='2, 1, 0')
variants={'105-raw':[a105],'107-pinned':[a107],'107-default':[default107],'108':[a108],'108-alpha':[{**a108,'AlphaOnly':'1'}],'111':[a111],'110-two':[a110],'110-three':[{**a110,'Count':'3','Pos_3':'3, 2, 0'}],'combined':[a107,a105,a108,a111],'hsv':[]}
rows=[]
for (name,raw),time,seed in itertools.product(variants.items(),[0,500,750,1000,1250,1500,1750,2000,2500,2999,3250],[0,12345]):
 rows.append(dict(id=len(rows),variant=name,time=time,seed=seed,hsv=name=='hsv',raw=raw))
for time,seed in itertools.product([800,801,810,900,1000,1300],[0,12345]):
 rows.append(dict(id=len(rows),variant='user-emitter105',time=time,seed=seed,hsv=False,raw=[{'CtrlType':'105','StartTime':'0.800000','EndTime':'-1.000000','ColorDelta':'0, 0, 0, -500'}]))
(out/'affector-cases.json').write_text(json.dumps(rows,indent=2),encoding='utf-8');print(len(rows))
