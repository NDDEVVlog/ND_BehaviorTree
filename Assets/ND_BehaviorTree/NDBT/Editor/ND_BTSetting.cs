using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements; 

namespace ND_BehaviorTree.Editor
{   

    
    public sealed class ND_BehaviorTreeSetting : ScriptableObject
    {




        [HideInInspector]
        public string enableSettingPassword = "SubcribeToNDDEVGAME";

        private const string SettingsAssetPath = "Assets/ND_BehaviorTree/NDBT/Editor/Resources/ND_BehaviorTree_Settings.asset";

        [Tooltip("The UXML file to use for the default appearance of nodes in the editor.")]
        [SerializeField]
        private VisualTreeAsset defaultNodeUXML;

        [Tooltip("The USS file for the main GraphView's appearance.")]
        [SerializeField]
        private StyleSheet graphViewStyle;

        [Header("Node Style Sheets")]
        [Tooltip("Dictionary mapping node types to their corresponding USS files.")]
        [SerializeField]
        private List<StyleSheetEntry> _styleSheets;


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
                        Debug.LogWarning($"Created new ND_BehaviorTreeSetting at: {SettingsAssetPath}. Please configure it.");
                    }
                }
                return _instance;
            }
        }


        #region USS

        public string GetStyleSheetPath(string nodeType)
        {
            if (TryGetStyleSheet(nodeType, out StyleSheet styleSheet) && styleSheet != null)
            {
                return AssetDatabase.GetAssetPath(styleSheet);
            }
            return null;
        }

        #endregion

        #region Graph

        public string GetGraphViewStyleSheetPath()
        {
            if (graphViewStyle == null) return null;
            return AssetDatabase.GetAssetPath(graphViewStyle);
        }

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
            Selection.activeObject = Instance;
            if (Instance != null) EditorGUIUtility.PingObject(Instance);
        }

        #endregion

        #region Ults

        public bool TryGetStyleSheet(string nodeType, out StyleSheet styleSheet)
        {
            styleSheet = null;
            if (string.IsNullOrEmpty(nodeType))
                return false;

            foreach (var entry in _styleSheets)
            {
                if (entry.nodeType == nodeType && entry.styleSheet != null)
                {
                    styleSheet = entry.styleSheet;
                    return true;
                }
            }
            return false;
        }

        #endregion
    }
}