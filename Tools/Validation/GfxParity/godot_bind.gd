extends SceneTree
const Parser=preload("gfx_parser.gd")
const Builder=preload("create_id_101.gd")
const Builder100=preload("create_id_100.gd")
# Сітка без 0: елементи з StartTime>0 невидимі при active<StartTime (create_id_101.gd:1381),
# 250ms — найменша кратна 125ms точка, для якої всі фікстури видимі.
const TIMES=[250,500,750,1000,1250,1500]
func _initialize():call_deferred("run")
func run():
 var vp=SubViewport.new();vp.size=Vector2i(256,256);vp.own_world_3d=true;vp.render_target_update_mode=SubViewport.UPDATE_ALWAYS;root.add_child(vp)
 var cam=Camera3D.new();vp.add_child(cam);cam.position=Vector3(0,0,5 if "--near-camera" in OS.get_cmdline_user_args() else 500);cam.projection=Camera3D.PROJECTION_ORTHOGONAL;cam.size=8;cam.current=true
 var env=WorldEnvironment.new();env.environment=Environment.new();env.environment.background_mode=Environment.BG_COLOR;env.environment.background_color=Color.BLACK;vp.add_child(env)
 var records=[];var parser=Parser.new()
 for item in JSON.parse_string(FileAccess.get_file_as_string("res://../corpus-manifest.json")):
  if item.kind!="synthetic-binding":continue
  var header=parser.build_file_node(item.path,"Audit");vp.add_child(header);header.set_process(false)
  var elem_count=header.get_child_count()
  for ci in range(elem_count):
   var element=header.get_child(ci)
   print("BIND_STEP ",item.name," element ",ci,"/",elem_count)
   var builder=Builder100.new() if str(element.name).ends_with("ID_100") else Builder.new();var sprite=builder.build_element_node(element,2000);builder.free()
   var im=Image.create(2,2,false,Image.FORMAT_RGBA8);im.fill(Color.WHITE);var tex=ImageTexture.create_from_image(im);sprite.texture=tex;var mat=sprite.material_override;mat.set_shader_parameter("TexFile",tex);mat.set_shader_parameter("Time_Manual",true)
   var bound=mat.shader.code.contains("float fb_total")
   for t in TIMES:
    mat.set_shader_parameter("Time",float(t))
    for f in range(4):await process_frame
    await RenderingServer.frame_post_draw
    var capture=vp.get_texture().get_image()
    if t==750:capture.save_png("res://../godot-"+str(item.name)+"-e"+str(ci)+".png")
    var min_x=999.0;var max_x=-1.0;var sum_x=0.0;var sum_y=0.0;var count=0
    for y in range(256):
     for x in range(256):
      if capture.get_pixel(x,y).r>0.5:
       count+=1;sum_x+=x+0.5;sum_y+=y+0.5
       if x+0.5<min_x:min_x=x+0.5
       if x+0.5>max_x:max_x=x+0.5
    records.append({"name":str(item.name),"element":ci,"time":t,"pixels":count,"centroidWorldX":(sum_x/count-128)*8/256 if count>0 else -999.0,"centroidWorldY":(128-sum_y/count)*8/256 if count>0 else -999.0,"widthWorld":(max_x-min_x+1)*8/256 if count>0 else 0.0,"boundShader":bound})
   sprite.queue_free();await process_frame
  header.queue_free();await process_frame
 Parser.release_decoder();FileAccess.open("res://../godot-bind.json",FileAccess.WRITE).store_string(JSON.stringify({"times":TIMES,"records":records},"  "));print("BIND_AUDIT records=",records.size());quit()
