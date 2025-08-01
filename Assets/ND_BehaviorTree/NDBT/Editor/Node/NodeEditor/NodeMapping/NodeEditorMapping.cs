using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using System;
using System.Collections.Generic;

namespace ND_BehaviorTree.Editor
{
    [System.Serializable]
    public class NodeEditorMapping
    {
        [Tooltip("Drag the Node data script file here (e.g., GOAPActionNode.cs).")]
        public MonoScript nodeScript;

        public string styleSheetStringEntry;
        
        // The UXML Asset field has been removed as requested.
        [HideInInspector] public string nodeTypeFullName;

    }
    
        [Serializable]
        class StyleSheetEntry
        {
            public string nodeType;
            public StyleSheet styleSheet;
        }
}