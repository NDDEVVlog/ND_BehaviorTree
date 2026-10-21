using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND.Dialogue.Editor
{
    /// <summary>Editor stylesheets of the dialogue editor, found by file name anywhere in the project (moving the folder keeps working).</summary>
    public static class DialogueEditorStyles
    {
        private static StyleSheet s_Graph;
        private static bool s_Reported;

        public static StyleSheet Graph
        {
            get
            {
                if (s_Graph != null) return s_Graph;
                foreach (var guid in AssetDatabase.FindAssets("DialogueGraph t:StyleSheet"))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (System.IO.Path.GetFileNameWithoutExtension(path) != "DialogueGraph") continue;
                    s_Graph = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                    break;
                }
                if (s_Graph == null && !s_Reported)
                {
                    s_Reported = true;
                    Debug.LogError("[Dialogue Editor] DialogueGraph.uss was not found; nodes will render unstyled.");
                }
                return s_Graph;
            }
        }
    }
}
