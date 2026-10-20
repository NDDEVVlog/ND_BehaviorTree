using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ND_BehaviorTree.Editor.Plugins
{
    /// <summary>
    /// ND_BehaviorTree > Plugins: what each installed plugin adds (node types, themes, menu items, rules) and a
    /// form that generates a new plugin skeleton (<see cref="ND_BTPluginScaffold"/>).
    /// </summary>
    public class ND_BTPluginsWindow : EditorWindow
    {
        private Vector2 m_Scroll;
        private string m_NewName = "MyNodes";
        private string m_Root;
        private bool m_IncludeView = true;
        private bool m_IncludeTests = true;

        [MenuItem("ND_BehaviorTree/Plugins...", false, 30)]
        public static void Open()
        {
            var window = GetWindow<ND_BTPluginsWindow>();
            window.titleContent = new GUIContent("ND BT Plugins");
            window.minSize = new Vector2(420, 360);
        }

        private void OnEnable()
        {
            m_Root = ND_BTPluginScaffold.DefaultRoot();
            ND_BTPluginRegistry.Changed += Repaint;
        }

        private void OnDisable() => ND_BTPluginRegistry.Changed -= Repaint;

        private void OnGUI()
        {
            m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll);

            EditorGUILayout.LabelField("Installed plugins", EditorStyles.boldLabel);
            var entries = ND_BTPluginRegistry.Entries;
            if (entries.Count == 0) EditorGUILayout.HelpBox("No plugin found. A plugin is any class deriving from ND_BTPlugin in an Editor assembly.", MessageType.Info);
            foreach (var entry in entries) DrawEntry(entry);

            EditorGUILayout.Space(12);
            DrawCreateForm();

            EditorGUILayout.EndScrollView();
        }

        private static void DrawEntry(ND_BTPluginRegistry.Entry entry)
        {
            var plugin = entry.Plugin;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"{plugin.DisplayName}   v{plugin.Version}", EditorStyles.boldLabel);
                if (!string.IsNullOrEmpty(plugin.Description)) EditorGUILayout.LabelField(plugin.Description, EditorStyles.wordWrappedMiniLabel);
                if (!entry.Installed)
                {
                    EditorGUILayout.HelpBox($"Failed to register (disabled): {entry.Error}", MessageType.Error);
                    return;
                }

                var ctx = entry.Context;
                var nodes = NodeTypesOf(plugin);
                EditorGUILayout.LabelField("Node types", nodes.Length == 0 ? "—" : string.Join(", ", nodes.Select(t => t.Name)), EditorStyles.wordWrappedMiniLabel);
                Row("Themes", ctx.Themes.Count == 0 ? null : string.Join(", ", ctx.Themes));
                Row("Canvas menu", Paths(ctx.CanvasItems));
                Row("Node menu", Paths(ctx.NodeItems));
                Row("Toolbar", Paths(ctx.ToolbarItems));
                Row("Validation", ctx.Rules.Count == 0 ? null : string.Join(", ", ctx.Rules.Select(r => r.Name)));
                Row("Edit hooks", ctx.Unhooks.Count == 0 ? null : ctx.Unhooks.Count.ToString());
                EditorGUILayout.LabelField("Class", plugin.GetType().FullName, EditorStyles.miniLabel);
            }
        }

        private static void Row(string label, string value)
        {
            if (value != null) EditorGUILayout.LabelField(label, value, EditorStyles.wordWrappedMiniLabel);
        }

        private static string Paths(System.Collections.Generic.List<ND_BTPluginMenuItem> items) =>
            items.Count == 0 ? null : string.Join(", ", items.Select(i => i.Path));

        /// <summary>Node types in the plugin's namespace family: "X.Editor" plugin class → node types in namespace "X" or below.</summary>
        private static Type[] NodeTypesOf(ND_BTPlugin plugin)
        {
            string ns = plugin.GetType().Namespace ?? string.Empty;
            if (ns.EndsWith(".Editor")) ns = ns.Substring(0, ns.Length - ".Editor".Length);
            if (ns.Length == 0) return new Type[0];
            return TypeCache.GetTypesDerivedFrom<Node>()
                .Where(t => !t.IsAbstract && t.Namespace != null && (t.Namespace == ns || t.Namespace.StartsWith(ns + ".")) && !t.Namespace.Contains(".Tests"))
                .OrderBy(t => t.Name).ToArray();
        }

        private void DrawCreateForm()
        {
            EditorGUILayout.LabelField("Create a new plugin", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                m_NewName = EditorGUILayout.TextField(new GUIContent("Name", "PascalCase, e.g. DialogueNodes. Assemblies: ND_BehaviorTree.<Name>(.Editor/.Tests)."), m_NewName);
                using (new EditorGUILayout.HorizontalScope())
                {
                    m_Root = EditorGUILayout.TextField("Folder", m_Root);
                    if (GUILayout.Button("…", GUILayout.Width(24)))
                    {
                        string picked = EditorUtility.OpenFolderPanel("Plugin folder", m_Root, "");
                        string assets = Application.dataPath.Replace('\\', '/');
                        if (!string.IsNullOrEmpty(picked) && picked.Replace('\\', '/').StartsWith(assets))
                            m_Root = "Assets" + picked.Replace('\\', '/').Substring(assets.Length);
                    }
                }
                m_IncludeView = EditorGUILayout.Toggle(new GUIContent("Custom node view + USS", "Adds a [CustomNodeView] view and a stylesheet for the example node."), m_IncludeView);
                m_IncludeTests = EditorGUILayout.Toggle(new GUIContent("Tests", "Adds a test assembly (run by ND_BehaviorTree > Diagnostics > Run Editor Tests)."), m_IncludeTests);

                string error = ND_BTPluginScaffold.Validate(m_NewName, m_Root);
                var options = new ND_BTPluginScaffold.Options { IncludeNodeView = m_IncludeView, IncludeTests = m_IncludeTests };
                if (error != null) EditorGUILayout.HelpBox(error, MessageType.Warning);
                else EditorGUILayout.HelpBox("Will create:\n" + string.Join("\n", ND_BTPluginScaffold.Generate(m_NewName, options).Keys.Select(k => $"{m_Root}/{m_NewName}/{k}")), MessageType.None);

                using (new EditorGUI.DisabledScope(error != null))
                {
                    if (GUILayout.Button("Create Plugin", GUILayout.Height(24)))
                    {
                        string folder = ND_BTPluginScaffold.Create(m_NewName, m_Root, options);
                        if (folder != null) EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<DefaultAsset>(folder));
                    }
                }
            }
            EditorGUILayout.HelpBox("Add more nodes to a plugin: select its Runtime folder, then Assets > Create > ND_BehaviorTree > Action / Composite / Decorator / Service Node.", MessageType.Info);
        }
    }
}
