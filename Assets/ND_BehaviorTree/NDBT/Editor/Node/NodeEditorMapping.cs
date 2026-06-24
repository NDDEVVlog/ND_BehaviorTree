using System;
using UnityEditor;
using UnityEngine;

namespace ND_BehaviorTree.Editor
{
    [Serializable]
    public class NodeEditorMapping
    {
        public MonoScript nodeScript;
        public MonoScript customViewScript;
        public string styleSheetKey;
        public string uxmlKey;
        
        [HideInInspector] 
        public string nodeTypeFullName;
    }
}