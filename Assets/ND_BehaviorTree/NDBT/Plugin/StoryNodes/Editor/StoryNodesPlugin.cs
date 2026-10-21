using System.Collections.Generic;
using System.Linq;
using ND_BehaviorTree.Editor.Graph;
using ND_BehaviorTree.Editor.Plugins;

namespace ND_BehaviorTree.Story.Editor
{
    /// <summary>Entry point of the Story Nodes plugin: everything it adds to the editor is declared here.</summary>
    public sealed class StoryNodesPlugin : ND_BTPlugin
    {
        public const string CaseOutOfSync = "story.switch-cases-out-of-sync";

        public override string DisplayName => "Story Nodes";
        public override string Version => "1.0.0";
        public override string Description => "Unity Behavior style nodes: sentence nodes with inline values, action Blocks, Switch with generated Cases, Random.";

        public override void Register(ND_BTPluginContext context)
        {
            context.AddTheme(StoryTheme.Name, StoryTheme.Build);

            // Cases follow the Switch's list inside the edit that changed it (one undo step).
            context.OnNodeCreated<StorySwitchNode>((graph, node) => StorySwitchCases.Sync(graph, node));
            context.OnNodeEdited<StorySwitchNode>((graph, node) => StorySwitchCases.Sync(graph, node));

            context.AddNodeMenuItem<StorySwitchNode>("Story/Sync Cases",
                args => StorySwitchCases.Sync(args.Graph, (StorySwitchNode)args.Node),
                args => !StorySwitchCases.InSync((StorySwitchNode)args.Node));

            context.AddNodePropertiesButton<StorySwitchNode>("Sync Cases",
                args => StorySwitchCases.Sync(args.Graph, (StorySwitchNode)args.Node),
                args => !StorySwitchCases.InSync((StorySwitchNode)args.Node));

            context.AddNodeMenuItem<StorySwitchNode>("Story/Add Case",
                args => StorySwitchCases.AddCase(args.Graph, (StorySwitchNode)args.Node));

            context.AddValidationRule("Switch cases", CheckSwitches);
        }

        public const string CaseIgnored = "story.switch-case-ignored";

        private static IEnumerable<GraphIssue> CheckSwitches(BehaviorTree tree)
        {
            foreach (var s in tree.nodes.OfType<StorySwitchNode>())
            {
                if (!StorySwitchCases.InSync(s))
                    yield return new GraphIssue(GraphIssueSeverity.Warning, CaseOutOfSync,
                        $"Switch '{s.name}': its Case nodes do not match the 'Cases' list (right-click > Story > Sync Cases).", s);
                var ignored = StorySwitchCases.IgnoredEntries(s);
                if (ignored.Count > 0)
                    yield return new GraphIssue(GraphIssueSeverity.Warning, CaseIgnored,
                        $"Switch '{s.name}': 'Cases' entries {string.Join(", ", ignored.Select(e => $"'{e}'"))} are empty or repeated and make no Case node. Give each entry a unique value.", s);
            }
        }
    }
}
