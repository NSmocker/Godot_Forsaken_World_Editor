using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SilverEcho.ForsakenWorld.Editor
{
    /// <summary>
    /// Godot aui_xml_panel.gd: list interfaces/*.xml for a language, filter by layout/search,
    /// build a preview hierarchy via <see cref="AngelicaUiBuilder"/>.
    /// </summary>
    public sealed class AuiXmlPanel : EditorWindow
    {
        private const string PrefRoot = "ForsakenWorld.Aui.Root";
        private const string PrefLang = "ForsakenWorld.Aui.Lang";

        private static readonly string[] LanguageLabels = { "Deutsch", "Englisch", "Französisch" };
        private static readonly string[] LanguageCodes = { "Deu", "Eng", "Franz" };

        private string rootPath = string.Empty;
        private int languageIndex;
        private int layoutFilter; // 0=Alle, 1..Layouts, last=Ohne Liste
        private string search = string.Empty;
        private string status = "Choose game data root and language.";
        private Vector2 scroll;
        private AngelicaUi ui;
        private List<string> files = new List<string>();
        private string selectedFile = string.Empty;

        [MenuItem("Tools/Forsaken World/AUI XML")]
        public static void Open()
        {
            GetWindow<AuiXmlPanel>("AUI-XML");
        }

        private void OnEnable()
        {
            rootPath = EditorPrefs.GetString(PrefRoot, string.Empty);
            languageIndex = EditorPrefs.GetInt(PrefLang, 0);
            RebuildUi();
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Game data", GUILayout.Width(72));
                rootPath = EditorGUILayout.TextField(rootPath);
                if (GUILayout.Button("…", GUILayout.Width(28)))
                {
                    var folder = EditorUtility.OpenFolderPanel("Game data root", rootPath, string.Empty);
                    if (!string.IsNullOrEmpty(folder))
                    {
                        rootPath = folder;
                        EditorPrefs.SetString(PrefRoot, rootPath);
                        RebuildUi();
                    }
                }

                if (GUILayout.Button("Reload", GUILayout.Width(70)))
                    RebuildUi();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Language", GUILayout.Width(64));
                EditorGUI.BeginChangeCheck();
                languageIndex = EditorGUILayout.Popup(languageIndex, LanguageLabels, EditorStyles.toolbarPopup, GUILayout.Width(100));
                if (EditorGUI.EndChangeCheck())
                {
                    EditorPrefs.SetInt(PrefLang, languageIndex);
                    RebuildUi();
                }

                var layoutLabels = BuildLayoutLabels();
                EditorGUI.BeginChangeCheck();
                layoutFilter = EditorGUILayout.Popup(layoutFilter, layoutLabels, EditorStyles.toolbarPopup, GUILayout.Width(120));
                if (EditorGUI.EndChangeCheck())
                    Repaint();

                search = EditorGUILayout.TextField(search);
            }

            EditorGUILayout.LabelField(status, EditorStyles.miniLabel);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            var query = (search ?? string.Empty).Trim().ToLowerInvariant();
            foreach (var file in files)
            {
                if (query.Length > 0 && file.ToLowerInvariant().IndexOf(query, StringComparison.Ordinal) < 0)
                    continue;
                var lay = ui != null ? ui.LayoutOf(file) : string.Empty;
                if (!LayoutMatches(lay))
                    continue;
                var label = string.IsNullOrEmpty(lay) ? file : file + "  [" + lay + "]";
                if (GUILayout.Toggle(selectedFile == file, label, "Button"))
                {
                    if (selectedFile != file)
                        selectedFile = file;
                }
            }

            EditorGUILayout.EndScrollView();

            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(selectedFile) || ui == null))
            {
                if (GUILayout.Button("Build preview in scene"))
                    BuildSelected();
            }
        }

        private void RebuildUi()
        {
            files.Clear();
            selectedFile = string.Empty;
            if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath))
            {
                status = "Game data root missing.";
                ui = null;
                return;
            }

            var lang = languageIndex >= 0 && languageIndex < LanguageCodes.Length
                ? LanguageCodes[languageIndex]
                : "Deu";
            ui = new AngelicaUi(rootPath, lang);
            files = ui.ListInterfaceXmlFiles();
            status = files.Count + " dialogs in " + (ui.InterfacesDir.Length == 0 ? "(no interfaces)" : ui.InterfacesDir);
        }

        private void BuildSelected()
        {
            if (ui == null || string.IsNullOrEmpty(selectedFile))
                return;
            var dialog = ui.ReadDialog(selectedFile);
            if (dialog == null)
            {
                status = "Failed to read " + selectedFile;
                return;
            }

            var loader = new AuiSurfaceLoader();
            var root = AngelicaUiBuilder.BuildDialog(dialog, ui, loader);
            if (root == null)
            {
                status = "Build failed for " + selectedFile;
                return;
            }

            Undo.RegisterCreatedObjectUndo(root, "AUI Preview " + selectedFile);
            Selection.activeGameObject = root;
            var warn = loader.Warnings.Count > 0
                ? (" · " + loader.Warnings.Count + " surface warn")
                : string.Empty;
            status = "Built " + root.name + " (" + dialog.Controls.Count + " controls)" + warn;
        }

        private string[] BuildLayoutLabels()
        {
            var list = new List<string> { "Alle" };
            list.AddRange(AngelicaUi.Layouts);
            list.Add("Ohne Liste");
            return list.ToArray();
        }

        private bool LayoutMatches(string lay)
        {
            if (layoutFilter <= 0)
                return true;
            if (layoutFilter > AngelicaUi.Layouts.Length)
                return string.IsNullOrEmpty(lay);
            return string.Equals(lay, AngelicaUi.Layouts[layoutFilter - 1], StringComparison.OrdinalIgnoreCase);
        }
    }
}
