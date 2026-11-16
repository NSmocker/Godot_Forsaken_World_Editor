extends SceneTree
const KP=preload("gfx_keypoint_eval.gd")
const CP=preload("gfx_curve_path.gd")
func v(a): return Vector3(a[0],a[1],a[2])
func q(a): return Quaternion(a[0],a[1],a[2],a[3])
func c(a): return Color(a[0],a[1],a[2],a[3])
func arr(x):
 if x is Vector3: return [x.x,x.y,x.z]
 if x is Quaternion: return [x.x,x.y,x.z,x.w]
 if x is Color: return [x.r,x.g,x.b,x.a]
 return x
func control(raw):
 var d=raw.duplicate(true)
 for key in ["dir","pos","axis","scale_change"]: d[key]=v(d[key])
 for key in ["delta","base_color"]: d[key]=c(d[key])
 d["colors"]=d["colors"].map(c)
 if int(d.type)==110:
  var path=CP.gen_path(d.verts.map(v))
  d.merge(path,true)
 return d
func keypoint(raw):
 var d=raw.duplicate(true)
 d.pos=v(d.pos);d.dir=q(d.dir);d.color=c(d.color);d.ctrls=d.ctrls.map(control)
 return d
func _initialize():
 var input=JSON.parse_string(FileAccess.get_file_as_string("../cases.json"))
 var output={"engine":Engine.get_version_info().string,"timing":[],"keypoints":[],"curves":[]}
 for item in input.timing:
  var a=item.args
  output.timing.append({"id":item.id,"value":KP.element_time(a[0],a[1],a[2],a[3],int(a[4]),a[5])})
 for item in input.keypoints:
  var state=KP.evaluate(item.keypoints.map(keypoint),item.time)
  var row={"id":item.id}
  for key in ["pos","dir","color","scale","rad"]: row[key]=arr(state[key])
  output.keypoints.append(row)
 for item in input.curves:
  var path=CP.gen_path(item.verts.map(v))
  var pt=CP.interp_point(path,item.ratio)
  output.curves.append({"id":item.id,"pos":arr(pt.pos),"dir":arr(pt.quat),"length":path.total_len})
 var f=FileAccess.open("../godot-cpu.json",FileAccess.WRITE)
 f.store_string(JSON.stringify(output,"\t"));f.close()
 print("AUDIT_COUNTS ",output.timing.size()," ",output.keypoints.size()," ",output.curves.size())
 quit()
