using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND_BehaviorTree.Editor.CustomGraph
{
    /// <summary>
    /// Editor stylesheets that are always applied on top of the configurable node/blackboard themes.
    /// Found by file name anywhere in the project (so moving the package folder keeps working), cached.
    /// </summary>
    public static class ND_BTStyles
    {
        private static readonly Dictionary<string, StyleSheet> s_Cache = new Dictionary<string, StyleSheet>();
        private static readonly HashSet<string> s_Reported = new HashSet<string>();

        public static StyleSheet NodeAttachments => Load("ND_NodeAttachments");
        public static StyleSheet Blackboard => Load("ND_Blackboard");
        public static StyleSheet Reroute => Load("ND_Reroute");
        public static StyleSheet LiveBlackboard => Load("ND_LiveBlackboard");

        public static void Apply(VisualElement element, StyleSheet sheet)
        {
            if (sheet != null && !element.styleSheets.Contains(sheet)) element.styleSheets.Add(sheet);
        }

        private static StyleSheet Load(string fileName)
        {
            if (s_Cache.TryGetValue(fileName, out var cached) && cached != null) return cached;

            StyleSheet sheet = null;
            foreach (var guid in AssetDatabase.FindAssets($"{fileName} t:StyleSheet"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) != fileName) continue;
                sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                break;
            }

            if (sheet == null && s_Reported.Add(fileName))
                Debug.LogError($"[ND_BTStyles] Stylesheet '{fileName}.uss' was not found in the project. Graph sections will render unstyled.");
            s_Cache[fileName] = sheet;
            return sheet;
        }
    }
}
