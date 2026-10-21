using ND.GraphUI;
using ND_BehaviorTree.Editor;
using ND_BehaviorTree.Editor.CustomGraph;
using UnityEngine;

namespace ND_BehaviorTree.Story.Editor
{
    /// <summary>The "Story" theme (Unity Behavior look: dark canvas, white step edges, small round ports); registered by <see cref="StoryNodesPlugin"/>.</summary>
    public static class StoryTheme
    {
        public const string Name = "Story";

        private static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;

        public static ThemeData Build() => new ThemeData
        {
            displayName = Name,
            nodeStyle = ND_BTStyles.Find("NodeElementUss"),
            graphBackgroundColor = Hex("#262626"),
            gridColor = Hex("#303030"),
            edge = new EdgeTheme
            {
                edgeColor = Hex("#E6E6E6"), edgeThickness = 2f, curve = EdgeCurve.Step,
                labelBackgroundColor = Hex("#2A2A2AE6"), labelTextColor = Hex("#FFFFFF"),
            },
            port = new PortTheme { inputPortColor = Hex("#E6E6E6"), outputPortColor = Hex("#E6E6E6"), portSize = 10f, shape = PortShape.Circle },
        };
    }
}
