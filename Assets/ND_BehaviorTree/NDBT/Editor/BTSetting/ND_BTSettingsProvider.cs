using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ND_BehaviorTree.Editor
{
    /// <summary>
    /// Project Settings pages for ND Behavior Tree:
    ///   Project/ND Behavior Tree                  → general settings (graph theme, templates, port/edge/blackboard themes)
    ///   Project/ND Behavior Tree/Node Appearance  → the active NodeEditorConfig (per-node UXML/USS/custom view mappings)
    /// Also reachable from the ND_BehaviorTree menu.
    /// </summary>
    public static class ND_BTSettingsProvider
    {
        public const string GeneralPath = "Project/ND Behavior Tree";
        public const string NodeAppearancePath = GeneralPath + "/Node Appearance";

        [MenuItem("ND_BehaviorTree/Settings", false, 100)]
        public static void OpenGeneral() => SettingsService.OpenProjectSettings(GeneralPath);

        [MenuItem("ND_BehaviorTree/Node Appearance", false, 101)]
        public static void OpenNodeAppearance() => SettingsService.OpenProjectSettings(NodeAppearancePath);

        [SettingsProvider]
        public static SettingsProvider CreateGeneral()
        {
            UnityEditor.Editor cachedEditor = null;
            return new SettingsProvider(GeneralPath, SettingsScope.Project)
            {
                label = "ND Behavior Tree",
                keywords = new HashSet<string>(new[] { "Behavior", "Tree", "Graph", "Theme", "Grid", "Edge", "Port", "Blackboard" }),
                guiHandler = _ =>
                {
                    var settings = ND_BehaviorTreeSetting.Instance;
                    DrawAssetHeader(settings);
                    UnityEditor.Editor.CreateCachedEditor(settings, null, ref cachedEditor);
                    EditorGUI.BeginChangeCheck();
                    cachedEditor.OnInspectorGUI();
                    bool changed = EditorGUI.EndChangeCheck();

                    EditorGUILayout.Space(10);
                    if (GUILayout.Button("Edit Node Appearance →", GUILayout.Height(24))) OpenNodeAppearance();
                    DrawApplyButton(changed);
                }
            };
        }

        [SettingsProvider]
        public static SettingsProvider CreateNodeAppearance()
        {
            UnityEditor.Editor cachedEditor = null;
            return new SettingsProvider(NodeAppearancePath, SettingsScope.Project)
            {
                label = "Node Appearance",
                keywords = new HashSet<string>(new[] { "Node", "UXML", "USS", "Style", "Mapping", "Appearance", "Theme" }),
                guiHandler = _ =>
                {
                    var settings = ND_BehaviorTreeSetting.Instance;
                    var config = settings.activeConfig;
                    if (config == null)
                    {
                        EditorGUILayout.HelpBox("No Node Editor Config is assigned in ND Behavior Tree settings ('Active Config'). Nodes use the default UXML/USS.", MessageType.Warning);
                        if (GUILayout.Button("Open ND Behavior Tree Settings")) OpenGeneral();
                        return;
                    }

                    EditorGUILayout.HelpBox("Map a node script to a UXML/USS key or a custom ND_BTNodeView subclass. The most specific base class mapping wins.", MessageType.Info);
                    DrawAssetHeader(config);
                    UnityEditor.Editor.CreateCachedEditor(config, null, ref cachedEditor);
                    EditorGUI.BeginChangeCheck();
                    cachedEditor.OnInspectorGUI();
                    DrawApplyButton(EditorGUI.EndChangeCheck());
                }
            };
        }

        private static void DrawAssetHeader(Object asset)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Asset", AssetDatabase.GetAssetPath(asset), EditorStyles.miniLabel);
                if (GUILayout.Button("Select", GUILayout.Width(60)))
                {
                    Selection.activeObject = asset;
                    EditorGUIUtility.PingObject(asset);
                }
            }
            EditorGUILayout.Space(4);
        }

        private static void DrawApplyButton(bool changed)
        {
            if (changed) CustomGraph.ND_BTViewFactory.InvalidateCache();
            EditorGUILayout.Space(6);
            if (GUILayout.Button("Apply to Open Behavior Tree Editors", GUILayout.Height(24)))
            {
                CustomGraph.ND_BTViewFactory.InvalidateCache();
                foreach (var window in Resources.FindObjectsOfTypeAll<ND_BehaviorTreeEditorWindow>()) window.RefreshGraph();
            }
        }
    }
}
