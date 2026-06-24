using System;
using UnityEngine.UIElements;

namespace ND_BehaviorTree.Editor
{
    [Serializable]
    public class StyleSheetEntry
    {
        public string styleKey;
        public StyleSheet styleSheet;
    }

    [Serializable]
    public class UxmlEntry
    {
        public string uxmlKey;
        public VisualTreeAsset uxmlAsset;
    }
}