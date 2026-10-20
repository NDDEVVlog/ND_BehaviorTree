using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ND_BehaviorTree.Editor.Graph
{
    /// <summary>
    /// Upgrades BehaviorTree assets to <see cref="BehaviorTree.CurrentSchemaVersion"/> when they are opened in the editor.
    ///
    /// v0 → v1: standalone decorator nodes (parent → D1 → D2 → node) become decorators attached to the node
    /// they wrap (node.decorators = [D1, D2]). The parent keeps the node at the same list index, so execution
    /// order and runtime behaviour are unchanged: BehaviorTree.Clone() rebuilds the same chain.
    /// Chains that cannot be attached (no target node, broken graph) are left
    /// standalone and reported; nothing is deleted.
    ///
    /// Before writing, the original asset file is copied to &lt;project&gt;/NDBT_MigrationBackups/ (outside Assets).
    /// Migration is a data upgrade and is not undoable; the backup is the rollback path.
    /// </summary>
    public static class BehaviorTreeMigrator
    {
        private const string LogTag = "[BehaviorTreeMigrator]";
        public const string BackupFolderName = "NDBT_MigrationBackups";

        /// <summary>Returns true if the tree was changed.</summary>
        public static bool MigrateIfNeeded(BehaviorTree tree)
        {
            if (tree == null) return false;
            if (tree.SchemaVersion == BehaviorTree.CurrentSchemaVersion) return false;
            if (tree.SchemaVersion > BehaviorTree.CurrentSchemaVersion)
            {
                Debug.LogError($"{LogTag} '{tree.name}' has schema v{tree.SchemaVersion}, newer than this editor (v{BehaviorTree.CurrentSchemaVersion}). Not migrating; edits may lose data.", tree);
                return false;
            }

            bool persistent = EditorUtility.IsPersistent(tree);
            int fromVersion = tree.SchemaVersion;
            var report = new List<string>();

            string backupPath = null;
            if (persistent && NeedsDataChanges(tree))
            {
                backupPath = Backup(tree, fromVersion);
                if (backupPath == null) return false; // Backup failed: never migrate without a rollback path.
            }

            if (tree.SchemaVersion < 1) MigrateV0ToV1(tree, report);
            if (tree.SchemaVersion < 2) MigrateV1ToV2(tree, report);

            tree.SchemaVersion = BehaviorTree.CurrentSchemaVersion;
            EditorUtility.SetDirty(tree);
            if (persistent) AssetDatabase.SaveAssetIfDirty(tree);

            if (report.Count > 0)
            {
                Debug.Log($"{LogTag} Migrated '{tree.name}' v{fromVersion} → v{BehaviorTree.CurrentSchemaVersion}" +
                          (backupPath != null ? $" (backup: {backupPath})" : "") + ":\n  - " + string.Join("\n  - ", report), tree);
            }
            return true;
        }

        private static bool NeedsDataChanges(BehaviorTree tree)
        {
            return (tree.SchemaVersion < 1 && tree.nodes.Any(n => n is DecoratorNode))
                || (tree.SchemaVersion < 2 && FindAttachmentsListedAsNodes(tree).Count > 0);
        }

        /// <summary>
        /// v1 → v2: a service/decorator that is attached to a node must not also be a graph node in m_nodes.
        /// Older editors left such duplicates; the canvas then drew the attachment as a free node, and deleting
        /// either copy destroyed an object the other still referenced (broken PPtr on save). The attachment is kept;
        /// only the duplicate m_nodes entry is removed.
        /// </summary>
        public static void MigrateV1ToV2(BehaviorTree tree, List<string> report)
        {
            foreach (var attachment in FindAttachmentsListedAsNodes(tree))
            {
                tree.nodes.Remove(attachment);
                report.Add($"'{attachment.name}' is attached to a node; removed its duplicate entry from the graph node list.");
            }
            tree.nodes.RemoveAll(n => n == null);
        }

        private static List<Node> FindAttachmentsListedAsNodes(BehaviorTree tree)
        {
            var attached = new HashSet<Node>();
            foreach (var node in tree.nodes.Where(n => n != null))
            {
                foreach (var s in node.services) if (s != null) attached.Add(s);
                foreach (var d in node.decorators) if (d != null) attached.Add(d);
            }
            return tree.nodes.Where(n => n != null && attached.Contains(n)).ToList();
        }

        public static void MigrateV0ToV1(BehaviorTree tree, List<string> report)
        {
            var parentOf = new Dictionary<Node, Node>();
            var parentCount = new Dictionary<Node, int>();
            foreach (var node in tree.nodes.Where(n => n != null))
            {
                foreach (var child in node.GetChildren().Where(c => c != null))
                {
                    if (!parentOf.ContainsKey(child)) parentOf[child] = node;
                    parentCount.TryGetValue(child, out int c);
                    parentCount[child] = c + 1;
                }
            }

            var chainTops = tree.nodes.OfType<DecoratorNode>()
                .Where(d => !(parentOf.TryGetValue(d, out var p) && p is DecoratorNode))
                .ToList();

            foreach (var top in chainTops)
            {
                var chain = new List<DecoratorNode>();
                Node current = top;
                while (current is DecoratorNode d && !chain.Contains(d))
                {
                    chain.Add(d);
                    current = d.child;
                }
                string chainNames = string.Join(" → ", chain.Select(c => c.name));
                Node target = current;

                string skipReason = null;
                if (target == null) skipReason = "the chain has no child node to attach to";
                else if (target is DecoratorNode) skipReason = "the chain loops back on itself";
                else if (target is RootNode) skipReason = "the chain wraps a RootNode";
                else if (!tree.nodes.Contains(target)) skipReason = $"target '{target.name}' is not in this tree";
                else if (parentCount.TryGetValue(target, out int targetParents) && targetParents > 1) skipReason = $"target '{target.name}' has {targetParents} parents";
                else if (chain.Skip(1).Any(d => parentCount.TryGetValue(d, out int n) && n > 1)) skipReason = "a decorator inside the chain has several parents";

                if (skipReason != null)
                {
                    report.Add($"SKIPPED [{chainNames}]: {skipReason}. Left as standalone node(s).");
                    continue;
                }

                if (parentOf.TryGetValue(top, out var parent))
                {
                    if (parent is CompositeNode composite)
                    {
                        int index = composite.children.IndexOf(top);
                        composite.children[index] = target;
                    }
                    else
                    {
                        parent.RemoveChild(top);
                        parent.AddChild(target);
                    }
                    parentOf[target] = parent;
                    EditorUtility.SetDirty(parent);
                }

                target.decorators.InsertRange(0, chain);
                foreach (var d in chain)
                {
                    d.child = null;
                    tree.nodes.Remove(d);
                    EditorUtility.SetDirty(d);
                }
                EditorUtility.SetDirty(target);
                report.Add($"[{chainNames}] attached to '{target.name}'" + (parent != null ? $" under '{parent.name}'" : " (no parent)"));
            }
        }

        private static string Backup(BehaviorTree tree, int fromVersion)
        {
            try
            {
                string assetPath = AssetDatabase.GetAssetPath(tree);
                string projectRoot = Path.GetDirectoryName(Application.dataPath);
                string source = Path.Combine(projectRoot, assetPath);
                string folder = Path.Combine(projectRoot, BackupFolderName);
                Directory.CreateDirectory(folder);
                string dest = Path.Combine(folder, $"{Path.GetFileNameWithoutExtension(assetPath)}_v{fromVersion}_{DateTime.Now:yyyyMMdd_HHmmss}.asset");
                File.Copy(source, dest, overwrite: false);
                return dest.Replace('\\', '/');
            }
            catch (Exception e)
            {
                Debug.LogError($"{LogTag} Could not back up '{tree.name}' before migration; migration skipped. Error: {e.Message}", tree);
                return null;
            }
        }
    }
}
