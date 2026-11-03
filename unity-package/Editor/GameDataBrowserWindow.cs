using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace SilverEcho.ForsakenWorld.Editor
{
    public sealed class GameDataBrowserWindow : EditorWindow
    {
        private const string RootPreference = "SilverEcho.ForsakenWorld.GameDataRoot";
        private const string ImportRoot = "Assets/ForsakenWorld/Imported";
        private const int VisibleResultLimit = 500;
        private static GfxTextDecoder gameDataDecoder;
        private static readonly string[] FileTypes =
            { "All", "ECM", "GFX", "Models", "Skeletons", "Skins", "Tracks", "Maps" };
        private static readonly string[] Extensions =
            { "", "ecm", "gfx", "smd", "bon", "ski", "stck", "trn2" };

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
        private AssembledModel selectedAssembledModel;
        private Task<GameDataIndex> scanTask;
        private int scannedDirectories;
        private int scannedFiles;
        private int lastReportedDirectoryCount;

        [MenuItem("Tools/Forsaken World/Game Data Browser")]
        private static void Open()
        {
            GetWindow<GameDataBrowserWindow>("FW Data");
        }

        private void OnEnable()
        {
            rootPath = EditorPrefs.GetString(RootPreference, string.Empty);
            EditorApplication.update += PollScanTask;
        }

        private void OnDisable()
        {
            EditorApplication.update -= PollScanTask;
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Browse extracted .ecm, .gfx, .smd, .bon, .ski, .stck, and .trn2 files. Supported text " +
                "and model files can be inspected and imported as ScriptableObject assets.",
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
                EditorGUILayout.LabelField(
                    "Files (" + visibleFiles.Count + ")",
                    EditorStyles.boldLabel);
                fileListScroll = EditorGUILayout.BeginScrollView(fileListScroll);
                var displayed = Math.Min(visibleFiles.Count, VisibleResultLimit);
                for (var i = 0; i < displayed; i++)
                {
                    var file = visibleFiles[i];
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

                if (visibleFiles.Count > displayed)
                {
                    EditorGUILayout.HelpBox(
                        "Showing the first " + displayed +
                        " matches. Refine the search to see other files.",
                        MessageType.None);
                }

                EditorGUILayout.EndScrollView();
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
                        selectedAssembledModel == null ||
                        !selectedAssembledModel.IsValid ||
                        selectedAssembledModel.Animations.Count == 0))
                    {
                        if (GUILayout.Button("Import assembled AnimationClips"))
                            ImportAssembledAnimationClips();
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
            selectedAssembledModel = null;
            visibleFiles = Array.Empty<GameDataFile>();
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
                status = "Indexed " + index.Files.Count + " game data files.";
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
                return;
            }

            visibleFiles = index.Search(searchText, Extensions[selectedFileType]);
            if (selectedFile != null && !visibleFiles.Contains(selectedFile))
            {
                selectedFile = null;
                selectedGfx = null;
                selectedEcm = null;
                selectedSmd = null;
                selectedBon = null;
                selectedSki = null;
                selectedStck = null;
                selectedAssembledModel = null;
            }
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


        private void DrawAssembledModelDetails(AssembledModel model)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Assemble preview", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Bones: " + model.Bones.Count);
            EditorGUILayout.LabelField("Hooks: " + model.HookBones.Count);
            EditorGUILayout.LabelField("Skins: " + model.Skins.Count);
            EditorGUILayout.LabelField("Actions: " + model.Actions.Count);
            EditorGUILayout.LabelField("Animations: " + model.Animations.Count);
            EditorGUILayout.LabelField(
                "Status: " + (model.IsValid ? "OK" : "Has errors"),
                EditorStyles.miniBoldLabel);

            foreach (var skin in model.Skins)
            {
                EditorGUILayout.LabelField(
                    skin.SourcePath + " (" + skin.Surfaces.Count + " surfaces, " +
                    skin.BindBones.Count + " bind bones)",
                    EditorStyles.miniLabel);
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

        private AssembledModel TryAssembleSelectedSmd(GameDataFile file, SmdDocument smd)
        {
            if (index == null || file == null || smd == null)
                return null;

            try
            {
                var resolution = new ModelResourceResolver(index).Resolve(smd, file.RelativePath);
                if (resolution.Skeleton == null || !resolution.Skeleton.IsResolved)
                {
                    var incomplete = new AssembledModel
                    {
                        SourceSmdPath = file.RelativePath
                    };
                    incomplete.Errors.AddRange(resolution.Errors);
                    incomplete.Warnings.AddRange(resolution.Warnings);
                    return incomplete;
                }

                var decoder = GetGameDataDecoder();
                var skeletonBytes = File.ReadAllBytes(resolution.Skeleton.File.FullPath);
                var skeleton = new BonParser(decoder).Parse(
                    skeletonBytes,
                    resolution.Skeleton.File.RelativePath);

                var skins = new System.Collections.Generic.List<SkiDocument>();
                var skinPaths = new System.Collections.Generic.List<string>();
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
                    file.RelativePath,
                    skeleton,
                    resolution.Skeleton.File.RelativePath,
                    skins,
                    skinPaths,
                    resolution);
                AttachAssembledAnimations(model, resolution, decoder);
                return model;
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
            var normalizedPath = folderPath.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(normalizedPath))
                return;

            var parent = Path.GetDirectoryName(normalizedPath)?.Replace('\\', '/');
            var folderName = Path.GetFileName(normalizedPath);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(folderName))
                throw new IOException("Cannot create asset folder: " + folderPath);

            EnsureAssetFolders(parent);
            if (!AssetDatabase.IsValidFolder(normalizedPath) &&
                string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, folderName)))
                throw new IOException("Could not create asset folder: " + normalizedPath);
        }
    }
}
