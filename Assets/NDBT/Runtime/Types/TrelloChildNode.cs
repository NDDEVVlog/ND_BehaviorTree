using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ND_BehaviorTree
{
    [NodeInfo("TrelloChild", "Trello/TrelloChild")]
    public class TrelloChildNode : Node
    {
        [System.Serializable]
        public struct CheckBox
        {
            [Multiline]
            public string text;
            public bool isChecked;
        }

        [System.Serializable]
        public struct ScriptRef
        {
            public MonoScript targetScript;
            public int line;
        }
        public string task;
        public bool isComplete = false;
        public string Description;
        [SerializeField]
        public List<CheckBox> checkBoxes = new List<CheckBox>();

        [SerializeField]
        public List<ScriptRef> scriptRefs = new List<ScriptRef>();

        protected override Status OnProcess()
        {
            throw new System.NotImplementedException();
        }

    }
}
