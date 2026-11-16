extends SceneTree
const Builder=preload("create_id_101.gd")
const Element=preload("gfx_element_101.gd")
class Header extends Node3D:
 var parsed_data: Dictionary={}
var viewport: SubViewport
var capture_index=0
func _initialize():
 call_deferred("run")
func pixel(x=32,y=32):
 for i in range(4): await process_frame
 await RenderingServer.frame_post_draw
 var captured=viewport.get_texture().get_image()
 captured.save_png("res://../godot-"+RenderingServer.get_current_rendering_method()+"-"+str(capture_index)+".png")
 capture_index+=1
 return captured.get_pixel(x,y)
func rgba(c):return [c.r,c.g,c.b,c.a]
func make_sprite(header):
 var e=Element.new();header.add_child(e)
 e.parsed_data={"TexFile":"","DestBlend":"2","Width":"1","Height":"1","TexRow":"1","TexCol":"1","TexInterval":"50","StartTime":"0","RepeatCount":"1","RepeatDelay":"0","UVExchg":"false","_keypoints":[{"TimeSpan":"3000","Scale":"1","Color":"4294967295","Position":"0, 0, 0","Direction":"0, 0, 0, 1"}]}
 e.parsed_data["RotFromView"]="false"
 var builder=Builder.new();var sprite=builder.build_element_node(e,3000);builder.free()
 var im=Image.create(2,2,false,Image.FORMAT_RGBA8);im.fill(Color.WHITE)
 var tex=ImageTexture.create_from_image(im);sprite.texture=tex
 var m=sprite.material_override;m.set_shader_parameter("TexFile",tex);m.set_shader_parameter("Time_Manual",true);m.set_shader_parameter("Time",0.0)
 return sprite
func run():
 viewport=SubViewport.new();viewport.size=Vector2i(64,64);viewport.own_world_3d=true;viewport.render_target_update_mode=SubViewport.UPDATE_ALWAYS;root.add_child(viewport)
 var world=Node3D.new();viewport.add_child(world)
 var env=WorldEnvironment.new();var settings=Environment.new();settings.background_mode=Environment.BG_COLOR;settings.background_color=Color.BLACK;settings.tonemap_mode=Environment.TONE_MAPPER_LINEAR;env.environment=settings;world.add_child(env)
 var camera=Camera3D.new();camera.projection=Camera3D.PROJECTION_ORTHOGONAL;camera.size=2;camera.position=Vector3(0,0,3);world.add_child(camera);camera.current=true
 var header=Header.new();world.add_child(header);var sprite=make_sprite(header);var mat=sprite.material_override
 var full=await pixel();mat.set_shader_parameter("GFX_Alpha",0.5);var half=await pixel();mat.set_shader_parameter("GFX_Alpha",1.0)
 var im=Image.create(2,2,false,Image.FORMAT_RGBA8);im.set_pixel(0,0,Color.RED);im.set_pixel(1,0,Color.GREEN);im.set_pixel(0,1,Color.BLUE);im.set_pixel(1,1,Color.WHITE)
 var pattern=ImageTexture.create_from_image(im);mat.set_shader_parameter("TexFile",pattern)
 var uv0=await pixel(48,16);mat.set_shader_parameter("UVExchg",true);var uv1=await pixel(48,16)
 var white=Image.create(2,2,false,Image.FORMAT_RGBA8);white.fill(Color.WHITE);mat.set_shader_parameter("TexFile",ImageTexture.create_from_image(white));mat.set_shader_parameter("UVExchg",false)
 var cube=MeshInstance3D.new();cube.mesh=BoxMesh.new();cube.scale=Vector3(2,2,0.2);cube.position=Vector3(0,0,1);var opaque=StandardMaterial3D.new();opaque.shading_mode=BaseMaterial3D.SHADING_MODE_UNSHADED;opaque.albedo_color=Color.BLACK;cube.material_override=opaque;world.add_child(cube)
 var depth0=await pixel();sprite.visible=false
 var header2=Header.new();header2.parsed_data={"2DRender":"1"};world.add_child(header2);var sprite2=make_sprite(header2);var depth2=await pixel()
 var result={"engine":Engine.get_version_info().string,"renderer":RenderingServer.get_current_rendering_method(),"alpha":{"full":rgba(full),"half":rgba(half),"rawRatio":half.r/full.r if full.r>0 else -1,"linearizedRatio":half.srgb_to_linear().r/full.srgb_to_linear().r if full.r>0 else -1},"uvExchange":{"normal":rgba(uv0),"exchange":rgba(uv1)},"depth":{"normal":rgba(depth0),"header2d":rgba(depth2)}}
 result["valid_control"]=full.r>0.9 and depth0.r<0.05
 FileAccess.open("res://../godot-render-"+RenderingServer.get_current_rendering_method()+".json",FileAccess.WRITE).store_string(JSON.stringify(result,"  "))
 FileAccess.open("res://../godot-sprite-generated.gdshader",FileAccess.WRITE).store_string(sprite2.material_override.shader.code)
 print("RENDER_AUDIT ",JSON.stringify(result));quit(0 if result.valid_control else 2)
