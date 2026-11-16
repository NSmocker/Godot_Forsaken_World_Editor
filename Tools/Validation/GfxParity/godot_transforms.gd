extends SceneTree
const Header=preload("gfx_data_node.gd")
func vec(v):return [v.x,v.y,v.z]
func _initialize():call_deferred("run")
func run():
 var parent=Node3D.new();root.add_child(parent)
 var effect=Header.new();parent.add_child(effect);effect.set_process(false)
 var camera=Camera3D.new();root.add_child(camera);camera.position=Vector3(0,0,5)
 effect.parsed_data={"FadeByDist":"1","FadeStart":"0","FadeEnd":"10"};effect._read_cfg()
 var baseline=effect.actual_alpha(camera);parent.position=Vector3(100,0,0)
 var result={"fade":{"baselineAlpha":baseline,"translatedAlpha":effect.actual_alpha(camera),"worldDistance":camera.global_position.distance_to(effect.global_position)}}
 parent.position=Vector3.ZERO;parent.rotation_degrees=Vector3(0,90,0);effect.basis=Basis.IDENTITY
 effect.parsed_data={"NoChangeDir":"1"};effect._read_cfg();effect.apply_no_change_dir()
 result["noChangeDir"]={"worldForward":vec(effect.global_basis.z),"worldPosition":vec(effect.global_position)}
 effect.basis=Basis.IDENTITY;effect.parsed_data={"FaceToViewer":"1"};effect._read_cfg();effect._apply_face(camera)
 result["faceToViewer"]={"worldForward":vec(effect.global_basis.z),"targetDirection":vec((camera.global_position-effect.global_position).normalized()),"alignment":effect.global_basis.z.dot((camera.global_position-effect.global_position).normalized())}
 FileAccess.open("res://../godot-transforms.json",FileAccess.WRITE).store_string(JSON.stringify(result,"  "));print("TRANSFORM_AUDIT ",JSON.stringify(result));quit()
