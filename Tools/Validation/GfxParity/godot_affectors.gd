extends SceneTree
const Eval=preload("gfx_particle_eval.gd")
const Builder=preload("create_id_120.gd")
func a(v):
 if v is Vector3:return [v.x,v.y,v.z]
 if v is Quaternion or v is Vector4:return [v.x,v.y,v.z,v.w]
 return v
func _initialize():
 var results=[]
 for item in JSON.parse_string(FileAccess.get_file_as_string("res://../affector-cases.json")):
  # Pool_Size must come from production _calc_pool_size like create_id_120.gd:179/472
  # does for real files (hardcoding 8 diverged once Unity gained the same formula).
  var pool_builder=Builder.new()
  var pool_size=pool_builder._calc_pool_size({"Quota":8,"TTL":3.0,"EmissionRate":2.0})
  pool_builder.free()
  var uniforms={"Pool_Size":pool_size,"Quota":8,"EmissionRate":2.0,"TTL":3.0,"Speed":0.0,"Random_Seed":int(item.seed),"IsBind":true,"GlobalLoopTime":10000.0,"LocalTotalTime":10000.0,"RepeatCount":1,"IsUseHSVInterp":item.hsv,"ColorMin":Vector4(1,0,0,1) if item.hsv else Vector4.ONE,"ColorMax":Vector4(1,1,0,1) if item.hsv else Vector4.ONE}
  var info={"type":"120","hsv":item.hsv,"affectors":[]}
  for i in range(item.raw.size()):
   var raw=item.raw[i];var node=load("gfx_ctrl_"+raw.CtrlType+".gd").new();node.parsed_data=raw
   var prefix="Affector_"+str(i)+"_";info.affectors.append({"p":prefix,"t":raw.CtrlType})
   if raw.CtrlType=="110":
    var builder=Builder.new();builder._declare_path_array(node,prefix,{},uniforms,{});builder.free()
   for key in raw:
    if key=="CtrlType" or key=="Pos" or key.begins_with("Pos_"):continue
    var value=node.get(key)
    if value is Color:value=Vector4(value.r,value.g,value.b,value.a)
    uniforms[prefix+key]=value
   node.free()
  if item.variant=="user-emitter105":
   info["ctrls"]=[{"p":"Affector_0_","t":"105","base":0.0,"dur":2000.0,"blocked":false}];info.affectors=[]
  var code="shader_type spatial;\n"
  for key in uniforms:
   var v=uniforms[key];var t="float"
   if v is bool:t="bool"
   elif v is int:t="int"
   elif v is Vector3:t="vec3"
   elif v is Vector4:t="vec4"
   if v is PackedVector3Array:code+="uniform vec3 "+key+"["+str(v.size())+"];\n"
   else:code+="uniform "+t+" "+key+";\n"
  var shader=Shader.new();shader.code=code;var mat=ShaderMaterial.new();mat.shader=shader
  for key in uniforms:mat.set_shader_parameter(key,uniforms[key])
  var sim=Eval.new(mat,info);var slots=[]
  for p in sim.particles(item.time):
   if p==null:slots.append(null)
   else:slots.append({"e_idx":p.e_idx,"age":p.age,"pos":a(p.pos),"move":a(p.move),"rot":a(p.rot),"size":p.size,"color":a(p.col),"keyAlpha":p.k_alpha})
  results.append({"id":item.id,"slots":slots})
 FileAccess.open("res://../godot-affectors.json",FileAccess.WRITE).store_string(JSON.stringify(results));print("AFFECTOR_CASES ",results.size());quit()
