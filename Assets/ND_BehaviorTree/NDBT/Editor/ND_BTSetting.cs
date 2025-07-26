
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements; 

namespace ND_BehaviorTree.Editor
{
    //[CreateAssetMenu(fileName = "ND_BehaviorTree_Settings", menuName = "ND_BehaviorTree/Settings Asset")] // Standardized name
    public sealed class ND_BehaviorTreeSetting : ScriptableObject
    {

        [HideInInspector] 
        public string enableSettingPassword = "SubcribeToNDDEVGAME"; 

        // Corrected path to match the CreateAssetMenu fileName if that's the intention
        private const string SettingsAssetPath = "Assets/ND_BehaviorTree/NDBT/Editor/Resources/ND_BehaviorTree_Settings.asset";

        [Tooltip("The UXML file to use for the default appearance of nodes in the editor.")]
        [SerializeField]
        private VisualTreeAsset defaultNodeUXML;

        [Tooltip("The USS file for the main GraphView's appearance.")]
        [SerializeField]
        private StyleSheet graphViewStyle;

        [SerializeField]
        private StyleSheet nodeDefaultUSS;

        [SerializeField]
        private VisualTreeAsset trelloNodeUXML;
        [SerializeField]
        private VisualTreeAsset trelloChildNodeUXML;
        [SerializeField]
        private StyleSheet trelloNodeUSS;



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
                        string directoryPath = Path.GetDirectoryName(SettingsAssetPath);
                        if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
                        {
                            Directory.CreateDirectory(directoryPath);
                        }
                        _instance = CreateInstance<ND_BehaviorTreeSetting>();
                        AssetDatabase.CreateAsset(_instance, SettingsAssetPath);
                        AssetDatabase.SaveAssets();
                        AssetDatabase.Refresh();
                        Debug.LogWarning($"Created new ND_DrawTrelloSetting at: {SettingsAssetPath}. Please configure it.");
                    }
                }
                return _instance;
            }
        }



        public VisualTreeAsset TrelloChildUXML => trelloChildNodeUXML; // Public getter

        public string GetTrelloChildPath()
        {
            if (trelloChildNodeUXML == null) return null;
            return AssetDatabase.GetAssetPath(trelloChildNodeUXML);
        }


        public VisualTreeAsset TrelloUXML => trelloNodeUXML; // Public getter

        public string GetTrelloUXMLPath()
        {
            if (trelloNodeUXML == null) return null;
            return AssetDatabase.GetAssetPath(trelloNodeUXML);
        }


        public StyleSheet TrelloNodeUSSStyle => trelloNodeUSS; // Public getter

        public string GetTrelloUSSPath()
        {
            if (trelloNodeUSS == null) return null;
            return AssetDatabase.GetAssetPath(trelloNodeUSS);
        }



        public StyleSheet NodeDefaultUSSStyle => nodeDefaultUSS; // Public getter

        public string GetNodeDefaultUSSPath()
        {
            if (nodeDefaultUSS == null) return null;
            return AssetDatabase.GetAssetPath(nodeDefaultUSS);
        }


        public StyleSheet GraphViewStyle => graphViewStyle; // Public getter

        public string GetGraphViewStyleSheetPath()
        {
            if (graphViewStyle == null) return null;
            return AssetDatabase.GetAssetPath(graphViewStyle);
        }

        public VisualTreeAsset DefaultNodeUXML => defaultNodeUXML; // Public getter

        public string GetNodeDefaultUXMLPath()
        {
            if (defaultNodeUXML == null)
            {
                Debug.LogError($"CRITICAL: 'Default Node UXML' is not assigned in {SettingsAssetPath}.");
                return null;
            }
            return AssetDatabase.GetAssetPath(defaultNodeUXML);
        }

        [MenuItem("Tools/ND_DrawTrello/Select Settings Asset", false, 100)]
        public static void SelectSettingsAsset()
        {
            Selection.activeObject = Instance; // This will also trigger Instance creation if needed
            // Optionally, ping the object in the project window
            if (Instance != null) EditorGUIUtility.PingObject(Instance);
        }
    }
}