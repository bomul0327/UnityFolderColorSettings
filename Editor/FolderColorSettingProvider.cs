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

namespace FolderColorSettings.Editor
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

        private static string _pathToAdd;

        private static Object _objectToAdd;

        private static Color _colorToAdd = Color.white;

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
            EditorGUILayout.LabelField("Add the color setting", EditorStyles.largeLabel);

            EditorGUILayout.BeginHorizontal();

            _objectToAdd = EditorGUILayout.ObjectField(_objectToAdd, typeof(Object), false);
            _pathToAdd = AssetDatabase.GetAssetPath(_objectToAdd);
            _colorToAdd = EditorGUILayout.ColorField(_colorToAdd);

            if (GUILayout.Button("Add / Modify"))
            {
                try
                {
                    if (File.GetAttributes(_pathToAdd).HasFlag(FileAttributes.Directory))
                    {
                        FolderIconDrawer.ColorDict[_pathToAdd] = _colorToAdd;
                        FolderIconDrawer.SaveColorSettings();
                        EditorApplication.RepaintProjectWindow();
                    }
                }
                catch
                {
                    // ignored
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(20);
            EditorGUILayout.LabelField("Current color settings", EditorStyles.largeLabel);

            foreach (var kv in FolderIconDrawer.ColorDict.ToList())
            {
                EditorGUILayout.BeginHorizontal();

                var drawObject = AssetDatabase.LoadAssetAtPath<Object>(kv.Key);
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.ObjectField(drawObject, typeof(Object), false);
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

        public static Dictionary<string, Color> ColorDict = new Dictionary<string, Color>();

        static FolderIconDrawer()
        {
            ColorDict.Clear();

            LoadColorSettings();

            DefaultFolderTexture = EditorGUIUtility.FindTexture("d_Folder Icon");
            OpenedFolderTexture = EditorGUIUtility.FindTexture("d_FolderOpened Icon");
            EmptyFolderTexture = EditorGUIUtility.FindTexture("d_FolderEmpty Icon");

            EditorApplication.projectWindowItemOnGUI += DrawFolderIcon;
        }

        public static void DrawFolderIcon(string guid, Rect rect)
        {
            if (!FolderColorSettingProvider.UseCustomFolderColor) return;

            var path = AssetDatabase.GUIDToAssetPath(guid);

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
    internal static class ProjectWindowUtil
    {
        private static TreeViewState _currentTreeViewState;

        public static bool IsFolderOpened(string path)
        {
            if (_currentTreeViewState == null)
            {
                SetTreeViewState();
            }

            if (_currentTreeViewState != null)
            {
                var instanceID = AssetDatabase.LoadAssetAtPath<Object>(path).GetInstanceID();
                return _currentTreeViewState.expandedIDs.Contains(instanceID);
            }

            return false;
        }

        private static void SetTreeViewState()
        {
            try
            {
                var projectBrowserType = typeof(EditorWindow).Assembly.GetType("UnityEditor.ProjectBrowser");
                var projectBrowser = EditorWindow.GetWindow(projectBrowserType);

                var folderTreeStateField = projectBrowserType.GetField("m_FolderTreeState",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (folderTreeStateField != null)
                    _currentTreeViewState = folderTreeStateField.GetValue(projectBrowser) as TreeViewState;
            }
            catch
            {
                _currentTreeViewState = null;
            }
        }
    }
}
