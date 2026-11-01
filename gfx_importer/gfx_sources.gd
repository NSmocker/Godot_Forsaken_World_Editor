@tool
extends RefCounted

# ====================================================
# Woher die GFX-Elemente Texturen, Unter-GFX und Modelle holen:
#  - Import aus der Leiste ECM-GFX-MAP (ecm_importer.gd ruft begin() / end()): nur aus den Spieldaten
#    (res://.resources), Pfade wie in Angelica: gfx\textures\<TexFile>, gfx\<GfxPath>,
#    gfx\models\<ModelPath> (.smd -> Szene wie beim ECM-Import), ECMPath relativ zum Spielordner.
#  - sonst wie bisher aus den Bibliotheken res://GFX_Import/ (Unter-GFX) und
#    res://GFXEditor_Textures_out/ bzw. res://GFXEditor_Models_out/ (GFX-Importer, Erstell-Buttons,
#    "Unter-GFX neu laden"); Modelle sucht dann das Element selbst.
# ====================================================

const LIBRARY_TEXTURES = "res://GFXEditor_Textures_out/"

static var _importer: WeakRef = null


static func begin(importer) -> void:
	_importer = weakref(importer)

static func end() -> void:
	_importer = null

# Laufender Import (ecm_importer.gd) oder null
static func importer():
	return _importer.get_ref() if _importer != null else null

static func active() -> bool:
	return importer() != null

# TexFile der .gfx -> Textur, null = fehlt (Hinweis im Import bzw. Warnung)
static func texture(tex_path) -> Texture2D:
	var raw = str(tex_path).strip_edges()
	if raw == "":
		return null
	var imp = importer()
	if imp != null:
		return imp.source_texture(raw)
	var full_path = LIBRARY_TEXTURES + raw.replace("\\", "/")
	if ResourceLoader.exists(full_path):
		return load(full_path) as Texture2D
	push_warning("Textur konnte nicht gefunden werden: " + full_path)
	return null

# GfxPath (relativ zum gfx-Ordner) -> .gfx-Datei, "" = fehlt
static func gfx_file(gfx_path: String) -> String:
	var imp = importer()
	if imp != null:
		return imp.source_gfx(gfx_path)
	return preload("gfx_parser.gd").new().find_library_file(gfx_path)

# ModelPath (relativ zu gfx\Models) -> Szene des Modells; nur während eines Imports
static func model(model_path: String) -> PackedScene:
	var imp = importer()
	return imp.source_model(model_path) if imp != null else null

# ECMPath -> {"scene": Szene oder null, "ecm": .ecm-Pfad oder ""}; nur während eines Imports
static func ecm(ecm_path: String) -> Dictionary:
	var imp = importer()
	return imp.source_ecm(ecm_path) if imp != null else {"scene": null, "ecm": ""}

# Hinweis in den laufenden Import, sonst als Warnung
static func warn(msg: String) -> void:
	var imp = importer()
	if imp != null:
		imp.warn(msg)
	else:
		push_warning(msg)
