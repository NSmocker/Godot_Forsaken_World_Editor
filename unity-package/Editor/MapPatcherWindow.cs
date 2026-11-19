using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SilverEcho.ForsakenWorld.Editor
{
    public sealed class MapPatcherWindow : EditorWindow
    {
        // Actual a05.ecbsd ornament: "1\litmodel_515.bmd" under LitModels\a05.
        internal const string ExamplePath = @"LitModels\a05\1\litmodel_515.bmd";
        private const string RootPreference = "SilverEcho.ForsakenWorld.GameDataRoot";
        [SerializeField] private List<string> paths = new List<string> { "" };
        [SerializeField] private string gameRoot = "";
        [SerializeField] private string lastMapFolder = "";
        private Vector2 scroll;
        private string status = "";
        private MessageType statusType = MessageType.Info;
        private string backupFolder;
        private GUIStyle placeholderStyle;
        private bool placeholderDarkSkin;

        [MenuItem("Tools/Angelica 2.x/Map Patcher")]
        public static void Open()
        {
            var window = GetWindow<MapPatcherWindow>("Map Patcher");
            window.minSize = new Vector2(560, 320);
        }

        private void OnEnable()
        {
            if (paths == null) paths = new List<string> { "" };
            if (string.IsNullOrEmpty(gameRoot)) gameRoot = EditorPrefs.GetString(RootPreference, "");
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Litmodels для карти", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Позиція, поворот і масштаб беруться з BMD.", EditorStyles.wordWrappedLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                gameRoot = EditorGUILayout.TextField(new GUIContent("Папка гри", "Коренева папка з LitModels. Якщо порожньо — визначається з maps/<карта>."), gameRoot);
                if (GUILayout.Button("…", GUILayout.Width(30)))
                {
                    string selected = EditorUtility.OpenFolderPanel("Папка гри з LitModels", gameRoot, "");
                    if (!string.IsNullOrEmpty(selected)) gameRoot = selected;
                }
            }
            EditorGUILayout.Space(6);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            int remove = -1;
            for (int i = 0; i < paths.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField((i + 1).ToString(), GUILayout.Width(24));
                    Rect rect = EditorGUILayout.GetControlRect();
                    paths[i] = EditorGUI.TextField(rect, paths[i]);
                    if (string.IsNullOrEmpty(paths[i]) && Event.current.type == EventType.Repaint)
                    {
                        if (placeholderStyle == null || placeholderDarkSkin != EditorGUIUtility.isProSkin)
                        {
                            placeholderDarkSkin = EditorGUIUtility.isProSkin;
                            placeholderStyle = new GUIStyle(EditorStyles.label) { fontStyle = FontStyle.Italic };
                            placeholderStyle.normal.textColor = placeholderDarkSkin ? new Color(.55f, .55f, .55f) : Color.gray;
                        }
                        rect.xMin += 4;
                        GUI.Label(rect, ExamplePath, placeholderStyle);
                    }
                    if (GUILayout.Button(new GUIContent("−", "Видалити рядок"), GUILayout.Width(28))) remove = i;
                }
            }
            if (remove >= 0) paths.RemoveAt(remove);
            if (GUILayout.Button(new GUIContent("+", "Додати Litmodel"), GUILayout.Width(34))) paths.Add("");
            EditorGUILayout.EndScrollView();
            EditorGUILayout.LabelField("Шлях від кореня гри: LitModels\\<карта>\\…\\модель.bmd", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(6);
            using (new EditorGUI.DisabledScope(paths.Count == 0 || paths.Any(string.IsNullOrWhiteSpace)))
                if (GUILayout.Button("Пропатчити карту", GUILayout.Height(34))) PatchMap();
            if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, statusType);
            if (!string.IsNullOrEmpty(backupFolder) && GUILayout.Button("Відкрити резервну копію"))
                EditorUtility.RevealInFinder(backupFolder);
            EditorGUILayout.Space(6);
        }

        private void PatchMap()
        {
            string folder = EditorUtility.OpenFolderPanel("Виберіть папку з файлами карти", lastMapFolder, "");
            if (string.IsNullOrEmpty(folder)) return;
            lastMapFolder = folder;
            backupFolder = null;
            try
            {
                EditorUtility.DisplayProgressBar("Map Patcher", "Перевірка Litmodels і запис карти…", .35f);
                var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(MapPatcherWindow).Assembly);
                if (package == null) throw new InvalidOperationException("Не знайдено Unity-пакет Angelica.");
                var encoding = new MapPatcherGbk(File.ReadAllBytes(Path.Combine(package.resolvedPath, "Runtime", "Data", "gbk_table.bin")));
                var report = MapPatcherFiles.Patch(folder, paths, gameRoot, encoding);
                backupFolder = report.BackupFolder;
                statusType = MessageType.Info;
                status = report.Added == 0
                    ? "Усі вказані Litmodels уже підключені. Файли карти не змінено."
                    : "Карту «" + report.MapName + "» пропатчено. Додано Litmodels: " + report.Added +
                      ". Оновлено блоків: " + report.ChangedBlocks + ". Пропущено повторів: " + report.Skipped +
                      ".\nРезервна копія: " + backupFolder;
                Debug.Log("[Map Patcher] " + status + "\n" + report.WorldPath + "\n" + report.SharedDataPath);
            }
            catch (Exception error)
            {
                statusType = MessageType.Error;
                status = error.Message;
                Debug.LogWarning("[Map Patcher] " + error);
            }
            finally { EditorUtility.ClearProgressBar(); }
        }
    }
}
