extends SceneTree
const Eval=preload("gfx_particle_eval.gd")
const Builder=preload("create_id_120.gd")
const CurveBuilder=preload("create_id_125.gd")
func a(v):
 if v is Vector3:return [v.x,v.y,v.z]
 if v is Quaternion:return [v.x,v.y,v.z,v.w]
 return v
func _initialize():
 var results=[]
 for item in JSON.parse_string(FileAccess.get_file_as_string("res://../geometry-cases.json")):
  var uniforms={"Pool_Size":8,"Quota":8,"EmissionRate":10.0,"TTL":0.7,"Speed":3.0,"Angle":0.4,"Random_Seed":int(item.seed),"IsBind":item.bind,"IsSurface":item.surface,"GlobalLoopTime":3000.0,"LocalTotalTime":3000.0,"RepeatCount":1,"AreaSize":Vector3(1,2,3)}
  var builder=Builder.new();uniforms["Pool_Size"]=builder._calc_pool_size({"Quota":int(item.quota),"TTL":0.7,"EmissionRate":10.0});builder.free();uniforms["Quota"]=int(item.quota)
  uniforms.merge({"UseNormal":item.normal,"PerpToNormal":item.perp,"IsAvgGen":item.average,"IsAvgGenSeq":item.sequential,"AvgGenSeg":4,"Avg_Step_Count":5,"Radius":float(item.radius),"MultiPlane_Points":PackedVector3Array([Vector3(1,0,0),Vector3(0,2,1),Vector3(-1,0,2)]),"MultiPlane_Normals":PackedVector3Array([Vector3(0,0,1),Vector3(0,1,0),Vector3(1,0,0)]),"Curve_Points":PackedVector3Array([Vector3(0,0,0),Vector3(1,2,0),Vector3(3,2,1)]),"Curve_Ratios":PackedFloat32Array([0,0.5,1]),"Curve_Dirs":PackedVector3Array([Vector3(0,0,1),Vector3(0,1,0),Vector3(1,0,0)])})
  uniforms.merge({"KP_0_Position":Vector3(1,2,3) if item.pose else Vector3.ZERO,"KP_0_Direction":Vector4(0,0.70710678,0,0.70710678) if item.pose else Vector4(0,0,0,1),"KP_0_Scale":2.0 if item.pose else 1.0})
  var info={"type":item.kind,"origin_pos":true}
  if item.affector!=0:
   info["affectors"]=[{"p":"A_","t":str(int(item.affector))}]
   if item.affector==102 or item.affector==103:info["axis_prefix"]="A_"
   uniforms.merge({"A_StartTime":0.1,"A_EndTime":0.6,"A_Vel":2.0,"A_Acc":-0.3,"A_Dir":Vector3(1,2,-1),"A_Pos":Vector3(0.2,-1,0.5),"A_Axis":Vector3(0,1,0),"A_ScaleChange":Vector3(-0.5,0.1,2),"A_WaveLen":10.0,"A_OctaveNum":3,"A_Persistence":0.25,"A_Amplitude":0.5})
  var code="shader_type spatial;\n"
  for key in uniforms:
   var v=uniforms[key];var t="float"
   if v is bool:t="bool"
   elif v is int:t="int"
   elif v is Vector3:t="vec3"
   elif v is Vector4:t="vec4"
   if v is PackedVector3Array:code+="uniform vec3 "+key+"[3];\n"
   elif v is PackedFloat32Array:code+="uniform float "+key+"[3];\n"
   else:code+="uniform "+t+" "+key+";\n"
  var shader=Shader.new();shader.code=code;var mat=ShaderMaterial.new();mat.shader=shader
  for key in uniforms:mat.set_shader_parameter(key,uniforms[key])
  var sim=Eval.new(mat,info);var slots=[]
  for p in sim.particles(item.time):
   if p==null:slots.append(null)
   else:slots.append({"e_idx":p.e_idx,"age":p.age,"pos":a(p.pos),"move":a(p.move),"rot":a(p.rot),"size":p.size})
  results.append({"id":item.id,"slots":slots})
 FileAccess.open("res://../godot-geometry.json",FileAccess.WRITE).store_string(JSON.stringify(results))
 var curve_builder=CurveBuilder.new();var counts=[]
 for n in range(0,65):counts.append({"segments":n,"steps":curve_builder._avg_step_count(n)})
 curve_builder.free()
 FileAccess.open("res://../godot-curve-step-counts.json",FileAccess.WRITE).store_string(JSON.stringify(counts))
 print("PARTICLE_CASES ",results.size());quit()
