using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
using UnityEngine;

namespace SilverEcho.ForsakenWorld.Editor
{
    public sealed class GameDataBrowserWindow : EditorWindow
    {
        private const string RootPreference = "SilverEcho.ForsakenWorld.GameDataRoot";
        private const string PageSizePreference = "SilverEcho.ForsakenWorld.GameDataPageSize";
        private const string ImportRoot = "Assets/ForsakenWorld/Imported";
        private static readonly string[] PageSizeLabels = { "50", "100", "250", "500" };
        private static GfxTextDecoder gameDataDecoder;
        private static readonly string[] FileTypes =
            { "All", "ECM", "GFX", "Models", "Skeletons", "Skins", "Tracks", "Maps", "Buildings" };
        private static readonly string[] Extensions =
            { "", "ecm", "gfx", "smd", "bon", "ski", "stck", "trn2", "bmd" };

        private string rootPath;
        private string searchText = string.Empty;
        private string status = "Choose the extracted game data folder and scan it.";
        private int selectedFileType;
        private Vector2 fileListScroll;
        private Vector2 detailsScroll;
        private GameDataIndex index;
        private IReadOnlyList<GameDataFile> visibleFiles = Array.Empty<GameDataFile>();
        private GameDataFile selectedFile;
        private GfxDocument selectedGfx;
        private EcmDocument selectedEcm;
        private SmdDocument selectedSmd;
        private BonDocument selectedBon;
        private SkiDocument selectedSki;
        private StckDocument selectedStck;
        private TerrainDocument selectedTerrain;
        private EcwldDocument selectedEcwld;
        private EcbsdDocument selectedEcbsd;
        private string selectedWorldStatus;
        private BmdDocument selectedBuilding;
        private AssembledBuilding selectedAssembledBuilding;
        private AssembledModel selectedAssembledModel;
        private int previewAnimationClipIndex;
        private Task<GameDataIndex> scanTask;
        private int scannedDirectories;
        private int scannedFiles;
        private int lastReportedDirectoryCount;
        private int pageSize = 100;
        private int pageIndex;
        private ListPagination pagination = ListPagination.Create(0, 100, 0);
        private IReadOnlyList<GameDataFile> pageFiles = Array.Empty<GameDataFile>();
        private GameDataFolderTree.BuildResult folderTree;
        private readonly HashSet<string> expandedFolders =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> filledFolders =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private Vector2 folderTreeScroll;
        private bool showFolderTree = true;
        private string scanTime = string.Empty;

        [MenuItem("Tools/Forsaken World/Game Data Browser")]
        private static void Open()
        {
            GetWindow<GameDataBrowserWindow>("FW Data");
        }

        private void OnEnable()
        {
            rootPath = EditorPrefs.GetString(RootPreference, string.Empty);
            pageSize = ListPagination.NormalizePageSize(
                EditorPrefs.GetInt(PageSizePreference, 100));
            pagination = ListPagination.Create(0, pageSize, 0);
            EditorApplication.update += PollScanTask;
            TryLoadScanCache();
        }

        private void OnDisable()
        {
            EditorApplication.update -= PollScanTask;
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Browse extracted .ecm, .gfx, .smd, .bon, .ski, .stck, .trn2, .ecwld, .ecbsd, and .bmd files. Supported text, " +
                "model, terrain, and building files can be inspected and imported as Unity assets/prefabs.",
                MessageType.Info);

            DrawRootPicker();
            DrawSearchControls();
            DrawStatus();

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawFileList();
                DrawSelectionDetails();
            }
        }

        private void DrawRootPicker()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Game data", GUILayout.Width(72));
                rootPath = EditorGUILayout.TextField(rootPath);

                if (GUILayout.Button("Browse...", GUILayout.Width(90)))
                {
                    var folder = EditorUtility.OpenFolderPanel(
                        "Select extracted game data",
                        Directory.Exists(rootPath) ? rootPath : string.Empty,
                        string.Empty);
                    if (!string.IsNullOrEmpty(folder))
                        rootPath = folder;
                }

                using (new EditorGUI.DisabledScope(scanTask != null || string.IsNullOrWhiteSpace(rootPath)))
                {
                    if (GUILayout.Button("Scan", GUILayout.Width(70)))
                        StartScan();
                }
            }
        }

        private void DrawSearchControls()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUI.BeginChangeCheck();
                searchText = GUILayout.TextField(
                    searchText,
                    GUI.skin.FindStyle("ToolbarSeachTextField") ?? EditorStyles.toolbarTextField);
                selectedFileType = EditorGUILayout.Popup(
                    selectedFileType,
                    FileTypes,
                    EditorStyles.toolbarPopup,
                    GUILayout.Width(100));
                if (EditorGUI.EndChangeCheck())
                    UpdateVisibleFiles();
            }
        }

        private void DrawStatus()
        {
            EditorGUILayout.LabelField(status, EditorStyles.miniLabel);
        }

        private void DrawFileList()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.MinWidth(240), GUILayout.MaxWidth(420)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(
                        "Files (" + visibleFiles.Count + ")",
                        EditorStyles.boldLabel);
                    showFolderTree = GUILayout.Toggle(
                        showFolderTree,
                        "Tree",
                        EditorStyles.toolbarButton,
                        GUILayout.Width(48));
                }

                if (showFolderTree)
                    DrawFolderTree();
                else
                {
                    DrawPaginationControls();
                    fileListScroll = EditorGUILayout.BeginScrollView(fileListScroll);
                    for (var i = 0; i < pageFiles.Count; i++)
                    {
                        var file = pageFiles[i];
                        var label = file.RelativePath;
                        if (GUILayout.Toggle(
                            selectedFile == file,
                            label,
                            "Button",
                            GUILayout.ExpandWidth(true)))
                        {
                            if (selectedFile != file)
                                SelectFile(file);
                        }
                    }

                    EditorGUILayout.EndScrollView();
                }
            }
        }

        private void DrawFolderTree()
        {
            if (folderTree == null)
            {
                EditorGUILayout.HelpBox(
                    "Scan game data (or load cache) to build the folder tree.",
                    MessageType.None);
                return;
            }

            folderTreeScroll = EditorGUILayout.BeginScrollView(folderTreeScroll);
            foreach (var pair in folderTree.Root.Dirs)
                DrawFolderNode(pair.Key, pair.Value, pair.Key, 0);
            DrawFolderFiles(folderTree.Root, string.Empty, 0);
            EditorGUILayout.EndScrollView();
        }

        private void DrawFolderNode(
            string name,
            GameDataFolderTree.FolderNode node,
            string rel,
            int depth)
        {
            // Godot: depth 0 open; expand_all opens everything; else lazy via Foldout.
            var forceOpen = folderTree != null && folderTree.ExpandAll;
            var open = forceOpen || expandedFolders.Contains(rel) || depth == 0;
            if (depth == 0)
                expandedFolders.Add(rel);

            EditorGUI.BeginChangeCheck();
            open = EditorGUILayout.Foldout(open, name + "  (" + node.Count + ")", true);
            if (EditorGUI.EndChangeCheck() && !forceOpen)
            {
                if (open)
                    expandedFolders.Add(rel);
                else
                    expandedFolders.Remove(rel);
            }

            if (!open && !forceOpen)
                return;

            EditorGUI.indentLevel++;
            foreach (var child in node.Dirs)
            {
                var childRel = string.IsNullOrEmpty(rel) ? child.Key : rel + "/" + child.Key;
                DrawFolderNode(child.Key, child.Value, childRel, depth + 1);
            }

            DrawFolderFiles(node, rel, depth + 1);
            EditorGUI.indentLevel--;
        }

        private void DrawFolderFiles(GameDataFolderTree.FolderNode node, string rel, int depth)
        {
            for (var i = 0; i < node.Files.Count; i++)
            {
                var fileName = node.Files[i];
                var relative = string.IsNullOrEmpty(rel) ? fileName : rel + "/" + fileName;
                GameDataFile file = null;
                string error;
                if (index != null)
                    index.TryResolve(relative, out file, out error);
                var selected = file != null && selectedFile == file;
                var shown = Path.GetFileNameWithoutExtension(fileName);
                if (GUILayout.Toggle(selected, shown, "Button", GUILayout.ExpandWidth(true)))
                {
                    if (file != null && selectedFile != file)
                        SelectFile(file);
                }
            }
        }

        private void DrawPaginationControls()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUI.BeginChangeCheck();
                var sizeIndex = ListPagination.IndexOfAllowedPageSize(pageSize);
                sizeIndex = EditorGUILayout.Popup(
                    sizeIndex,
                    PageSizeLabels,
                    EditorStyles.toolbarPopup,
                    GUILayout.Width(60));
                if (EditorGUI.EndChangeCheck())
                {
                    pageSize = ListPagination.AllowedPageSizes[sizeIndex];
                    EditorPrefs.SetInt(PageSizePreference, pageSize);
                    pageIndex = 0;
                    RefreshPagination();
                }

                using (new EditorGUI.DisabledScope(!pagination.HasPrevious))
                {
                    if (GUILayout.Button("|<", EditorStyles.toolbarButton, GUILayout.Width(28)))
                    {
                        pageIndex = 0;
                        RefreshPagination();
                    }

                    if (GUILayout.Button("<", EditorStyles.toolbarButton, GUILayout.Width(24)))
                    {
                        pageIndex = Math.Max(0, pageIndex - 1);
                        RefreshPagination();
                    }
                }

                var pageLabel = pagination.PageCount == 0
                    ? "0 / 0"
                    : pagination.DisplayPage + " / " + pagination.PageCount;
                GUILayout.Label(pageLabel, EditorStyles.miniLabel, GUILayout.Width(64));

                using (new EditorGUI.DisabledScope(!pagination.HasNext))
                {
                    if (GUILayout.Button(">", EditorStyles.toolbarButton, GUILayout.Width(24)))
                    {
                        pageIndex++;
                        RefreshPagination();
                    }

                    if (GUILayout.Button(">|", EditorStyles.toolbarButton, GUILayout.Width(28)))
                    {
                        pageIndex = Math.Max(0, pagination.PageCount - 1);
                        RefreshPagination();
                    }
                }
            }
        }

        private void DrawSelectionDetails()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                detailsScroll = EditorGUILayout.BeginScrollView(detailsScroll);
                if (selectedFile == null)
                {
                    EditorGUILayout.HelpBox(
                        "Select a file to inspect its parsed data and import it as a Unity asset.",
                        MessageType.None);
                }
                else
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(selectedFile.RelativePath, EditorStyles.boldLabel);
                        if (GUILayout.Button("Reveal", GUILayout.Width(70)))
                            EditorUtility.RevealInFinder(selectedFile.FullPath);
                    }

                    using (new EditorGUI.DisabledScope(
                        !CanImportSelectedFile()))
                    {
                        if (GUILayout.Button("Import selected asset"))
                            ImportSelectedAsset();
                    }

                    using (new EditorGUI.DisabledScope(
                        selectedFile == null ||
                        selectedFile.Extension != "trn2" ||
                        selectedTerrain == null))
                    {
                        if (GUILayout.Button("Import terrain Prefab"))
                            ImportSelectedTerrain();
                    }

                    using (new EditorGUI.DisabledScope(
                        selectedFile == null ||
                        selectedFile.Extension != "trn2" ||
                        selectedTerrain == null ||
                        index == null))
                    {
                        if (GUILayout.Button("Import map Prefab (terrain + buildings + world)"))
                            ImportSelectedMap();
                    }

                    using (new EditorGUI.DisabledScope(
                        selectedFile == null ||
                        selectedFile.Extension != "bmd" ||
                        selectedBuilding == null ||
                        selectedAssembledBuilding == null ||
                        selectedAssembledBuilding.CollideOnly ||
                        selectedAssembledBuilding.Surfaces.Length == 0))
                    {
                        if (GUILayout.Button("Import building Prefab"))
                            ImportSelectedBuilding();
                    }

                    using (new EditorGUI.DisabledScope(
                        selectedAssembledModel == null ||
                        !selectedAssembledModel.IsValid ||
                        selectedAssembledModel.Animations.Count == 0))
                    {
                        if (GUILayout.Button("Import assembled AnimationClips"))
                            ImportAssembledAnimationClips();
                    }

                    using (new EditorGUI.DisabledScope(
                        selectedAssembledModel == null ||
                        !selectedAssembledModel.IsValid ||
                        !HasAssembledMaterials(selectedAssembledModel)))
                    {
                        if (GUILayout.Button("Import assembled URP materials + textures"))
                            ImportAssembledMaterials();
                    }

                    using (new EditorGUI.DisabledScope(
                        selectedAssembledModel == null ||
                        !selectedAssembledModel.IsValid))
                    {
                        if (GUILayout.Button("Import assembled model Prefab (mesh + clips)"))
                            ImportAssembledModelPrefab();
                    }

                    using (new EditorGUI.DisabledScope(
                        selectedAssembledModel == null ||
                        !selectedAssembledModel.IsValid))
                    {
                        if (GUILayout.Button("Instantiate assembled model in scene"))
                            InstantiateAssembledModelInScene(withAnimation: false);
                    }

                    using (new EditorGUI.DisabledScope(
                        selectedAssembledModel == null ||
                        !selectedAssembledModel.IsValid ||
                        selectedAssembledModel.Animations.Count == 0))
                    {
                        previewAnimationClipIndex = EditorGUILayout.IntField(
                            "Preview clip index",
                            previewAnimationClipIndex);
                        if (GUILayout.Button("Instantiate model + play AnimationClip"))
                            InstantiateAssembledModelInScene(withAnimation: true);
                    }

                    if (selectedGfx != null)
                        DrawGfxDetails(selectedGfx);
                    else if (selectedEcm != null)
                        DrawEcmDetails(selectedEcm);
                    else if (selectedSmd != null)
                        DrawSmdDetails(selectedSmd);
                    else if (selectedBon != null)
                        DrawBonDetails(selectedBon);
                    else if (selectedSki != null)
                        DrawSkiDetails(selectedSki);
                    else if (selectedStck != null)
                        DrawStckDetails(selectedStck);
                    else if (selectedTerrain != null)
                    {
                        DrawTerrainDetails(selectedTerrain);
                        DrawWorldDetailsBesideTerrain();
                    }
                    else if (selectedEcwld != null)
                        DrawWorldDetails(selectedEcwld, selectedEcbsd, selectedWorldStatus);
                    else if (selectedBuilding != null)
                        DrawBuildingDetails(selectedBuilding, selectedAssembledBuilding);
                    else
                        EditorGUILayout.HelpBox(
                            "Selected " + selectedFile.Extension.ToUpperInvariant() +
                            " file. A reader for this format has not been ported yet.",
                            MessageType.Info);

                    if (selectedAssembledModel != null)
                        DrawAssembledModelDetails(selectedAssembledModel);
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawGfxDetails(GfxDocument document)
        {
            EditorGUILayout.LabelField("Header", EditorStyles.boldLabel);
            DrawProperties(document.Header);

            EditorGUILayout.Space();
            if (GUILayout.Button("Instantiate GFX runtime in scene (core + stubs)"))
                InstantiateGfxRuntimeInScene(document);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "Elements (" + document.Elements.Count + ")",
                EditorStyles.boldLabel);
            foreach (var element in document.Elements)
            {
                EditorGUILayout.LabelField(
                    "ID " + element.SystemId +
                    " — affectors " + element.Affectors.Count +
                    ", controls " + element.ControlMethods.Count +
                    ", keypoints " + element.Keypoints.Count,
                    EditorStyles.miniBoldLabel);
                DrawProperties(element.Properties);
            }

            foreach (var warning in document.Warnings)
                EditorGUILayout.HelpBox(warning, MessageType.Warning);
        }

        private void DrawEcmDetails(EcmDocument document)
        {
            if (!string.IsNullOrEmpty(document.Error))
            {
                EditorGUILayout.HelpBox(document.Error, MessageType.Error);
                return;
            }

            EditorGUILayout.LabelField("Skin model", EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(
                document.SkinModelPath,
                EditorStyles.textField,
                GUILayout.MinHeight(EditorGUIUtility.singleLineHeight));

            EditorGUILayout.LabelField("Additional skins", EditorStyles.boldLabel);
            foreach (var path in document.AdditionalSkinPaths)
                EditorGUILayout.SelectableLabel(
                    path,
                    EditorStyles.textField,
                    GUILayout.MinHeight(EditorGUIUtility.singleLineHeight));

            EditorGUILayout.LabelField(
                "CoGfx events (" + document.CoGfxEvents.Count + ")",
                EditorStyles.boldLabel);
            foreach (var coGfx in document.CoGfxEvents)
            {
                EditorGUILayout.LabelField(
                    "Event type " + coGfx.EventType,
                    EditorStyles.miniBoldLabel);
                foreach (var path in coGfx.Files)
                    EditorGUILayout.SelectableLabel(
                        path,
                        EditorStyles.textField,
                        GUILayout.MinHeight(EditorGUIUtility.singleLineHeight));
                DrawProperties(coGfx.Properties);
                foreach (var parameter in coGfx.Parameters)
                    DrawProperties(parameter);
            }

            EditorGUILayout.LabelField(
                "Combined actions (" + document.CombinedActions.Count + ")",
                EditorStyles.boldLabel);
            foreach (var action in document.CombinedActions)
            {
                EditorGUILayout.LabelField(
                    action.Name + " from " + string.Join(", ", action.BaseActionNames),
                    EditorStyles.miniLabel);
            }

            foreach (var warning in document.Warnings)
                EditorGUILayout.HelpBox(warning, MessageType.Warning);
        }

        private void DrawSmdDetails(SmdDocument document)
        {
            DrawSmdPath("Skeleton", document.SkeletonPath);
            EditorGUILayout.LabelField(
                "Skin files (" + document.SkinPaths.Count + ")",
                EditorStyles.boldLabel);
            foreach (var path in document.SkinPaths)
                DrawSmdPath("Skin", path);
            DrawSmdPath("Physics", document.PhysicsPath);
            DrawSmdPath("Track collection", document.TrackCollectionPath);
            EditorGUILayout.LabelField("Hangers: " + document.HangerCount);
            EditorGUILayout.LabelField(
                "Actions (" + document.Actions.Count + ")",
                EditorStyles.boldLabel);
            foreach (var action in document.Actions)
            {
                EditorGUILayout.LabelField(
                    action.Name + " (" + action.StartFrame + "–" + action.EndFrame + ")",
                    EditorStyles.miniBoldLabel);
                DrawSmdPath("Track", action.TrackPath);
            }
        }

        private static void DrawSmdPath(string label, string path)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(110));
                EditorGUILayout.SelectableLabel(
                    path,
                    EditorStyles.textField,
                    GUILayout.MinHeight(EditorGUIUtility.singleLineHeight));
            }
        }

        private void DrawBonDetails(BonDocument document)
        {
            EditorGUILayout.LabelField("Frame rate: " + document.FramesPerSecond);
            EditorGUILayout.LabelField(
                "Bones (" + document.Bones.Count + ")",
                EditorStyles.boldLabel);
            foreach (var bone in document.Bones)
            {
                EditorGUILayout.LabelField(
                    bone.Name + " (parent " + bone.ParentIndex +
                    ", first joint " + bone.FirstJointIndex +
                    ", children " + bone.ChildIndices.Count + ")",
                    EditorStyles.miniBoldLabel);
            }

            EditorGUILayout.LabelField(
                "Joints (" + document.Joints.Count + ")",
                EditorStyles.boldLabel);
            foreach (var joint in document.Joints)
            {
                EditorGUILayout.LabelField(
                    joint.Name + " (" + joint.ParentBoneIndex + " → " +
                    joint.ChildBoneIndex + ", sibling " + joint.SiblingJointIndex + ")",
                    EditorStyles.miniLabel);
            }

            EditorGUILayout.LabelField(
                "Hooks (" + document.Hooks.Count + ")",
                EditorStyles.boldLabel);
            foreach (var hook in document.Hooks)
            {
                EditorGUILayout.LabelField(
                    hook.Name + " (type " + hook.Type + ", bone " + hook.BoneIndex + ")",
                    EditorStyles.miniLabel);
            }
        }

        private void DrawStckDetails(StckDocument document)
        {
            EditorGUILayout.LabelField(
                "Animation range: " + document.AnimationStart + "–" + document.AnimationEnd +
                " ms, frame rate " + document.FramesPerSecond);
            EditorGUILayout.LabelField(
                "Joint tracks (" + document.Tracks.Count + ")",
                EditorStyles.boldLabel);
            foreach (var track in document.Tracks)
            {
                EditorGUILayout.LabelField(
                    "Joint " + track.JointIndex +
                    " — pos keys " + track.Position.KeyCount +
                    "/segs " + track.Position.SegmentCount +
                    ", rot keys " + track.Rotation.KeyCount +
                    "/segs " + track.Rotation.SegmentCount,
                    EditorStyles.miniLabel);
            }
        }

        private void DrawSkiDetails(SkiDocument document)
        {
            EditorGUILayout.LabelField(
                "Skeleton bones: " + document.SkeletonBoneCount +
                " (skin bone names: " + document.BoneNames.Count + ")");
            EditorGUILayout.LabelField(
                "Textures (" + document.TexturePaths.Count + ")",
                EditorStyles.boldLabel);
            foreach (var path in document.TexturePaths)
                DrawSmdPath("Texture", path);

            EditorGUILayout.LabelField(
                "Materials (" + document.Materials.Count + ")",
                EditorStyles.boldLabel);
            foreach (var material in document.Materials)
            {
                EditorGUILayout.LabelField(
                    material.Name + " (power " + material.Power +
                    (material.TwoSided ? ", two-sided)" : ")"),
                    EditorStyles.miniLabel);
            }

            DrawSkiMeshes("Skinned meshes", document.SkinMeshes);
            DrawSkiMeshes("Rigid meshes", document.RigidMeshes);
        }

        private static void DrawSkiMeshes<T>(string label, IList<T> meshes)
            where T : SkiMesh
        {
            EditorGUILayout.LabelField(
                label + " (" + meshes.Count + ")",
                EditorStyles.boldLabel);
            foreach (var mesh in meshes)
            {
                EditorGUILayout.LabelField(
                    mesh.Name + " — " + mesh.Positions.Length / 3 +
                    " vertices, " + mesh.Indices.Length + " indices, texture " +
                    mesh.TextureIndex + ", material " + mesh.MaterialIndex,
                    EditorStyles.miniLabel);
            }
        }

        private static void DrawProperties(Dictionary<string, string> properties)
        {
            foreach (var property in properties.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(property.Key, GUILayout.Width(150));
                    EditorGUILayout.SelectableLabel(
                        property.Value,
                        EditorStyles.textField,
                        GUILayout.MinHeight(EditorGUIUtility.singleLineHeight));
                }
            }
        }


        private void TryLoadScanCache()
        {
            if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
                return;
            try
            {
                var cachePath = GameDataScanCache.DefaultCachePath(Application.dataPath);
                GameDataScanCache.Snapshot snapshot;
                if (!GameDataScanCache.TryLoad(cachePath, rootPath, out snapshot))
                    return;
                index = new GameDataIndex(rootPath);
                index.ReplaceFromRelativePaths(snapshot.RelativePaths);
                scanTime = snapshot.ScanTime ?? string.Empty;
                index.ScanTime = scanTime;
                status = "Loaded cache: " + index.Files.Count + " files" +
                    (string.IsNullOrEmpty(scanTime) ? "." : "  ·  scanned " + scanTime);
                UpdateVisibleFiles();
            }
            catch (Exception exception)
            {
                status = "Could not load scan cache: " + exception.Message;
            }
        }

        private void SaveScanCache()
        {
            if (index == null)
                return;
            try
            {
                var cachePath = GameDataScanCache.DefaultCachePath(Application.dataPath);
                var paths = new List<string>(index.Files.Count);
                for (var i = 0; i < index.Files.Count; i++)
                    paths.Add(index.Files[i].RelativePath);
                GameDataScanCache.Save(cachePath, index.Root, scanTime, paths);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Forsaken World: could not save scan cache: " + exception.Message);
            }
        }

        private void RebuildFolderTree()
        {
            if (index == null)
            {
                folderTree = null;
                return;
            }

            var paths = new List<string>(visibleFiles.Count);
            for (var i = 0; i < visibleFiles.Count; i++)
                paths.Add(visibleFiles[i].RelativePath);
            folderTree = GameDataFolderTree.Build(paths, string.Empty, searchText);
            if (folderTree.ExpandAll)
            {
                expandedFolders.Clear();
                filledFolders.Clear();
            }
        }

        private void StartScan()
        {
            var requestedRoot = rootPath.Trim();
            status = "Scanning game data...";
            index = null;
            selectedFile = null;
            selectedGfx = null;
            selectedEcm = null;
            selectedSmd = null;
            selectedBon = null;
            selectedSki = null;
            selectedStck = null;
            selectedTerrain = null;
            selectedEcwld = null;
            selectedEcbsd = null;
            selectedWorldStatus = null;
            selectedBuilding = null;
            selectedAssembledBuilding = null;
            selectedAssembledModel = null;
            visibleFiles = Array.Empty<GameDataFile>();
            pageFiles = Array.Empty<GameDataFile>();
            pageIndex = 0;
            pagination = ListPagination.Create(0, pageSize, 0);
            scannedDirectories = 0;
            scannedFiles = 0;
            lastReportedDirectoryCount = 0;
            EditorPrefs.SetString(RootPreference, requestedRoot);
            scanTask = Task.Run(() =>
            {
                var newIndex = new GameDataIndex(requestedRoot);
                newIndex.Scan((directories, files) =>
                {
                    Interlocked.Exchange(ref scannedDirectories, directories);
                    Interlocked.Exchange(ref scannedFiles, files);
                });
                return newIndex;
            });
        }

        private void PollScanTask()
        {
            if (scanTask == null)
                return;

            if (!scanTask.IsCompleted)
            {
                var directories = Interlocked.CompareExchange(ref scannedDirectories, 0, 0);
                if (directories != lastReportedDirectoryCount)
                {
                    var files = Interlocked.CompareExchange(ref scannedFiles, 0, 0);
                    status = "Scanning... " + directories + " folders, " + files + " files.";
                    lastReportedDirectoryCount = directories;
                    Repaint();
                }

                return;
            }

            try
            {
                index = scanTask.GetAwaiter().GetResult();
                scanTime = GameDataScanCache.FormatScanTime(DateTime.Now);
                index.ScanTime = scanTime;
                SaveScanCache();
                status = "Indexed " + index.Files.Count + " game data files" +
                    (string.IsNullOrEmpty(scanTime) ? "." : "  ·  scanned " + scanTime);
                UpdateVisibleFiles();
            }
            catch (DirectoryNotFoundException exception)
            {
                status = exception.Message;
            }
            catch (UnauthorizedAccessException exception)
            {
                status = "Access denied while scanning: " + exception.Message;
            }
            catch (IOException exception)
            {
                status = "Could not scan game data: " + exception.Message;
            }
            catch (SecurityException exception)
            {
                status = "Permission denied while scanning: " + exception.Message;
            }
            catch (ArgumentException exception)
            {
                status = "Invalid game data path: " + exception.Message;
            }
            catch (NotSupportedException exception)
            {
                status = "Unsupported game data path: " + exception.Message;
            }
            finally
            {
                scanTask = null;
                Repaint();
            }
        }

        private void UpdateVisibleFiles()
        {
            if (index == null)
            {
                visibleFiles = Array.Empty<GameDataFile>();
                folderTree = null;
                return;
            }

            visibleFiles = index.Search(searchText, Extensions[selectedFileType]);
            RebuildFolderTree();
            pageIndex = 0;
            RefreshPagination();
            if (selectedFile != null && !visibleFiles.Contains(selectedFile))
            {
                selectedFile = null;
                selectedGfx = null;
                selectedEcm = null;
                selectedSmd = null;
                selectedBon = null;
                selectedSki = null;
                selectedStck = null;
                selectedTerrain = null;
                selectedBuilding = null;
                selectedAssembledBuilding = null;
                selectedAssembledModel = null;
            }
        }

        private void RefreshPagination()
        {
            pagination = ListPagination.Create(visibleFiles.Count, pageSize, pageIndex);
            pageIndex = pagination.PageIndex;
            pageFiles = pagination.Slice(visibleFiles);
        }

        private void SelectFile(GameDataFile file)
        {
            selectedFile = file;
            selectedGfx = null;
            selectedEcm = null;
            selectedSmd = null;
            selectedBon = null;
            selectedSki = null;
            selectedStck = null;
            selectedTerrain = null;
            selectedEcwld = null;
            selectedEcbsd = null;
            selectedWorldStatus = null;
            selectedBuilding = null;
            selectedAssembledBuilding = null;
            selectedAssembledModel = null;
            status = "Selected " + file.RelativePath;
            if (file.Extension == "gfx")
            {
                try
                {
                    selectedGfx = new GfxParser(GetGameDataDecoder())
                        .Parse(File.ReadAllBytes(file.FullPath));
                }
                catch (InvalidOperationException exception)
                {
                    status = exception.Message;
                }
                catch (NotSupportedException exception)
                {
                    status = exception.Message;
                }
                catch (UnauthorizedAccessException exception)
                {
                    status = "Access denied while reading file: " + exception.Message;
                }
                catch (IOException exception)
                {
                    status = "Could not read file: " + exception.Message;
                }
                catch (SecurityException exception)
                {
                    status = "Permission denied while reading file: " + exception.Message;
                }
            }
            else if (file.Extension == "ecm")
            {
                try
                {
                    selectedEcm = new EcmParser(GetGameDataDecoder())
                        .Parse(File.ReadAllBytes(file.FullPath));
                    if (!string.IsNullOrEmpty(selectedEcm.Error))
                        status = selectedEcm.Error;
                    else
                        selectedAssembledModel = TryAssembleSelectedEcm(file, selectedEcm);
                }
                catch (InvalidOperationException exception)
                {
                    status = exception.Message;
                }
                catch (NotSupportedException exception)
                {
                    status = exception.Message;
                }
                catch (UnauthorizedAccessException exception)
                {
                    status = "Access denied while reading file: " + exception.Message;
                }
                catch (IOException exception)
                {
                    status = "Could not read file: " + exception.Message;
                }
                catch (SecurityException exception)
                {
                    status = "Permission denied while reading file: " + exception.Message;
                }
            }
            else if (file.Extension == "smd")
            {
                try
                {
                    selectedSmd = new SmdParser(GetGameDataDecoder())
                        .Parse(File.ReadAllBytes(file.FullPath), file.RelativePath);
                    selectedAssembledModel = TryAssembleSelectedSmd(file, selectedSmd);
                }
                catch (InvalidOperationException exception)
                {
                    status = exception.Message;
                }
                catch (NotSupportedException exception)
                {
                    status = exception.Message;
                }
                catch (InvalidDataException exception)
                {
                    status = exception.Message;
                }
                catch (UnauthorizedAccessException exception)
                {
                    status = "Access denied while reading file: " + exception.Message;
                }
                catch (IOException exception)
                {
                    status = "Could not read file: " + exception.Message;
                }
                catch (SecurityException exception)
                {
                    status = "Permission denied while reading file: " + exception.Message;
                }
            }
            else if (file.Extension == "bon")
            {
                try
                {
                    selectedBon = new BonParser(GetGameDataDecoder())
                        .Parse(File.ReadAllBytes(file.FullPath), file.RelativePath);
                }
                catch (InvalidOperationException exception)
                {
                    status = exception.Message;
                }
                catch (NotSupportedException exception)
                {
                    status = exception.Message;
                }
                catch (InvalidDataException exception)
                {
                    status = exception.Message;
                }
                catch (UnauthorizedAccessException exception)
                {
                    status = "Access denied while reading file: " + exception.Message;
                }
                catch (IOException exception)
                {
                    status = "Could not read file: " + exception.Message;
                }
                catch (SecurityException exception)
                {
                    status = "Permission denied while reading file: " + exception.Message;
                }
            }
            else if (file.Extension == "ski")
            {
                try
                {
                    selectedSki = new SkiParser(GetGameDataDecoder())
                        .Parse(File.ReadAllBytes(file.FullPath), file.RelativePath);
                }
                catch (InvalidOperationException exception)
                {
                    status = exception.Message;
                }
                catch (NotSupportedException exception)
                {
                    status = exception.Message;
                }
                catch (InvalidDataException exception)
                {
                    status = exception.Message;
                }
                catch (UnauthorizedAccessException exception)
                {
                    status = "Access denied while reading file: " + exception.Message;
                }
                catch (IOException exception)
                {
                    status = "Could not read file: " + exception.Message;
                }
                catch (SecurityException exception)
                {
                    status = "Permission denied while reading file: " + exception.Message;
                }
            }
            else if (file.Extension == "stck")
            {
                try
                {
                    selectedStck = new StckParser()
                        .Parse(File.ReadAllBytes(file.FullPath), file.RelativePath);
                }
                catch (InvalidOperationException exception)
                {
                    status = exception.Message;
                }
                catch (NotSupportedException exception)
                {
                    status = exception.Message;
                }
                catch (InvalidDataException exception)
                {
                    status = exception.Message;
                }
                catch (UnauthorizedAccessException exception)
                {
                    status = "Access denied while reading file: " + exception.Message;
                }
                catch (IOException exception)
                {
                    status = "Could not read file: " + exception.Message;
                }
                catch (SecurityException exception)
                {
                    status = "Permission denied while reading file: " + exception.Message;
                }
            }
            else if (file.Extension == "trn2")
            {
                try
                {
                    selectedTerrain = new TerrainParser(GetGameDataDecoder())
                        .Parse(File.ReadAllBytes(file.FullPath), file.RelativePath);
                }
                catch (InvalidOperationException exception)
                {
                    status = exception.Message;
                }
                catch (NotSupportedException exception)
                {
                    status = exception.Message;
                }
                catch (InvalidDataException exception)
                {
                    status = exception.Message;
                }
                catch (UnauthorizedAccessException exception)
                {
                    status = "Access denied while reading file: " + exception.Message;
                }
                catch (IOException exception)
                {
                    status = "Could not read file: " + exception.Message;
                }
                catch (SecurityException exception)
                {
                    status = "Permission denied while reading file: " + exception.Message;
                }
            }

            else if (file.Extension == "ecwld")
            {
                try
                {
                    selectedEcwld = new EcwldParser(GetGameDataDecoder())
                        .Parse(File.ReadAllBytes(file.FullPath), file.RelativePath);
                    selectedWorldStatus = null;
                    var dir = (Path.GetDirectoryName(file.RelativePath) ?? "").Replace('\\', '/');
                    var bsdRel = string.IsNullOrEmpty(dir)
                        ? file.Name + ".ecbsd"
                        : dir + "/" + file.Name + ".ecbsd";
                    if (index != null && index.TryResolve(bsdRel, out var bsd, out _))
                    {
                        selectedEcbsd = new EcbsdParser(GetGameDataDecoder()).Parse(
                            File.ReadAllBytes(bsd.FullPath),
                            bsd.RelativePath,
                            selectedEcwld.OrnamentRefs,
                            selectedEcwld.BoxRefs,
                            selectedEcwld.WaterRefs,
                            name =>
                            {
                                var bsdDir = Path.GetDirectoryName(bsd.FullPath) ?? "";
                                var full = Path.Combine(bsdDir, name.Replace('\\', Path.DirectorySeparatorChar));
                                if (!File.Exists(full))
                                    throw new FileNotFoundException(name);
                                return File.ReadAllBytes(full);
                            });
                    }
                    else
                        selectedWorldStatus = file.Name + ".ecbsd missing beside .ecwld.";
                }
                catch (InvalidDataException exception)
                {
                    status = exception.Message;
                }
                catch (IOException exception)
                {
                    status = "Could not read file: " + exception.Message;
                }
            }
            else if (file.Extension == "bmd")
            {
                try
                {
                    selectedBuilding = new BmdParser(GetGameDataDecoder())
                        .Parse(File.ReadAllBytes(file.FullPath), file.RelativePath);
                    selectedAssembledBuilding = BmdMeshAssembler.Assemble(selectedBuilding);
                }
                catch (InvalidOperationException exception)
                {
                    status = exception.Message;
                }
                catch (NotSupportedException exception)
                {
                    status = exception.Message;
                }
                catch (InvalidDataException exception)
                {
                    status = exception.Message;
                }
                catch (UnauthorizedAccessException exception)
                {
                    status = "Access denied while reading file: " + exception.Message;
                }
                catch (IOException exception)
                {
                    status = "Could not read file: " + exception.Message;
                }
                catch (SecurityException exception)
                {
                    status = "Permission denied while reading file: " + exception.Message;
                }
            }

            Repaint();
        }

        private static GfxTextDecoder GetGameDataDecoder()
        {
            if (gameDataDecoder != null)
                return gameDataDecoder;

            var package = PackageInfo.FindForAssembly(typeof(GameDataBrowserWindow).Assembly);
            if (package == null)
                throw new InvalidOperationException(
                    "Could not locate the Forsaken World Unity package to load its GBK table.");

            var tablePath = Path.Combine(
                package.resolvedPath,
                "Runtime",
                "Data",
                "gbk_table.bin");
            if (!File.Exists(tablePath))
                throw new InvalidOperationException(
                    "The bundled GBK table is missing: " + tablePath);

            gameDataDecoder = new GfxTextDecoder(gbkTable: File.ReadAllBytes(tablePath));
            return gameDataDecoder;
        }

        private bool CanImportSelectedFile()
        {
            return selectedFile != null &&
                ((selectedFile.Extension == "gfx" && selectedGfx != null) ||
                    (selectedFile.Extension == "ecm" &&
                        selectedEcm != null &&
                        string.IsNullOrEmpty(selectedEcm.Error)) ||
                    (selectedFile.Extension == "smd" && selectedSmd != null) ||
                    (selectedFile.Extension == "bon" && selectedBon != null) ||
                    (selectedFile.Extension == "ski" && selectedSki != null) ||
                    (selectedFile.Extension == "stck" && selectedStck != null));
        }

        private void ImportSelectedAsset()
        {
            if (!CanImportSelectedFile())
                return;

            UnityEngine.Object newAsset = null;
            try
            {
                var assetType = GetAssetType(selectedFile.Extension);
                var outputPath = GetAssetPath(selectedFile);
                var existing = AssetDatabase.LoadMainAssetAtPath(outputPath);
                newAsset = CreateSelectedAsset();

                if (existing != null)
                {
                    if (existing.GetType() != assetType)
                        throw new IOException(
                            "An incompatible asset already exists at the output path: " +
                            outputPath);
                    EditorUtility.CopySerialized(newAsset, existing);
                    EditorUtility.SetDirty(existing);
                    DestroyImmediate(newAsset);
                    newAsset = null;
                    status = "Updated " + outputPath;
                }
                else
                {
                    if (File.Exists(outputPath))
                        throw new IOException(
                            "A file already exists at the output path but is not a Unity asset: " +
                            outputPath);

                    EnsureAssetFolders(Path.GetDirectoryName(outputPath));
                    AssetDatabase.CreateAsset(newAsset, outputPath);
                    newAsset = null;
                    status = "Imported " + outputPath;
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(outputPath);
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is ArgumentException ||
                exception is InvalidOperationException ||
                exception is UnityException)
            {
                status = "Asset import failed: " + exception.Message;
                Debug.LogError(status);
            }
            finally
            {
                if (newAsset != null)
                    DestroyImmediate(newAsset);
            }
        }

        private Type GetAssetType(string extension)
        {
            switch (extension)
            {
                case "gfx":
                    return typeof(GfxAsset);
                case "ecm":
                    return typeof(EcmAsset);
                case "smd":
                    return typeof(SmdAsset);
                case "bon":
                    return typeof(BonAsset);
                case "ski":
                    return typeof(SkiAsset);
                case "stck":
                    return typeof(StckAsset);
                default:
                    throw new InvalidOperationException(
                        "No importer is available for ." + extension + " files.");
            }
        }

        private UnityEngine.Object CreateSelectedAsset()
        {
            switch (selectedFile.Extension)
            {
                case "gfx":
                    return GfxAsset.CreateFrom(selectedFile.RelativePath, selectedGfx);
                case "ecm":
                    return EcmAsset.CreateFrom(selectedFile.RelativePath, selectedEcm);
                case "smd":
                    return SmdAsset.CreateFrom(selectedFile.RelativePath, selectedSmd);
                case "bon":
                    return BonAsset.CreateFrom(selectedFile.RelativePath, selectedBon);
                case "ski":
                    return SkiAsset.CreateFrom(selectedFile.RelativePath, selectedSki);
                case "stck":
                    return StckAsset.CreateFrom(selectedFile.RelativePath, selectedStck);
                default:
                    throw new InvalidOperationException(
                        "No importer is available for ." + selectedFile.Extension + " files.");
            }
        }


        private void DrawTerrainDetails(TerrainDocument terrain)
        {
            EditorGUILayout.LabelField("Terrain (TRN2 Loader B)", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Size: " + terrain.Width.ToString("0.###") + " x " + terrain.Length.ToString("0.###") +
                "  grid=" + terrain.GridSize.ToString("0.###"));
            EditorGUILayout.LabelField(
                "Blocks: " + terrain.BlockRows + "x" + terrain.BlockColumns +
                " (grid " + terrain.BlockGrid + ", count " + terrain.BlockCount + ")");
            EditorGUILayout.LabelField(
                "Mask areas: " + terrain.MaskAreaCount +
                " (mask blocks " + terrain.MaskBlock + ", mask grid " + terrain.MaskGrid + ")");
            EditorGUILayout.LabelField(
                "Subs: " + terrain.SubRows + "x" + terrain.SubColumns +
                " (sub grid " + terrain.SubGrid + ")");
            EditorGUILayout.LabelField("Textures: " + terrain.Textures.Length);
            foreach (var texture in terrain.Textures)
                EditorGUILayout.LabelField("  " + texture, EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Subterrains: " + terrain.Subs.Length);
            foreach (var sub in terrain.Subs)
            {
                EditorGUILayout.LabelField(
                    "  " + sub.Name +
                    " height=[" + sub.MinHeight.ToString("0.###") + ", " +
                    sub.MaxHeight.ToString("0.###") + "]",
                    EditorStyles.miniLabel);
            }

            EditorGUILayout.HelpBox(
                "Import terrain Prefab writes Mesh/Material/Lightmap assets under " +
                "Assets/ForsakenWorld/Imported/Terrain. Import map Prefab also places " +
                ".ecbsd-listed buildings, water, lights, ECModels, sounds, and GFX placeholders " +
                "under Assets/ForsakenWorld/Imported/Maps (Godot import_map parity).",
                MessageType.Info);
        }

        private void DrawBuildingDetails(BmdDocument building, AssembledBuilding assembled)
        {
            EditorGUILayout.LabelField("Building (BMD litmodel)", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "LitModel: 0x" + building.LitModelVersion.ToString("X8") +
                (building.CollideOnly ? "  collide-only" : string.Empty));
            EditorGUILayout.LabelField(
                "Pos: (" +
                building.Pos[0].ToString("0.###") + ", " +
                building.Pos[1].ToString("0.###") + ", " +
                building.Pos[2].ToString("0.###") + ")");
            EditorGUILayout.LabelField(
                "Scale: (" +
                building.Scale[0].ToString("0.###") + ", " +
                building.Scale[1].ToString("0.###") + ", " +
                building.Scale[2].ToString("0.###") + ")");
            EditorGUILayout.LabelField("Meshes: " + building.Meshes.Length);
            foreach (var mesh in building.Meshes)
            {
                EditorGUILayout.LabelField(
                    "  " + mesh.Name +
                    " ver=0x" + mesh.Version.ToString("X8") +
                    " verts=" + (mesh.Positions.Length / 3) +
                    " tris=" + (mesh.Indices.Length / 3) +
                    " tex=" + mesh.Texture +
                    (mesh.TwoSided ? " two-sided" : string.Empty),
                    EditorStyles.miniLabel);
            }

            if (assembled != null)
            {
                EditorGUILayout.LabelField(
                    "Assemble: " + assembled.Surfaces.Length + " surfaces" +
                    (assembled.CollideOnly ? " (skipped collide-only)" : string.Empty));
                foreach (var warning in assembled.Warnings)
                    EditorGUILayout.HelpBox(warning, MessageType.Warning);
            }

            EditorGUILayout.HelpBox(
                "Import builds a Mesh, Angelica LitModel materials (vertex day color × texture × 2), " +
                "and a Prefab under Assets/ForsakenWorld/Imported/Buildings with the building's " +
                "world transform. Map import filters buildings via .ecwld/.ecbsd ornament lists.",
                MessageType.Info);
        }

        private void ImportSelectedBuilding()
        {
            if (selectedFile == null ||
                selectedFile.Extension != "bmd" ||
                selectedBuilding == null ||
                selectedAssembledBuilding == null ||
                index == null)
            {
                return;
            }

            try
            {
                var result = BuildingAssetImporter.Import(
                    selectedFile,
                    index,
                    GetGameDataDecoder(),
                    showProgress: true);
                if (result.Skipped)
                {
                    status = "Skipped collide-only building " + selectedFile.RelativePath;
                    return;
                }

                status = "Imported building prefab " + result.PrefabPath +
                    " (" + result.Surfaces + " surfaces, " +
                    result.Vertices + " verts, " +
                    result.Triangles + " tris" +
                    (result.Warnings.Count > 0
                        ? ", " + result.Warnings.Count + " warnings"
                        : string.Empty) +
                    ")";
                foreach (var warning in result.Warnings)
                    Debug.LogWarning(warning);
                AssetDatabase.Refresh();
                Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(result.PrefabPath);
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is ArgumentException ||
                exception is InvalidOperationException ||
                exception is InvalidDataException ||
                exception is NotSupportedException ||
                exception is OperationCanceledException ||
                exception is UnityException)
            {
                status = "Building import failed: " + exception.Message;
                Debug.LogError(status);
            }
        }

        private void DrawAssembledModelDetails(AssembledModel model)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Assemble preview", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Bones: " + model.Bones.Count);
            EditorGUILayout.LabelField("Hooks: " + model.HookBones.Count);
            EditorGUILayout.LabelField("Skins: " + model.Skins.Count);
            EditorGUILayout.LabelField("Actions: " + model.Actions.Count);
            EditorGUILayout.LabelField("Animations: " + model.Animations.Count);
            var materialSlots = 0;
            var texturesResolved = 0;
            foreach (var skin in model.Skins)
            {
                materialSlots += skin.SurfaceMaterials.Count;
                foreach (var slot in skin.SurfaceMaterials)
                {
                    if (slot.TextureResolved)
                        texturesResolved++;
                }
            }

            EditorGUILayout.LabelField(
                "Materials: " + materialSlots + " (textures resolved " + texturesResolved + ")");
            EditorGUILayout.LabelField(
                "Status: " + (model.IsValid ? "OK" : "Has errors"),
                EditorStyles.miniBoldLabel);

            foreach (var skin in model.Skins)
            {
                EditorGUILayout.LabelField(
                    skin.SourcePath + " (" + skin.Surfaces.Count + " surfaces, " +
                    skin.BindBones.Count + " bind bones, " +
                    skin.SurfaceMaterials.Count + " materials)",
                    EditorStyles.miniLabel);
                foreach (var slot in skin.SurfaceMaterials)
                {
                    var tex = slot.TextureResolved
                        ? slot.TextureSourcePath
                        : (string.IsNullOrEmpty(slot.TextureError) ? "missing" : slot.TextureError);
                    var dds = slot.TextureDds != null
                        ? (" [" + slot.TextureDds.Compression + " " +
                           slot.TextureDds.Width + "x" + slot.TextureDds.Height + "]")
                        : string.Empty;
                    EditorGUILayout.LabelField(
                        "  " + slot.Name + " tex=" + (slot.TextureName ?? "") +
                        " alpha=" + slot.AlphaMode +
                        (slot.TwoSided ? " two-sided" : "") +
                        " -> " + tex + dds,
                        EditorStyles.miniLabel);
                }
            }

            foreach (var animation in model.Animations)
            {
                EditorGUILayout.LabelField(
                    animation.Name + " (" + animation.Bones.Count + " bones, " +
                    animation.LengthSeconds.ToString("0.###") + "s, " +
                    animation.SampleFramesPerSecond + " fps) from " + animation.TrackSourcePath,
                    EditorStyles.miniLabel);
            }

            foreach (var warning in model.Warnings)
                EditorGUILayout.HelpBox(warning, MessageType.Warning);
            foreach (var error in model.Errors)
                EditorGUILayout.HelpBox(error, MessageType.Error);
        }


        private void DrawWorldDetailsBesideTerrain()
        {
            if (selectedFile == null || selectedFile.Extension != "trn2")
                return;
            EnsureWorldSelectionLoaded();
            DrawWorldDetails(selectedEcwld, selectedEcbsd, selectedWorldStatus);
        }

        private void EnsureWorldSelectionLoaded()
        {
            if (selectedFile == null || index == null)
                return;
            if (selectedEcwld != null || selectedWorldStatus != null)
                return;
            try
            {
                var mapName = selectedFile.Name;
                var dir = (Path.GetDirectoryName(selectedFile.RelativePath) ?? "").Replace('\\', '/');
                var wldRel = string.IsNullOrEmpty(dir) ? mapName + ".ecwld" : dir + "/" + mapName + ".ecwld";
                GameDataFile wld;
                if (!index.TryResolve(wldRel, out wld, out _))
                {
                    selectedWorldStatus = mapName + ".ecwld not found beside terrain.";
                    return;
                }

                selectedEcwld = new EcwldParser(GetGameDataDecoder()).Parse(
                    File.ReadAllBytes(wld.FullPath), wld.RelativePath);
                var bsdRel = string.IsNullOrEmpty(dir) ? mapName + ".ecbsd" : dir + "/" + mapName + ".ecbsd";
                GameDataFile bsd;
                if (index.TryResolve(bsdRel, out bsd, out _))
                {
                    selectedEcbsd = new EcbsdParser(GetGameDataDecoder()).Parse(
                        File.ReadAllBytes(bsd.FullPath),
                        bsd.RelativePath,
                        selectedEcwld.OrnamentRefs,
                        selectedEcwld.BoxRefs,
                        selectedEcwld.WaterRefs,
                        name =>
                        {
                            var bsdDir = Path.GetDirectoryName(bsd.FullPath) ?? "";
                            var full = Path.Combine(bsdDir, name.Replace('\\', Path.DirectorySeparatorChar));
                            if (!File.Exists(full))
                                throw new FileNotFoundException(name);
                            return File.ReadAllBytes(full);
                        });
                }
                else
                    selectedWorldStatus = mapName + ".ecbsd missing; water/light unavailable, buildings unfiltered.";
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is InvalidDataException ||
                exception is UnauthorizedAccessException ||
                exception is ArgumentException)
            {
                selectedWorldStatus = exception.Message;
            }
        }

        private void DrawWorldDetails(EcwldDocument world, EcbsdDocument bsd, string statusMessage)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("World (.ecwld / .ecbsd)", EditorStyles.boldLabel);
            if (!string.IsNullOrEmpty(statusMessage))
                EditorGUILayout.HelpBox(statusMessage, MessageType.Warning);
            if (world == null)
                return;
            EditorGUILayout.LabelField("Version: " + world.Version);
            EditorGUILayout.LabelField(
                "Effects: " + world.Effects.Count +
                "  ECModels: " + world.Models.Count +
                "  Sounds: " + world.Sounds.Count);
            EditorGUILayout.LabelField(
                "Water refs: " + world.WaterRefs.Count +
                "  Ornaments: " + world.OrnamentRefs.Count +
                "  Boxes: " + world.BoxRefs.Count);
            if (bsd != null)
            {
                EditorGUILayout.LabelField(
                    "BSD ornaments: " + bsd.Ornaments.Count +
                    "  water areas: " + bsd.WaterAreas.Count +
                    (bsd.DefaultLight != null ? "  light: yes" : "  light: no"));
                foreach (var warning in bsd.Warnings)
                    EditorGUILayout.HelpBox(warning, MessageType.Warning);
            }

            EditorGUILayout.HelpBox(
                "Import map Prefab builds terrain + filtered buildings + water/light/ECModels/sounds " +
                "and GFX placeholders (GFX runtime TODO) under Assets/ForsakenWorld/Imported/Maps.",
                MessageType.Info);
        }

        private void ImportSelectedMap()
        {
            if (selectedFile == null ||
                selectedFile.Extension != "trn2" ||
                selectedTerrain == null ||
                index == null)
            {
                return;
            }

            try
            {
                var result = MapAssetImporter.Import(
                    selectedFile,
                    index,
                    GetGameDataDecoder(),
                    showProgress: true);
                status = "Imported map prefab " + result.PrefabPath +
                    " (terrain areas=" + result.TerrainAreas +
                    ", buildings=" + result.Buildings +
                    ", unused_bmd=" + result.UnusedBuildings +
                    ", water=" + result.Water +
                    ", effects=" + result.Effects +
                    ", models=" + result.Models +
                    ", sounds=" + result.Sounds +
                    (result.HasLight ? ", light" : "") +
                    (result.Warnings.Count > 0
                        ? ", " + result.Warnings.Count + " warnings"
                        : string.Empty) +
                    ")";
                foreach (var warning in result.Warnings)
                    Debug.LogWarning(warning);
                AssetDatabase.Refresh();
                Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(result.PrefabPath);
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is ArgumentException ||
                exception is InvalidOperationException ||
                exception is InvalidDataException ||
                exception is NotSupportedException ||
                exception is OperationCanceledException ||
                exception is UnityException)
            {
                status = "Map import failed: " + exception.Message;
                Debug.LogError(status);
            }
        }

        private void ImportSelectedTerrain()
        {
            if (selectedFile == null ||
                selectedFile.Extension != "trn2" ||
                selectedTerrain == null ||
                index == null)
            {
                return;
            }

            try
            {
                var result = TerrainAssetImporter.Import(
                    selectedFile,
                    index,
                    GetGameDataDecoder(),
                    showProgress: true);
                status = "Imported terrain prefab " + result.PrefabPath +
                    " (" + result.Areas + " areas, " +
                    result.HiddenAreas + " hidden, " +
                    result.Vertices + " verts, " +
                    result.Triangles + " tris" +
                    (result.Warnings.Count > 0
                        ? ", " + result.Warnings.Count + " warnings"
                        : string.Empty) +
                    ")";
                foreach (var warning in result.Warnings)
                    Debug.LogWarning(warning);
                AssetDatabase.Refresh();
                Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(result.PrefabPath);
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is ArgumentException ||
                exception is InvalidOperationException ||
                exception is InvalidDataException ||
                exception is NotSupportedException ||
                exception is OperationCanceledException ||
                exception is UnityException)
            {
                status = "Terrain import failed: " + exception.Message;
                Debug.LogError(status);
            }
        }

        private void ImportAssembledModelPrefab()
        {
            if (selectedAssembledModel == null ||
                !selectedAssembledModel.IsValid ||
                selectedFile == null)
            {
                return;
            }

            try
            {
                var fromEcm = selectedFile.Extension == "ecm";
                var result = AssembledModelAssetExporter.Export(
                    selectedAssembledModel,
                    index,
                    selectedFile.RelativePath,
                    fromEcm,
                    ecmDocument: fromEcm ? selectedEcm : null);
                if (!result.Succeeded)
                {
                    status = "Prefab export failed: " + (result.Error ?? "unknown error");
                    Debug.LogError(status);
                    return;
                }

                status = "Prefab " +
                    (result.PrefabCreated ? "created" : "updated") +
                    " at " + result.PrefabPath +
                    "; meshes +" + result.MeshesCreated + "/~" + result.MeshesUpdated +
                    ", clips +" + result.ClipsCreated + "/~" + result.ClipsUpdated +
                    ", materials +" + result.MaterialsCreated + "/~" + result.MaterialsUpdated +
                    ", textures +" + result.TexturesCreated + "/~" + result.TexturesUpdated +
                    " missing=" + result.TexturesMissing;
                foreach (var warning in result.Warnings)
                    Debug.LogWarning("Forsaken prefab: " + warning);
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is ArgumentException ||
                exception is InvalidOperationException ||
                exception is UnityException)
            {
                status = "Prefab export failed: " + exception.Message;
                Debug.LogError(status);
            }
        }

        private AssembledModel TryAssembleSelectedEcm(GameDataFile file, EcmDocument ecm)
        {
            if (index == null || file == null || ecm == null)
                return null;

            try
            {
                var ecmResolver = new EcmModelResolver(index);
                var preparation = ecmResolver.Prepare(ecm, file.RelativePath);
                if (preparation.HasBlockingErrors)
                {
                    var incomplete = new AssembledModel
                    {
                        SourceSmdPath = preparation.SkinModel != null && preparation.SkinModel.IsResolved
                            ? preparation.SkinModel.File.RelativePath
                            : string.Empty
                    };
                    incomplete.Errors.AddRange(preparation.Errors);
                    incomplete.Warnings.AddRange(preparation.Warnings);
                    return incomplete;
                }

                var smdFile = preparation.SkinModel.File;
                var decoder = GetGameDataDecoder();
                var smd = new SmdParser(decoder).Parse(
                    File.ReadAllBytes(smdFile.FullPath),
                    smdFile.RelativePath);
                selectedSmd = smd;

                var addiRequested = new List<string>();
                foreach (var addi in preparation.AdditionalSkins)
                    addiRequested.Add(addi.RequestedPath);
                // Prefer original ECM AddiSkinPath strings for Godot-parity Models/ prefixing
                if (ecm.AdditionalSkinPaths.Count > 0)
                    addiRequested = new List<string>(ecm.AdditionalSkinPaths);

                var resolution = ecmResolver.ResolveSmdWithAdditionalSkins(
                    smd,
                    smdFile.RelativePath,
                    addiRequested,
                    preparation.Warnings);
                return AssembleFromResolution(smdFile, smd, resolution, decoder);
            }
            catch (Exception exception)
            {
                var failed = new AssembledModel();
                failed.Errors.Add("ECM assemble failed: " + exception.Message);
                return failed;
            }
        }

        private AssembledModel AssembleFromResolution(
            GameDataFile smdFile,
            SmdDocument smd,
            ModelResourceResolution resolution,
            GfxTextDecoder decoder)
        {
            if (resolution.Skeleton == null || !resolution.Skeleton.IsResolved)
            {
                var incomplete = new AssembledModel
                {
                    SourceSmdPath = smdFile.RelativePath
                };
                incomplete.Errors.AddRange(resolution.Errors);
                incomplete.Warnings.AddRange(resolution.Warnings);
                return incomplete;
            }

            var skeletonBytes = File.ReadAllBytes(resolution.Skeleton.File.FullPath);
            var skeleton = new BonParser(decoder).Parse(
                skeletonBytes,
                resolution.Skeleton.File.RelativePath);

            var skins = new List<SkiDocument>();
            var skinPaths = new List<string>();
            foreach (var skinRef in resolution.Skins)
            {
                if (!skinRef.IsResolved)
                    continue;
                var skinBytes = File.ReadAllBytes(skinRef.File.FullPath);
                skins.Add(new SkiParser(decoder).Parse(skinBytes, skinRef.File.RelativePath));
                skinPaths.Add(skinRef.File.RelativePath);
            }

            var model = new ModelAssembler().Assemble(
                smd,
                smdFile.RelativePath,
                skeleton,
                resolution.Skeleton.File.RelativePath,
                skins,
                skinPaths,
                resolution);
            AttachAssembledAnimations(model, resolution, decoder);
            AttachAssembledMaterials(model);
            return model;
        }

        private AssembledModel TryAssembleSelectedSmd(GameDataFile file, SmdDocument smd)
        {
            if (index == null || file == null || smd == null)
                return null;

            try
            {
                var resolution = new ModelResourceResolver(index).Resolve(smd, file.RelativePath);
                return AssembleFromResolution(file, smd, resolution, GetGameDataDecoder());
            }
            catch (System.Exception exception)
            {
                var failed = new AssembledModel
                {
                    SourceSmdPath = file.RelativePath
                };
                failed.Errors.Add("Assemble failed: " + exception.Message);
                return failed;
            }
        }

        private static void AttachAssembledAnimations(
            AssembledModel model,
            ModelResourceResolution resolution,
            GfxTextDecoder decoder)
        {
            if (model == null || resolution == null || resolution.ActionTracks.Count == 0)
                return;

            var sources = new System.Collections.Generic.List<ActionAnimationSource>();
            foreach (var action in resolution.ActionTracks)
            {
                if (action == null)
                    continue;

                var source = new ActionAnimationSource
                {
                    ActionName = action.ActionName,
                    StartFrame = action.StartFrame,
                    EndFrame = action.EndFrame,
                    TrackSourcePath = action.Track != null && action.Track.IsResolved
                        ? action.Track.File.RelativePath
                        : (action.Track != null ? action.Track.RequestedPath : string.Empty)
                };

                if (action.Track != null && action.Track.IsResolved)
                {
                    try
                    {
                        var bytes = File.ReadAllBytes(action.Track.File.FullPath);
                        source.Track = new StckParser().Parse(
                            bytes,
                            action.Track.File.RelativePath);
                    }
                    catch (System.Exception exception)
                    {
                        model.Warnings.Add(
                            "Action " + action.ActionName + ": failed to parse STCK (" +
                            action.Track.File.RelativePath + "): " + exception.Message);
                    }
                }

                sources.Add(source);
            }

            new ModelAnimationAssembler().Build(model, sources);
        }

        private void ImportAssembledAnimationClips()
        {
            if (selectedAssembledModel == null ||
                !selectedAssembledModel.IsValid ||
                selectedAssembledModel.Animations.Count == 0 ||
                selectedFile == null)
            {
                return;
            }

            try
            {
                var clips = AssembledAnimationClipBuilder.CreateClips(
                    selectedAssembledModel.Animations);
                if (clips.Count == 0)
                {
                    status = "No AnimationClips were produced from the assembled model.";
                    return;
                }

                var imported = 0;
                var updated = 0;
                foreach (var clip in clips)
                {
                    if (clip == null)
                        continue;
                    var outputPath = GetAnimationClipAssetPath(selectedFile, clip.name);
                    var existing = AssetDatabase.LoadMainAssetAtPath(outputPath);
                    if (existing != null)
                    {
                        if (!(existing is AnimationClip))
                            throw new IOException(
                                "An incompatible asset already exists at the output path: " +
                                outputPath);
                        EditorUtility.CopySerialized(clip, existing);
                        EditorUtility.SetDirty(existing);
                        DestroyImmediate(clip);
                        updated++;
                    }
                    else
                    {
                        if (File.Exists(outputPath))
                            throw new IOException(
                                "A file already exists at the output path but is not a Unity asset: " +
                                outputPath);
                        EnsureAssetFolders(Path.GetDirectoryName(outputPath));
                        AssetDatabase.CreateAsset(clip, outputPath);
                        imported++;
                    }
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                status = "AnimationClips imported=" + imported + " updated=" + updated +
                    " under " + ImportRoot + "/Animations";
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is ArgumentException ||
                exception is InvalidOperationException ||
                exception is UnityException)
            {
                status = "AnimationClip import failed: " + exception.Message;
                Debug.LogError(status);
            }
        }

        private static string GetAnimationClipAssetPath(GameDataFile file, string clipName)
        {
            var relativePath = file.RelativePath.Replace('\\', '/');
            var pathParts = relativePath.Split('/');
            for (var i = 0; i < pathParts.Length; i++)
                pathParts[i] = SanitizeAssetName(pathParts[i]);
            var folder = ImportRoot + "/Animations/" +
                Path.ChangeExtension(string.Join("/", pathParts), null).Replace('\\', '/');
            return folder + "/" + SanitizeAssetName(clipName) + ".anim";
        }

        private void AttachAssembledMaterials(AssembledModel model)
        {
            if (model == null || index == null)
                return;
            new ModelMaterialAssembler(new SkiTextureResolver(index)).BuildForModel(model);
        }

        private static bool HasAssembledMaterials(AssembledModel model)
        {
            if (model == null)
                return false;
            foreach (var skin in model.Skins)
            {
                if (skin.SurfaceMaterials.Count > 0)
                    return true;
            }

            return false;
        }

        private void ImportAssembledMaterials()
        {
            if (selectedAssembledModel == null ||
                !selectedAssembledModel.IsValid ||
                !HasAssembledMaterials(selectedAssembledModel))
            {
                return;
            }

            try
            {
                int texturesCreated;
                int texturesUpdated;
                int texturesMissing;
                List<string> textureErrors;
                var textures = AssembledTextureImporter.ImportTexturesForModel(
                    selectedAssembledModel,
                    index,
                    out texturesCreated,
                    out texturesUpdated,
                    out texturesMissing,
                    out textureErrors);

                var imported = 0;
                var updated = 0;
                foreach (var skin in selectedAssembledModel.Skins)
                {
                    foreach (var slot in skin.SurfaceMaterials)
                    {
                        Texture2D albedo = null;
                        if (slot.TextureResolved &&
                            !string.IsNullOrWhiteSpace(slot.TextureSourcePath))
                        {
                            textures.TryGetValue(
                                slot.TextureSourcePath.Replace('\\', '/'),
                                out albedo);
                        }

                        var reflect = slot.UsesReflectShader
                            ? AssembledUrpMaterialBuilder.FindReflectMap(textures)
                            : null;
                        var material = AssembledUrpMaterialBuilder.CreateMaterial(slot, albedo, reflect);
                        var outputPath = AssembledUrpMaterialBuilder.GetMaterialAssetPath(
                            skin.SourcePath,
                            slot);
                        var existing = AssetDatabase.LoadMainAssetAtPath(outputPath);
                        if (existing != null)
                        {
                            if (!(existing is Material))
                                throw new IOException(
                                    "An incompatible asset already exists at the output path: " +
                                    outputPath);
                            EditorUtility.CopySerialized(material, existing);
                            EditorUtility.SetDirty(existing);
                            DestroyImmediate(material);
                            updated++;
                        }
                        else
                        {
                            if (File.Exists(outputPath))
                                throw new IOException(
                                    "A file already exists at the output path but is not a Unity asset: " +
                                    outputPath);
                            EnsureAssetFolders(Path.GetDirectoryName(outputPath));
                            AssetDatabase.CreateAsset(material, outputPath);
                            imported++;
                        }
                    }
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                status = "URP materials imported=" + imported + " updated=" + updated +
                    "; textures created=" + texturesCreated + " updated=" + texturesUpdated +
                    " missing=" + texturesMissing +
                    " under " + ImportRoot + "/Materials and /Textures";
                if (textureErrors.Count > 0)
                {
                    status += " | texture issues: " + textureErrors.Count;
                    foreach (var error in textureErrors)
                        Debug.LogWarning("Forsaken texture: " + error);
                }
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is ArgumentException ||
                exception is InvalidOperationException ||
                exception is UnityException)
            {
                status = "Material import failed: " + exception.Message;
                Debug.LogError(status);
            }
        }

        private void InstantiateGfxRuntimeInScene(GfxDocument document)
        {
            if (document == null || selectedFile == null)
                return;
            try
            {
                var built = GfxSceneBuilder.Build(
                    document,
                    Path.GetFileNameWithoutExtension(selectedFile.Name),
                    selectedFile.RelativePath,
                    index);
                if (built.Root == null)
                {
                    EditorUtility.DisplayDialog(
                        "Forsaken World",
                        "GFX runtime build returned no root.",
                        "OK");
                    return;
                }

                Undo.RegisterCreatedObjectUndo(built.Root, "Instantiate Forsaken GFX");
                var stubNote = built.StubbedIds.Count > 0
                    ? " Stubbed element IDs: " + string.Join(", ", built.StubbedIds) + "."
                    : "";
                EditorUtility.DisplayDialog(
                    "Forsaken World",
                    "Instantiated GFX runtime root with GfxPlayer. ID 100/101 are sprites; other element IDs may still be stubs." +
                    stubNote,
                    "OK");
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog(
                    "Forsaken World",
                    "GFX instantiate failed: " + e.Message,
                    "OK");
            }
        }

        private void InstantiateAssembledModelInScene(bool withAnimation)
        {
            if (selectedAssembledModel == null || !selectedAssembledModel.IsValid)
                return;

            try
            {
                Func<AssembledSurfaceMaterial, Texture> albedoLookup = null;
                if (index != null && HasAssembledMaterials(selectedAssembledModel))
                {
                    int created;
                    int updated;
                    int missing;
                    List<string> errors;
                    var textures = AssembledTextureImporter.ImportTexturesForModel(
                        selectedAssembledModel,
                        index,
                        out created,
                        out updated,
                        out missing,
                        out errors);
                    albedoLookup = slot =>
                    {
                        if (slot == null ||
                            string.IsNullOrWhiteSpace(slot.TextureSourcePath))
                            return null;
                        Texture2D texture;
                        return textures.TryGetValue(
                            slot.TextureSourcePath.Replace('\\', '/'),
                            out texture)
                            ? texture
                            : null;
                    };
                    if (errors.Count > 0)
                    {
                        foreach (var error in errors)
                            Debug.LogWarning("Forsaken texture: " + error);
                    }
                }

                GameObject root;
                if (withAnimation && selectedAssembledModel.Animations.Count > 0)
                {
                    root = AssembledModelUnityBuilder.BuildHierarchyWithAnimations(
                        selectedAssembledModel,
                        selectedAssembledModel.Animations,
                        null,
                        albedoLookup,
                        previewAnimationClipIndex);
                }
                else
                {
                    root = AssembledModelUnityBuilder.BuildHierarchy(
                        selectedAssembledModel,
                        null,
                        albedoLookup);
                }

                Undo.RegisterCreatedObjectUndo(root, "Instantiate Forsaken Model");
                Selection.activeObject = root;
                status = withAnimation
                    ? "Instantiated assembled model with Animation preview in the open scene."
                    : "Instantiated assembled model in the open scene.";
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is ArgumentException ||
                exception is InvalidOperationException ||
                exception is UnityException)
            {
                status = "Scene instantiate failed: " + exception.Message;
                Debug.LogError(status);
            }
        }

        private static string GetAssetPath(GameDataFile file)
        {
            var relativePath = file.RelativePath.Replace('\\', '/');
            var pathParts = relativePath.Split('/');
            for (var i = 0; i < pathParts.Length; i++)
                pathParts[i] = SanitizeAssetName(pathParts[i]);

            var typeFolder = file.Extension == "gfx"
                ? "GFX"
                : file.Extension == "ecm"
                    ? "ECM"
                    : file.Extension == "bon"
                        ? "Skeletons"
                        : file.Extension == "ski"
                            ? "Skins"
                            : file.Extension == "stck"
                                ? "Tracks"
                                : "Models";
            var outputPath = ImportRoot + "/" + typeFolder + "/" + string.Join("/", pathParts);
            return Path.ChangeExtension(outputPath, ".asset").Replace('\\', '/');
        }

        private static string SanitizeAssetName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var chars = name.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                if (chars[i] == '/' || chars[i] == '\\' || invalid.Contains(chars[i]))
                    chars[i] = '_';
            }

            return new string(chars);
        }

        private static void EnsureAssetFolders(string folderPath)
        {
            AngelicaAssetFolders.Ensure(folderPath);
        }
    }
}
