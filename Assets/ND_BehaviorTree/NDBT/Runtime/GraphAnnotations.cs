using System;
using System.Collections.Generic;
using UnityEngine;

namespace ND_BehaviorTree
{
    /// <summary>
    /// Editor-only annotation: a titled, coloured frame around related nodes.
    /// Stored on the BehaviorTree asset but never read at runtime; it does not affect execution or connectivity.
    /// </summary>
    [Serializable]
    public class GraphGroup
    {
        public string id = Guid.NewGuid().ToString();
        public string title = "Group";
        public Rect rect = new Rect(0, 0, 300, 200);
        public Color color = new Color(0.25f, 0.45f, 0.75f, 0.25f);
        public List<string> nodeIds = new List<string>();
    }

    /// <summary>
    /// Editor-only annotation: a sticky note. Never read at runtime; it does not affect execution or connectivity.
    /// </summary>
    [Serializable]
    public class GraphNote
    {
        public string id = Guid.NewGuid().ToString();
        public string title = "Note";
        [TextArea] public string content = string.Empty;
        public Rect rect = new Rect(0, 0, 220, 140);
        public Color color = new Color(0.95f, 0.85f, 0.35f, 0.95f);
    }
}
