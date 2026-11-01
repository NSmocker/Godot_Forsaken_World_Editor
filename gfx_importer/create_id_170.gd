@tool
extends "create_id_130.gd"

# ====================================================
# GFXELEMENTID 170 = ID_ELE_TYPE_SOUND (A3DGFXSound / GFXSOUNDIMP)
# ====================================================
# Kein Bild, sondern ein Sound aus Sfx\<Path>; bei mehreren Path-Einträgen wird beim Start
# zufällig gewählt. Godot lädt die Dateien aus
#   res://GFXEditor_Sfx_out/<Path>   (.wav / .ogg / .mp3, Groß/Klein egal)
# wie create_id_122 / create_id_160 die Modelle.
# Start, Wiederholung, Ende und Position steuert gfx_sound_170.gd jeden Frame aus der GFX-Zeit
# und den KeyPoints; Play-/Stop-Buttons im Inspektor spielen den Sound zur Vorschau ab.
# Fehlt eine Datei, entsteht der Node trotzdem; Sounds lassen sich im Inspektor nachtragen.
# Der Audio-Event-Pfad (Angelica 2.2, GFXSOUNDIMP22) wird nur zur Info übernommen.
# ====================================================

const SOUND_NODE_NAME = "Sound_GFX"
const SOUND_DRIVER_SCRIPT = "gfx_sound_170.gd"
const SFX_DIR = "res://GFXEditor_Sfx_out/"
const SOUND_EXTENSIONS = ["wav", "ogg", "mp3", "tres", "res"]


func _element_id() -> String:
	return "170"

func _node_name() -> String:
	return SOUND_NODE_NAME

func _node_label() -> String:
	return "Sounds"

func build_node(parent_node: Node, max_group_time: float) -> Node3D:
	return build_sound_node(parent_node, max_group_time)


# ====================================================
# BAUT DEN SOUND (ohne Szene / UndoRedo -> auch für Tests nutzbar)
# ====================================================
func build_sound_node(parent_node: Node, max_group_time: float) -> Node3D:
	var parsed_data = parent_node.get("parsed_data")
	if typeof(parsed_data) != TYPE_DICTIONARY:
		return null

	var paths = _sound_paths(parsed_data)
	var streams: Array[AudioStream] = []
	var missing = PackedStringArray()
	for p in paths:
		var stream = _load_sound(p)
		streams.append(stream)
		if stream == null:
			missing.append(SFX_DIR + p.replace("\\", "/"))
	if paths.is_empty():
		push_warning("GFX Sound: keine Sounddatei (Path) in " + str(parent_node.name))
	elif not missing.is_empty():
		push_warning("GFX Sound: nicht gefunden: " + ", ".join(missing) + " -> Sounds im Inspektor nachtragen")

	var sound = Node3D.new()
	sound.name = SOUND_NODE_NAME
	sound.set_script(load(_driver_path(SOUND_DRIVER_SCRIPT)))
	_apply_time_settings(sound, parsed_data, max_group_time)

	# Werte roh aus parsed_data; fehlende Zeilen (ältere SoundVer) = Standardwerte aus GfxSoundParamInfo()
	sound.Sound_Paths = paths
	sound.Sounds = streams
	sound.Loop = _as_bool(parsed_data.get("IsLoop", "0"))
	var vol = _volume_range(parsed_data)
	sound.Volume_Min = vol[0]
	sound.Volume_Max = vol[1]
	var pitch_min = clampf(_float(parsed_data, "PitchMin", 0.0), -12.0, 12.0)
	var pitch_max = clampf(_float(parsed_data, "PitchMax", 0.0), -12.0, 12.0)
	sound.Pitch_Min = minf(pitch_min, pitch_max)
	sound.Pitch_Max = maxf(pitch_min, pitch_max)
	sound.Force_2D = _as_bool(parsed_data.get("Force2D", "0"))
	sound.Min_Dist = _float(parsed_data, "MinDist", 12.0)
	sound.Max_Dist = _float(parsed_data, "MaxDist", 50.0)
	sound.Audio_Event = _audio_event(parsed_data, paths.size())
	sound.keypoints = _build_keypoints(parent_node, parsed_data)

	# Startzustand für den Editor (Sound am ersten KeyPoint)
	if not sound.keypoints.is_empty():
		sound.position = sound.keypoints[0]["pos"]
	return sound


# ====================================================
# PARAMETER AUS GFXSOUNDIMP::Load / GfxSoundParamInfo::LoadSoundParamInfo
# ====================================================
# "PathNum: n" + n Zeilen "Path: <Datei>" (Dateiversion < 88: eine Path-Zeile ohne PathNum).
# Der Parser nummeriert doppelte Schlüssel: Path, Path_2, Path_3 ...
func _sound_paths(parsed_data: Dictionary) -> PackedStringArray:
	var count = 0
	if parsed_data.has("PathNum"):
		count = int(str(parsed_data["PathNum"]))
	elif parsed_data.has("Path"):
		count = 1
	var paths = PackedStringArray()
	for i in range(count):
		var p = str(parsed_data.get(_path_key(i), "")).strip_edges()
		if p != "" and not p in paths: # RandStringContainer::UniqueAdd
			paths.append(p)
	return paths

func _path_key(index: int) -> String:
	return "Path" if index == 0 else "Path_" + str(index + 1)

# GFXSOUNDIMP22 (Dateiversion >= 96): die Path-Zeile nach den Sound-Parametern, gefolgt von UseCustom
func _audio_event(parsed_data: Dictionary, path_count: int) -> String:
	if not parsed_data.has("UseCustom"):
		return ""
	var count = int(str(parsed_data["PathNum"])) if parsed_data.has("PathNum") else path_count
	return str(parsed_data.get(_path_key(count), "")).strip_edges()

# SoundVer <= 1: "Volume", ab 2: "VolMin" / "VolMax" (0..100)
func _volume_range(parsed_data: Dictionary) -> Array:
	var vol_min = 100
	var vol_max = 100
	if parsed_data.has("VolMin") or parsed_data.has("VolMax"):
		vol_min = int(str(parsed_data.get("VolMin", "100")))
		vol_max = int(str(parsed_data.get("VolMax", "100")))
	elif parsed_data.has("Volume"):
		vol_min = int(str(parsed_data["Volume"]))
		vol_max = vol_min
	vol_min = clampi(vol_min, 0, 100)
	vol_max = clampi(vol_max, 0, 100)
	return [mini(vol_min, vol_max), maxi(vol_min, vol_max)]


# ====================================================
# KONVERTIERTE SOUNDDATEI SUCHEN
# ====================================================
func _load_sound(sound_path: String) -> AudioStream:
	if sound_path == "":
		return null
	var rel = sound_path.replace("\\", "/")
	var candidates = [SFX_DIR + rel]
	for ext in SOUND_EXTENSIONS:
		candidates.append(SFX_DIR + rel.get_basename() + "." + ext)
	# Windows-Pfade in der .gfx sind nicht groß/klein-sensitiv, res:// unter Linux schon
	var ci_path = _find_sound_ignore_case(rel)
	if ci_path != "":
		candidates.append(ci_path)

	for path in candidates:
		if not ResourceLoader.exists(path):
			continue
		var res = load(path)
		if res is AudioStream:
			return res
	return null

# Sucht SFX_DIR/<rel> Segment für Segment ohne Groß/Klein-Unterscheidung (beliebige Sound-Endung)
func _find_sound_ignore_case(rel: String) -> String:
	var parts = rel.split("/", false)
	if parts.is_empty():
		return ""
	var dir_path = SFX_DIR.trim_suffix("/")
	for i in range(parts.size()):
		var is_last = (i == parts.size() - 1)
		var wanted = (parts[i].get_basename() if is_last else parts[i]).to_lower()
		var dir = DirAccess.open(dir_path)
		if dir == null:
			return ""
		var found = ""
		if is_last:
			for f in dir.get_files():
				if f.get_basename().to_lower() == wanted and f.get_extension().to_lower() in SOUND_EXTENSIONS:
					found = f
					break
		else:
			for d in dir.get_directories():
				if d.to_lower() == wanted:
					found = d
					break
		if found == "":
			return ""
		dir_path = dir_path.path_join(found)
	return dir_path
