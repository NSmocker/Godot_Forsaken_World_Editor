import pathlib,zipfile,json,hashlib
root=pathlib.Path(__file__).resolve().parents[3];out=root/'.validation/gfx-parity/corpus';out.mkdir(exist_ok=True)
manifest=[]
def add(name,data,kind):
 p=out/f'{len(manifest):03d}.gfx';p.write_bytes(data)
 manifest.append(dict(path=p.as_posix(),name=name,kind=kind,sha256=hashlib.sha256(data).hexdigest()))
with zipfile.ZipFile(r'C:/Users/sinch/Downloads/GFX Extracted.zip') as z:
 for info in z.infolist():
  if info.filename.lower().endswith('.gfx'):add(info.filename,z.read(info),'user-archive')
base='GFXELEMENTCOUNT: 1\nGFXELEMENTID: 101\nName: 火焰测试\nTexFile: 火焰/纹理.dds\nWidth: 1\nWidth: 2\nOrgPt: 0.25\nOrgPt: 0.75\nKEYPOINTCOUNT: 2\nInterpolateMode: 0\nTimeSpan: 1000\nCtrlMethodCount: 1\nCtrlType: 100\nStartTime: 0\nEndTime: -1\nDir: 1, 0, 0\nVel: 2\nInterpolateMode: 0\nTimeSpan: 500\nCtrlMethodCount: 0\n'
for enc in ['utf-8','utf-8-sig','gbk']:
 for newline in ['\n','\r\n']:add(enc+repr(newline),base.replace('\n',newline).encode(enc),'synthetic-valid')
add('grid','GFXELEMENTID: 210\nGridAnimationLines: 2\nAAAA\nBBBB\nwNumber: 2\nhNumber: 2\n'.encode(),'synthetic-structure')
add('standalone-controls','GFXELEMENTID: 120\nCtrlMethodCount: 1\nCtrlType: 100\nVel: 2\nStartTime: 0\nKEYPOINTCOUNT: 1\nInterpolateMode: 0\nTimeSpan: 500\nCtrlMethodCount: 0\n'.encode(),'synthetic-structure')
for bound in [False,True]:
 text='GFXELEMENTCOUNT: 2\nGFXELEMENTID: 101\nName: Target\nTexFile: \nStartTime: 0\nRepeatCount: -1\nRepeatDelay: 0\nWidth: 0.5\nHeight: 0.5\nTexRow: 1\nTexCol: 1\nTexInterval: 50\nRotFromView: 0\nDestBlend: 2\nKEYPOINTCOUNT: 1\nInterpolateMode: 0\nTimeSpan: 2000\nCtrlMethodCount: 0\nGFXELEMENTID: 101\nName: Follower\nBindEle: '+('Target' if bound else '')+'\nTexFile: \nDestBlend: 2\nWidth: 0.1\nHeight: 0.1\nRotFromView: 0\nTexRow: 1\nTexCol: 1\nTexInterval: 50\nRepeatCount: -1\nRepeatDelay: 0\nStartTime: 0\nKEYPOINTCOUNT: 1\nInterpolateMode: 0\nTimeSpan: 500\nPosition: 0, 0, 0\nDirection: 0, 0, 0, 1\nScale: 1\nColor: 4294967295\nCtrlMethodCount: 1\nCtrlType: 100\nStartTime: 0\nEndTime: -1\nDir: 1, 0, 0\nVel: 1\nAcc: 0\n'
 add('bind-on' if bound else 'bind-off',text.encode(),'synthetic-binding')
add('colonless-grid',b'GFXELEMENTID: 210\nwNumber: 2\nhNumber: 2\n1 2 3\n4 5 6\n7 8 9\n10 11 12\n','synthetic-structure')
add('malformed-count',b'GFXELEMENTID: 100\nAffectorCount: 2abc\nCtrlType: 5\nCtrlType: 6\n','malformed')

# --- A03 BindEle: прив'язки різних типів (див. GFX_PARITY_AUDIT_RESULTS, прохід7) ---
# Роздільна здатність 128px/4units: x має бути кратним 0.25 (125ms), щоб центроїд
# пікселів збігався з центром квада точно. StartTime цілей: 0 або 250.
def _sprite(name,bind,start,ctrls,timespan):
 s='GFXELEMENTID: 101\nName: '+name+'\n'
 if bind is not None: s+='BindEle: '+bind+'\n'
 s+=('TexFile: \nDestBlend: 2\nWidth: 0.5\nHeight: 0.5\nRotFromView: 0\nTexRow: 1\nTexCol: 1\nTexInterval: 50\n'
  'RepeatCount: -1\nRepeatDelay: 0\nStartTime: '+start+'\n'
  'KEYPOINTCOUNT: 1\nInterpolateMode: 0\nTimeSpan: '+timespan+'\n'
  'Position: 0, 0, 0\nDirection: 0, 0, 0, 1\nScale: 1\nColor: 4294967295\n'+ctrls)
 return s
C_NONE='CtrlMethodCount: 0\n'
C_MOVE='CtrlMethodCount: 1\nCtrlType: 100\nStartTime: 0\nEndTime: -1\nDir: 1, 0, 0\nVel: 1\nAcc: 0\n'
C_MOVE_LOCAL=('CtrlMethodCount: 2\nCtrlType: 100\nStartTime: 0\nEndTime: -1\nDir: 1, 0, 0\nVel: 1\nAcc: 0\n'
 'CtrlType: 106\nStartTime: 0\nEndTime: -1\nScaleChage: 2, 0, 10\n')
def add_binding(name,elements):
 add(name,('GFXELEMENTCOUNT: %d\n'%len(elements)+''.join(elements)).encode(),'synthetic-binding')
add_binding('bind-missing',[_sprite('Target',None,'0',C_NONE,'2000'),_sprite('Follower','NoSuchTarget','0',C_MOVE,'500')])
add_binding('bind-target-start',[_sprite('Target',None,'250',C_NONE,'2000'),_sprite('Follower','Target','0',C_MOVE,'500')])
add_binding('bind-duplicate',[_sprite('Target',None,'0',C_NONE,'2000'),_sprite('Target',None,'250',C_NONE,'2000'),_sprite('Follower','Target','0',C_MOVE,'500')])
add_binding('bind-cycle',[_sprite('Alpha','Beta','0',C_MOVE,'500'),_sprite('Beta','Alpha','250',C_MOVE,'500')])
add_binding('bind-ctrl-local',[_sprite('Target',None,'0',C_NONE,'2000'),_sprite('Follower','Target','0',C_MOVE_LOCAL,'500')])

# Bound time has passed keypoint 1 while the local 200ms loop is back in keypoint 0.
future=_sprite('Follower','Target','0',C_NONE,'100').replace('KEYPOINTCOUNT: 1','KEYPOINTCOUNT: 2')
future+='InterpolateMode: 0\nTimeSpan: 100\nPosition: 0, 0, 0\nDirection: 0, 0, 0, 1\nScale: 1\nColor: 4294967295\n'+C_MOVE
add_binding('bind-future-keypoint',[_sprite('Target',None,'0',C_NONE,'2000'),future])

# Delayed movement acceleration: shader integrates elapsed time from control start.
for bound in [False,True]:
 for end in ['-1','1']:
  ctrl=C_MOVE.replace('StartTime: 0','StartTime: 0.5').replace('EndTime: -1','EndTime: '+end).replace('Vel: 1','Vel: 0.25').replace('Acc: 0','Acc: 2')
  add_binding('bind-delayed-acc-'+str(bound)+'-'+end,[_sprite('Target',None,'250',C_NONE,'2000'),_sprite('Follower','Target' if bound else None,'0',ctrl,'2000')])
  add_binding('bind-delayed-acc-100-'+str(bound)+'-'+end,[_sprite('Target',None,'250',C_NONE,'2000'),_sprite('Follower','Target' if bound else None,'0',ctrl,'2000').replace('GFXELEMENTID: 101','GFXELEMENTID: 100')])

# GLM delegated fixture: collinear uniform points isolate Ctrl110 timing from geometry.
def _c110(start,end):
 return ('CtrlMethodCount: 1\nCtrlType: 110\nStartTime: '+start+'\nEndTime: '+end+
         '\nCalcDir: 0\nCount: 3\nPos: 0, 0, 0\nPos_2: 0.25, 0, 0\nPos_3: 0.5, 0, 0\n')
for system in ['100','101']:
 for bound in [False,True]:
  for end in ['-1','0.4']:
   for start in ['0','0.1']:
    follower=_sprite('Follower','Target' if bound else None,'0',_c110(start,end),'500').replace('GFXELEMENTID: 101','GFXELEMENTID: '+system)
    add_binding('c110-'+system+'-'+str(bound)+'-'+start+'-'+end,[_sprite('Target',None,'250',C_NONE,'2000'),follower])

# Nonuniform/bent paths distinguish raw-index interpolation from smoothed arc length.
for system in ['100','101']:
 for kind,points,base in [
  ('uneven',['0, 0, 0','0.1, 0, 0','1, 0, 0'],'0, 0, 0'),
  ('bend',['0, 0, 0','0.1, 0.5, 0','1, 0, 0'],'0, 0, 0'),
  ('offset',['0.2, 0, 0','0.3, 0, 0','0.8, 0, 0'],'0.25, 0, 0'),
  ('two',['0, 0, 0','1, 0, 0'],'0, 0, 0')]:
  ctrl='CtrlMethodCount: 1\nCtrlType: 110\nStartTime: 0\nEndTime: 1\nCalcDir: 0\nCount: '+str(len(points))+'\n'
  ctrl+=''.join(('Pos' if j==0 else 'Pos_'+str(j+1))+': '+v+'\n' for j,v in enumerate(points))
  follower=_sprite('Follower','Target','0',ctrl,'500').replace('GFXELEMENTID: 101','GFXELEMENTID: '+system).replace('Position: 0, 0, 0','Position: '+base)
  add_binding('c110-geometry-'+system+'-'+kind,[_sprite('Target',None,'250',C_NONE,'2000'),follower])

# Pre-start and same-keypoint path suppression; CalcDir kept off to isolate positions.
for system in ['100','101']:
 path=_c110('0','1').replace('Pos_2: 0.25','Pos_2: 0.5').replace('Pos_3: 0.5','Pos_3: 1')
 pre=_c110('1','1.5').replace('Pos: 0,','Pos: 0.25,').replace('Pos_2: 0.25','Pos_2: 0.5').replace('Pos_3: 0.5','Pos_3: 0.75')
 add_binding('c110-prestart-'+system,[_sprite('Target',None,'250',C_NONE,'2000'),_sprite('Follower','Target','0',pre,'500').replace('GFXELEMENTID: 101','GFXELEMENTID: '+system)])
 for kind in ['100','104']:
  move=C_MOVE if kind=='100' else C_MOVE.replace('CtrlType: 100','CtrlType: 104').replace('Dir: 1, 0, 0','Pos: 1, 0, 0')
  for order in [False,True]:
   a,b=(move,path) if order else (path,move)
   ctrl=a.replace('CtrlMethodCount: 1','CtrlMethodCount: 2')+b.replace('CtrlMethodCount: 1\n','')
   add_binding('c110-suppress-'+system+'-'+kind+'-'+str(order),[_sprite('Target',None,'250',C_NONE,'2000'),_sprite('Follower','Target','0',ctrl,'500').replace('GFXELEMENTID: 101','GFXELEMENTID: '+system)])

(out.parent/'corpus-manifest.json').write_text(json.dumps(manifest,indent=2,ensure_ascii=True))
print({'files':len(manifest),'archive_gfx':sum(x['kind']=='user-archive' for x in manifest)})
