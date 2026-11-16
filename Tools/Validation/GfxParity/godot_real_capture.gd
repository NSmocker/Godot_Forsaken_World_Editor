extends SceneTree
const Parser=preload("gfx_parser.gd")
const Sources=preload("gfx_sources.gd")
class TextureSource extends RefCounted:
 var paths=[]
 func source_texture(raw):
  var path="res://GFXEditor_Textures_out/"+str(raw).replace("\\","/")
  var tex=load(path) as Texture2D
  paths.append({"path":path,"loaded":tex!=null})
  return tex
 func warn(msg):push_warning(msg)
func _initialize():call_deferred("run")
func run():
 var vp=SubViewport.new();vp.size=Vector2i(354,394);vp.own_world_3d=true;vp.render_target_update_mode=SubViewport.UPDATE_ALWAYS;root.add_child(vp)
 var cam=Camera3D.new();vp.add_child(cam);cam.projection=Camera3D.PROJECTION_ORTHOGONAL;cam.keep_aspect=Camera3D.KEEP_HEIGHT;cam.size=1.9;cam.position=Vector3(0,0.15,-3);cam.look_at(Vector3.ZERO,Vector3.UP);cam.current=true
 var env=WorldEnvironment.new();env.environment=Environment.new();env.environment.background_mode=Environment.BG_COLOR;env.environment.background_color=Color.BLACK;env.environment.tonemap_mode=Environment.TONE_MAPPER_LINEAR;vp.add_child(env)
 var source=TextureSource.new();Sources.begin(source)
 var parser=Parser.new();var header=parser.build_file_node("res://../corpus/000.gfx","Real GFX");vp.add_child(header);header.set_process(false);header.Time_Manual=true;header.Playing=false
 var sprite_builder=load("res://create_id_101.gd").new();var duration=sprite_builder._get_max_time_for_header(header);sprite_builder.free()
 var rows=[]
 var particle_states=[]
 for element in header.get_children():
  var id=str(element.name).get_slice("ID_",1)
  var builder=load("res://create_id_"+id+".gd").new()
  var visual=builder.build_particle_node(element,duration) if id=="123" else builder.build_element_node(element,duration)
  if id=="123":element.add_child(visual)
  rows.append({"name":str(element.name),"id":id,"built":visual!=null})
  if id=="123":
   var sim=load("res://gfx_particle_eval.gd").new(visual.material_override,builder._cpu_info)
   var slots=[]
   for state in sim.particles(1000.0):
    if state!=null:slots.append({"e_idx":state.e_idx,"age":state.age,"pos":[state.pos.x,state.pos.y,state.pos.z],"size":state.size})
   particle_states.append({"index":element.get_index(),"name":str(element.name),"seed":hash(str(element.name))&0xFFFF,"slots":slots})
  builder.free()
 Sources.end()
 header.scale=Vector3(2,2,2)
 var frames=[]
 for t in [0,100,250,500,750,1000,1250,1500,1750,1999,2001,2250]:
  header.Time_Ms=float(t);header.tick(0)
  for f in range(4):await process_frame
  await RenderingServer.frame_post_draw
  vp.get_texture().get_image().save_png("res://../real-godot-%04d.png"%t)
  frames.append(t)
 header.Time_Ms=1000.0;header.tick(0)
 var elements=header.get_children()
 for i in range(elements.size()):
  for j in range(elements.size()):elements[j].visible=i==j
  for f in range(4):await process_frame
  await RenderingServer.frame_post_draw
  vp.get_texture().get_image().save_png("res://../real-godot-element-%02d.png"%i)
 for element in elements:element.visible=true
 FileAccess.open("res://../real-godot-capture.json",FileAccess.WRITE).store_string(JSON.stringify({"elements":rows,"particleStates":particle_states,"textures":source.paths,"duration":duration,"times":frames,"scale":header.scale,"renderer":RenderingServer.get_current_rendering_method()},"  "))
 Parser.release_decoder();quit()
