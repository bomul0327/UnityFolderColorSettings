using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using ColorUtility = UnityEngine.ColorUtility;
using Object = UnityEngine.Object;

namespace UnityFolderColorSettings.Editor
{
    /// <summary>
    /// This class only shows the UI.
    /// </summary>
    public class FolderColorSettingProvider : SettingsProvider
    {
        /// <summary>
        /// Sets whether using the feature or not
        /// </summary>
        public static bool UseCustomFolderColor { get; private set; }

        private static Dictionary<string, Color> ColorToAddDict = new Dictionary<string, Color>();

        private static Color DefaultColor = Color.white;

        static FolderColorSettingProvider()
        {
            UseCustomFolderColor = EditorPrefs.GetBool("UseCustomFolderColor", true);
        }

        public FolderColorSettingProvider(string path, SettingsScope scope)
            : base(path, scope)
        {
        }

        [SettingsProvider]
        public static SettingsProvider CreateFolderIconSettingProvider()
        {
            var provider = new FolderColorSettingProvider("Preferences/Folder Color Settings", SettingsScope.User);

            return provider;
        }

        public override void OnGUI(string searchContext)
        {
            bool newUseFolderIconFeature =
                EditorGUILayout.Toggle("Use Custom Folder Color", UseCustomFolderColor, EditorStyles.toggle);

            if (newUseFolderIconFeature != UseCustomFolderColor)
            {
                UseCustomFolderColor = newUseFolderIconFeature;
                EditorPrefs.SetBool("UseCustomFolderColor", UseCustomFolderColor);
                EditorApplication.RepaintProjectWindow();
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Select folders to add the color setting", EditorStyles.largeLabel);

            foreach (var asset in Selection.GetFiltered<Object>(SelectionMode.Assets))
            {
                string path = AssetDatabase.GetAssetPath(asset);
                if (File.GetAttributes(path).HasFlag(FileAttributes.Directory))
                {
                    EditorGUILayout.BeginHorizontal();

                    EditorGUILayout.ObjectField(asset, typeof(Object), false);

                    var color = ColorToAddDict.GetValueOrDefault(path, DefaultColor);
                    ColorToAddDict[path] = EditorGUILayout.ColorField(color);

                    if (GUILayout.Button("Add / Modify"))
                    {
                        try
                        {
                            FolderIconDrawer.ColorDict[path] = ColorToAddDict[path];
                            FolderIconDrawer.SaveColorSettings();
                            EditorApplication.RepaintProjectWindow();
                        }
                        catch
                        {
                            // ignored
                        }
                    }

                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.Space(20);
            EditorGUILayout.LabelField("Current color settings", EditorStyles.largeLabel);

            foreach (var kv in FolderIconDrawer.ColorDict.ToList())
            {
                EditorGUILayout.BeginHorizontal();

                var drawObject = AssetDatabase.LoadAssetAtPath<Object>(kv.Key);
                using (new EditorGUI.DisabledScope(true))
                {
                    if (drawObject)
                    {
                        EditorGUILayout.ObjectField(drawObject, typeof(Object), false);
                    }
                    else
                    {
                        EditorGUILayout.TextField(kv.Key);
                    }
                }

                var newColor = EditorGUILayout.ColorField(kv.Value);
                if (newColor != kv.Value)
                {
                    FolderIconDrawer.ColorDict[kv.Key] = newColor;
                    FolderIconDrawer.SaveColorSettings();
                    EditorApplication.RepaintProjectWindow();
                }

                if (GUILayout.Button("Remove"))
                {
                    FolderIconDrawer.ColorDict.Remove(kv.Key);
                    FolderIconDrawer.SaveColorSettings();
                    EditorApplication.RepaintProjectWindow();
                    break;
                }

                EditorGUILayout.EndHorizontal();
            }
        }
    }

    /// <summary>
    /// This class Draws folder icons
    /// NOTE: this only works properly on single project window
    /// </summary>
    [InitializeOnLoad]
    internal static class FolderIconDrawer
    {
        /// <summary>
        /// Get textures from internal editor icon
        /// </summary>
        private static readonly Texture2D DefaultFolderTexture;

        private static readonly Texture2D OpenedFolderTexture;
        private static readonly Texture2D EmptyFolderTexture;

        /// <summary>
        /// Key: path, Value: Color
        /// </summary>
        public static Dictionary<string, Color> ColorDict = new Dictionary<string, Color>();

        static FolderIconDrawer()
        {
            ColorDict.Clear();

            LoadColorSettings();

            DefaultFolderTexture = EditorGUIUtility.FindTexture("d_Folder Icon");
            OpenedFolderTexture = EditorGUIUtility.FindTexture("d_FolderOpened Icon");
            EmptyFolderTexture = EditorGUIUtility.FindTexture("d_FolderEmpty Icon");

            EditorApplication.projectWindowItemInstanceOnGUI += DrawFolderIcon;
            EditorApplication.update += UpdateProjectBrowser;
        }

        private static void UpdateProjectBrowser()
        {
            ProjectWindowUtil.UpdateBrowserFields();
        }

        public static void DrawFolderIcon(int instanceid, Rect rect)
        {
            if (!FolderColorSettingProvider.UseCustomFolderColor) return;

            var path = AssetDatabase.GetAssetPath(instanceid);

            if (string.IsNullOrEmpty(path) ||
                Event.current.type != EventType.Repaint ||
                !File.GetAttributes(path).HasFlag(FileAttributes.Directory) ||
                !ColorDict.ContainsKey(path))
            {
                return;
            }

            bool isOpened = false;
            bool isTreeView = rect.width > rect.height;
            bool isSideView = Math.Abs(rect.x - 14) > float.Epsilon;

            // Add extra offset depending on its view.
            if (isTreeView)
            {
                rect.width = rect.height = 16;

                if (!isSideView)
                {
                    rect.x += 3f;
                }
                else
                {
                    // This will be used in tree view on side only.
                    isOpened = ProjectWindowUtil.IsFolderOpened(path);
                }
            }
            else
            {
                rect.height -= 14f;
            }

            var prevColor = GUI.color;
            GUI.color = ColorDict[path];

            if (!Directory.EnumerateFileSystemEntries(path).Any())
            {
                GUI.DrawTexture(rect, EmptyFolderTexture);
            }
            else if (isOpened)
            {
                GUI.DrawTexture(rect, OpenedFolderTexture);
            }
            else
            {
                GUI.DrawTexture(rect, DefaultFolderTexture);
            }

            GUI.color = prevColor;
        }

        /// <summary>
        /// Colors and paths are saved at the editor preference
        /// </summary>
        public static void SaveColorSettings()
        {
            EditorPrefs.SetInt("FolderIconColorCount", ColorDict.Count);

            int index = 0;
            foreach (var kvp in ColorDict)
            {
                EditorPrefs.SetString($"FolderIconColorPath{index}", kvp.Key);
                EditorPrefs.SetString($"FolderIconColorValue{index}", ColorUtility.ToHtmlStringRGBA(kvp.Value));
                index++;
            }
        }

        /// <summary>
        /// Load Colors and paths from the editor preference
        /// </summary>
        public static void LoadColorSettings()
        {
            int count = EditorPrefs.GetInt("FolderIconColorCount", 0);
            for (int i = 0; i < count; i++)
            {
                string path = EditorPrefs.GetString($"FolderIconColorPath{i}");
                string colorString = EditorPrefs.GetString($"FolderIconColorValue{i}");
                if (ColorUtility.TryParseHtmlString($"#{colorString}", out var color))
                {
                    ColorDict[path] = color;
                }
            }
        }
    }

    /// <summary>
    /// This util is for getting current tree view state in project window
    /// </summary>
    [InitializeOnLoad]
    internal static class ProjectWindowUtil
    {
        private static Type ProjectBrowserType;

        private static EditorWindow ProjectBrowser;

        /// <summary>
        /// Tree view state for one column
        /// </summary>
        private static TreeViewState CurrentAssetTreeViewState;

        /// <summary>
        /// Tree view state for two column
        /// </summary>
        private static TreeViewState CurrentFolderTreeViewState;

        // 0 for one column, 1 for two column
        private static int CurrentProjectBrowserMode;

        private static FieldInfo AssetTreeStateField;

        private static FieldInfo FolderTreeStateField;

        private static FieldInfo ProjectBroswerMode;

        static ProjectWindowUtil()
        {
            ProjectBrowserType = typeof(EditorWindow).Assembly.GetType("UnityEditor.ProjectBrowser");
            AssetTreeStateField =
                ProjectBrowserType.GetField("m_AssetTreeState", BindingFlags.NonPublic | BindingFlags.Instance);
            FolderTreeStateField =
                ProjectBrowserType.GetField("m_FolderTreeState", BindingFlags.NonPublic | BindingFlags.Instance);
            ProjectBroswerMode =
                ProjectBrowserType.GetField("m_ViewMode", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        /// <summary>
        /// Check whether current folder is opened or not
        /// </summary>
        /// <param name="path">Asset path</param>
        /// <param name="mode">0 is one column, 1 is two column</param>
        public static bool IsFolderOpened(string path)
        {
            var state = CurrentProjectBrowserMode == 0 ? CurrentAssetTreeViewState : CurrentFolderTreeViewState;

            if (state != null)
            {
                var instanceID = AssetDatabase.LoadAssetAtPath<Object>(path).GetInstanceID();
                return state.expandedIDs.Contains(instanceID);
            }

            return false;
        }

        /// <summary>
        /// Update tree view state from currently focused project browser
        /// </summary>
        public static void UpdateBrowserFields()
        {
            try
            {
                var projectBrowsers = Resources.FindObjectsOfTypeAll(ProjectBrowserType);

                foreach (var obj in projectBrowsers)
                {
                    var browser = obj as EditorWindow;
                    if (browser.hasFocus)
                    {
                        ProjectBrowser = browser;
                    }
                }

                CurrentAssetTreeViewState = AssetTreeStateField.GetValue(ProjectBrowser) as TreeViewState;
                CurrentFolderTreeViewState = FolderTreeStateField.GetValue(ProjectBrowser) as TreeViewState;
                CurrentProjectBrowserMode = (int)ProjectBroswerMode.GetValue(ProjectBrowser);
            }
            catch
            {
                CurrentFolderTreeViewState = null;
            }
        }
    }
}
