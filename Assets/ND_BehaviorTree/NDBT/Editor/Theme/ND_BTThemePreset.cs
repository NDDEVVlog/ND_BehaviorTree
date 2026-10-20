using UnityEngine;

namespace ND_BehaviorTree.Editor.Theme
{
    /// <summary>
    /// A custom graph theme (node stylesheet, canvas colours, edges, ports). Every preset asset in the project appears
    /// in the graph window's Theme menu next to the built-in themes. Create one from the current look with
    /// Theme ▸ Save Current as Preset…, or via Create ▸ ND_BehaviorTree ▸ Theme Preset.
    /// </summary>
    [CreateAssetMenu(fileName = "New Theme Preset", menuName = "ND_BehaviorTree/Theme Preset")]
    public class ND_BTThemePreset : ScriptableObject
    {
        public ThemeData theme = new ThemeData();
    }
}
