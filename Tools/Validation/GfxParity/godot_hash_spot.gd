extends SceneTree
func _initialize():
	var result = {}
	for text in ["", "Element_0", "Element_17", "Полум'я", "火🔥"]:
		result[text] = hash(text)
	var file = FileAccess.open("B:/Git/Godot_Forsaken_World_Editor/.validation/gfx-parity/godot-hash-spot.json", FileAccess.WRITE)
	file.store_string(JSON.stringify(result, "  "))
	quit()
