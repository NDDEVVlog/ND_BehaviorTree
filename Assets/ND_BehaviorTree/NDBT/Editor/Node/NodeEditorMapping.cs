using System;
using ND.GraphUI;
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

        [Tooltip("Give this node type its own port look instead of the project's Port Theme.")]
        public bool overridePorts;
        public PortTheme ports = new PortTheme();

        [HideInInspector] 
        public string nodeTypeFullName;
    }
}
