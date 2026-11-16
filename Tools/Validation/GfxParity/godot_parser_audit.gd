extends SceneTree
const Parser=preload("gfx_parser.gd")
func _initialize():
 var results=[];var parser=Parser.new()
 for file in JSON.parse_string(FileAccess.get_file_as_string("res://../corpus-manifest.json")):
  results.append({"path":file.path,"parsed":parser._parse_full_gfx_file_chunked(file.path)})
 Parser.release_decoder()
 FileAccess.open("res://../godot-parser.json",FileAccess.WRITE).store_string(JSON.stringify(results,"  "))
 print("PARSED_FILES ",results.size());quit()
