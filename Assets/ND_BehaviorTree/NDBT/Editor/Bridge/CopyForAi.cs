using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;

namespace ND_BehaviorTree.Editor.Bridge
{
    /// <summary>
    /// "Copy for AI": puts a tree's path/GUID (or selected node ids) on the clipboard with the command to read it, so
    /// the user can paste it into a prompt without typing paths. Offered (graph toolbar, node menu, tree Inspector)
    /// only while AI features are enabled in the ND Behavior Tree settings.
    /// </summary>
    public static class CopyForAi
    {
        public static string TreeText(BehaviorTree tree)
        {
            string path = AssetDatabase.GetAssetPath(tree);
            string guid = AssetDatabase.AssetPathToGUID(path);
            return $"ND Behavior Tree \"{tree.name}\"\n" +
                   $"  path: {path}\n" +
                   $"  guid: {guid}\n" +
                   $"  read:  tree_get {{\"tree\": \"{path}\", \"format\": \"outline\"}}   (CLI: ndbt tree-get --tree \"{path}\" --format outline)\n" +
                   $"  edit:  tree_apply {{\"tree\": \"{path}\", \"ops\": [...], \"dryRun\": true}}";
        }

        public static string NodesText(BehaviorTree tree, IEnumerable<Node> nodes)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Tree: {AssetDatabase.GetAssetPath(tree)}");
            sb.AppendLine("Nodes:");
            foreach (var node in nodes.Where(n => n != null))
            {
                string title = string.IsNullOrEmpty(node.typeName) ? BridgeUtil.Title(node.GetType()) : node.typeName;
                sb.AppendLine($"  - {title} ({node.GetType().Name}) id: {node.id}");
            }
            return sb.ToString();
        }
    }
}
