using UnityEditor;
using UnityEngine;
using ND_BehaviorTree.Editor.CustomGraph;

namespace ND_BehaviorTree.Editor
{
    [CustomEditor(typeof(NodeEditorConfig))]
    public class NodeEditorConfigEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var config = (NodeEditorConfig)target;

            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            if (EditorGUI.EndChangeCheck()) ND_BTViewFactory.InvalidateCache();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Packs", EditorStyles.boldLabel);

            if (ND_BehaviorTreeSetting.Instance.activeConfig != config)
            {
                EditorGUILayout.HelpBox("This is not the Active Config in ND Behavior Tree settings; the list below shows what it would use.", MessageType.Info);
            }

            var packs = NodeAppearanceRegistry.Collect(config);
            if (packs.Count == 0)
            {
                EditorGUILayout.HelpBox("No Node Appearance Pack found. Create one with Create > ND_BehaviorTree > Node Appearance Pack.", MessageType.None);
                return;
            }

            foreach (var info in packs)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool enabled = EditorGUILayout.Toggle(info.Enabled, GUILayout.Width(16));
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.ObjectField(info.Pack, typeof(NodeAppearancePack), false);
                    string source = info.Source == NodeAppearanceSource.Plugin ? $"Plugin: {info.PluginName}" : info.Source.ToString();
                    EditorGUILayout.LabelField($"{source} · priority {info.Pack.priority} · {info.Pack.mappings.Count} mappings", EditorStyles.miniLabel, GUILayout.Width(240));

                    if (enabled != info.Enabled)
                    {
                        Undo.RecordObject(config, enabled ? "Enable Appearance Pack" : "Disable Appearance Pack");
                        config.SetEnabled(info.Pack, enabled);
                        EditorUtility.SetDirty(config);
                        ND_BTViewFactory.InvalidateCache();
                    }
                }
                if (info.InactiveReason != null)
                    EditorGUILayout.LabelField(" ", $"Not used: {info.InactiveReason}", EditorStyles.miniLabel);
            }
        }
    }
}
