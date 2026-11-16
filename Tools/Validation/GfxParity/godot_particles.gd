extends SceneTree
const Eval=preload("gfx_particle_eval.gd")
const Builder=preload("create_id_120.gd")
func a(v):
 if v is Vector3:return [v.x,v.y,v.z]
 if v is Quaternion:return [v.x,v.y,v.z,v.w]
 return v
func _initialize():
 var results=[]
 for item in JSON.parse_string(FileAccess.get_file_as_string("res://../particle-cases.json")):
  var uniforms={"Pool_Size":8,"Quota":8,"EmissionRate":10.0,"TTL":0.7,"Speed":3.0,"Angle":0.4,"Random_Seed":int(item.seed),"IsBind":item.bind,"IsSurface":item.surface,"GlobalLoopTime":3000.0,"LocalTotalTime":3000.0,"RepeatCount":1,"AreaSize":Vector3(1,2,3)}
  var builder=Builder.new();uniforms["Pool_Size"]=builder._calc_pool_size({"Quota":int(item.quota),"TTL":0.7,"EmissionRate":10.0});builder.free();uniforms["Quota"]=int(item.quota)
  var info={"type":item.kind}
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
   code+="uniform "+t+" "+key+";\n"
  var shader=Shader.new();shader.code=code;var mat=ShaderMaterial.new();mat.shader=shader
  for key in uniforms:mat.set_shader_parameter(key,uniforms[key])
  var sim=Eval.new(mat,info);var slots=[]
  for p in sim.particles(item.time):
   if p==null:slots.append(null)
   else:slots.append({"e_idx":p.e_idx,"age":p.age,"pos":a(p.pos),"move":a(p.move),"rot":a(p.rot),"size":p.size})
  results.append({"id":item.id,"slots":slots})
 FileAccess.open("res://../godot-particles.json",FileAccess.WRITE).store_string(JSON.stringify(results))
 print("PARTICLE_CASES ",results.size());quit()
