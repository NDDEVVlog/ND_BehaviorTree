using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND_BehaviorTree.Editor
{
    public sealed class ND_BehaviorTreeSetting : ScriptableObject
    {
        [Header("Security")]
        public bool isLockEnabled = true;
        public string enableSettingPassword = "SubcribeToNDDEVGAMEandSORA";

        [Header("Core Configuration")]
        public NodeEditorConfig activeConfig;

        [Header("Global UI Templates")]
        public VisualTreeAsset defaultNodeUXML;
        public StyleSheet defaultNodeStyle; 
        public StyleSheet graphViewStyle;
        
        [Header("Graph Theme")]
        public Color graphBackgroundColor = new Color(0.12f, 0.12f, 0.12f, 1.0f);
        public Color gridColor = new Color(0.18f, 0.18f, 0.18f, 1.0f);

        [Header("Component Themes")]
        public EdgeTheme edgeTheme = new EdgeTheme();
        public PortTheme portTheme = new PortTheme();
        public BlackboardTheme blackboardTheme = new BlackboardTheme();

        private const string SettingsAssetPath = "Assets/ND_BehaviorTree/NDBT/Editor/Resources/ND_BehaviorTree_Settings.asset";
        private static ND_BehaviorTreeSetting _instance;

        public static ND_BehaviorTreeSetting Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = AssetDatabase.LoadAssetAtPath<ND_BehaviorTreeSetting>(SettingsAssetPath);
                    if (_instance == null)
                    {
                        string dir = Path.GetDirectoryName(SettingsAssetPath);
                        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                        _instance = CreateInstance<ND_BehaviorTreeSetting>();
                        AssetDatabase.CreateAsset(_instance, SettingsAssetPath);
                        AssetDatabase.SaveAssets();
                    }
                }
                return _instance;
            }
        }
    }
}