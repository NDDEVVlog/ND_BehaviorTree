using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ND_BehaviorTree.Editor.Graph
{
    /// <summary>
    /// Headless graph-editing API for a BehaviorTree. All graph mutations from the editor (and later tools
    /// and tests) go through here so they share the same validation, ordering and undo rules.
    ///
    /// Every public mutating method:
    ///   - validates its input and logs + returns false/null on rejection (no silent no-ops),
    ///   - produces exactly ONE step in the tree's GraphHistory (Unity's Undo is not used for graph edits),
    ///   - marks touched objects dirty. Call <see cref="Save"/> to write the asset.
    /// Works on in-memory trees too (no sub-asset registration), which is what tests use.
    /// </summary>
    public sealed class BehaviorTreeGraphService
    {
        private const string LogTag = "[BehaviorTreeGraphService]";

        public BehaviorTree Tree { get; }
        private bool IsPersistent => EditorUtility.IsPersistent(Tree);

        /// <summary>Undo/redo history of this tree's graph edits.</summary>
        public GraphHistory History => GraphHistory.For(Tree);

        public BehaviorTreeGraphService(BehaviorTree tree)
        {
            Tree = tree ?? throw new ArgumentNullException(nameof(tree));
        }

        #region Port rules

        public static bool HasInputPort(Node node) => node != null && (GetInfo(node)?.hasFlowInput ?? false);
        public static bool HasOutputPort(Node node) => node != null && (GetInfo(node)?.hasFlowOutput ?? false);
        public static bool AcceptsMultipleChildren(Node node) => node is CompositeNode;

        private static NodeInfoAttribute GetInfo(Node node) => node.GetType().GetCustomAttribute<NodeInfoAttribute>();

        #endregion

        #region Queries

        public Node GetParent(Node child)
        {
            if (child == null) return null;
            foreach (var node in Tree.nodes)
            {
                if (node != null && node.GetChildren().Contains(child)) return node;
            }
            return null;
        }

        public bool CanConnect(Node parent, Node child, out string reason) => CanConnect(parent, child, out reason, allowOwned: false);

        /// <param name="allowOwned">
        /// True for tools that manage an <see cref="IOwnedChildren"/> composite's children (Switch case generation, Block
        /// rows, AI import). Users cannot connect children to such composites by hand.
        /// </param>
        public bool CanConnect(Node parent, Node child, out string reason, bool allowOwned)
        {
            reason = null;
            if (parent == null || child == null) reason = "parent or child is null";
            else if (parent == child) reason = "a node cannot connect to itself";
            else if (!Tree.nodes.Contains(parent) || !Tree.nodes.Contains(child)) reason = "both nodes must belong to this tree";
            else if (parent is IOwnedChildren && !allowOwned) reason = $"'{parent.name}' manages its own children (add them from the node itself)";
            else if (!HasOutputPort(parent) && !(allowOwned && parent is IOwnedChildren)) reason = $"'{parent.name}' has no output port";
            else if (!HasInputPort(child)) reason = $"'{child.name}' has no input port";
            else if (parent.GetChildren().Contains(child)) reason = "already connected";
            else if (GetParent(child) != null) reason = $"'{child.name}' already has a parent";
            else if (!AcceptsMultipleChildren(parent) && parent.GetChildren().Count > 0) reason = $"'{parent.name}' accepts a single child";
            else if (GraphValidator.CollectReachable(child).Contains(parent)) reason = "connection would create a cycle";
            return reason == null;
        }

        #endregion

        #region Mutations

        public Node CreateNode(Type nodeType, Vector2 position)
        {
            if (nodeType == null || nodeType.IsAbstract || !typeof(Node).IsAssignableFrom(nodeType)
                || typeof(ServiceNode).IsAssignableFrom(nodeType) || typeof(DecoratorNode).IsAssignableFrom(nodeType))
            {
                Debug.LogError($"{LogTag} {nameof(CreateNode)} rejected: type={nodeType?.FullName ?? "null"} is not a concrete graph Node type (services and decorators are attached to nodes, not created standalone).");
                return null;
            }
            if (typeof(InitNode).IsAssignableFrom(nodeType) && Tree.nodes.Any(n => n is InitNode))
            {
                Debug.LogWarning($"{LogTag} {nameof(CreateNode)} rejected: tree '{Tree.name}' already has an Init node (only one is allowed).");
                return null;
            }

            using (new GraphUndoScope(Tree, $"Create {nodeType.Name}"))
            {
                var node = (Node)ScriptableObject.CreateInstance(nodeType);
                node.name = nodeType.Name;
                node.SetPosition(new Rect(position, Vector2.zero));
                RegisterNewObject(node);

                Tree.nodes.Add(node);
                EditorUtility.SetDirty(Tree);
                return node;
            }
        }

        public bool Connect(Node parent, Node child) => Connect(parent, child, allowOwned: false);

        /// <summary>Connects; <paramref name="allowOwned"/> lets owner tools add children to an <see cref="IOwnedChildren"/> composite.</summary>
        public bool Connect(Node parent, Node child, bool allowOwned)
        {
            if (!CanConnect(parent, child, out string reason, allowOwned))
            {
                Debug.LogWarning($"{LogTag} {nameof(Connect)} rejected: parent={parent?.name}, child={child?.name}, reason={reason}");
                return false;
            }

            using (new GraphUndoScope(Tree, "Connect Nodes"))
            {
                parent.AddChild(child);
                if (parent is CompositeNode composite) ExecutionOrder.SortChildren(composite);
                EditorUtility.SetDirty(parent);
            }
            return true;
        }

        /// <summary>
        /// Splits the edge parent → child with a <see cref="RerouteNode"/> at <paramref name="position"/>, keeping the
        /// child's slot in the parent's children list (execution order is unchanged). One history step.
        /// </summary>
        public RerouteNode InsertReroute(Node parent, Node child, Vector2 position)
        {
            if (parent == null || child == null || !parent.GetChildren().Contains(child))
            {
                Debug.LogWarning($"{LogTag} {nameof(InsertReroute)} rejected: parent={parent?.name}, child={child?.name} are not connected.");
                return null;
            }
            if (parent is IOwnedChildren)
            {
                // The owner manages this list (e.g. a Switch keeps exactly its Case nodes): a reroute in it would break that.
                Debug.LogWarning($"{LogTag} {nameof(InsertReroute)} rejected: '{parent.name}' ({parent.GetType().Name}) manages its children; reroutes cannot be inserted under it.");
                return null;
            }

            using (new GraphUndoScope(Tree, "Insert Reroute"))
            {
                var reroute = ScriptableObject.CreateInstance<RerouteNode>();
                reroute.name = nameof(RerouteNode);
                reroute.SetPosition(new Rect(position, Vector2.zero));
                RegisterNewObject(reroute);
                Tree.nodes.Add(reroute);

                if (parent is CompositeNode composite) composite.children[composite.children.IndexOf(child)] = reroute;
                else
                {
                    parent.RemoveChild(child);
                    parent.AddChild(reroute);
                }
                reroute.child = child;

                EditorUtility.SetDirty(parent);
                EditorUtility.SetDirty(reroute);
                EditorUtility.SetDirty(Tree);
                return reroute;
            }
        }

        public bool Disconnect(Node parent, Node child)
        {
            if (parent == null || child == null || !parent.GetChildren().Contains(child))
            {
                Debug.LogWarning($"{LogTag} {nameof(Disconnect)} rejected: parent={parent?.name}, child={child?.name} are not connected.");
                return false;
            }

            using (new GraphUndoScope(Tree, "Disconnect Nodes"))
            {
                parent.RemoveChild(child);
                EditorUtility.SetDirty(parent);
            }
            return true;
        }

        /// <summary>
        /// Commits final positions for a finished drag. Parents of moved nodes are re-sorted left-to-right
        /// in the same undo step (see <see cref="ExecutionOrder"/>).
        /// </summary>
        public void MoveNodes(IReadOnlyDictionary<Node, Vector2> newPositions)
        {
            var moved = newPositions.Where(kvp => kvp.Key != null && kvp.Key.position.position != kvp.Value).ToList();
            if (moved.Count == 0) return;

            var parents = moved.Select(kvp => GetParent(kvp.Key)).OfType<CompositeNode>().Distinct().ToList();
            var touched = moved.Select(kvp => (Object)kvp.Key).Concat(parents).Append(Tree).Distinct().ToArray();

            using (new GraphUndoScope(Tree, "Move Nodes"))
            {
                foreach (var kvp in moved)
                {
                    var rect = kvp.Key.position;
                    rect.position = kvp.Value;
                    kvp.Key.SetPosition(rect);
                    UpdateGroupMembership(kvp.Key);
                }
                foreach (var parent in parents) ExecutionOrder.SortChildren(parent);
                foreach (var obj in touched) EditorUtility.SetDirty(obj);
            }
        }

        /// <summary>
        /// A node belongs to the group whose rect contains the node's position (its top-left anchor).
        /// Staying inside its current group wins over overlapping groups. Caller must have recorded Tree.
        /// </summary>
        private void UpdateGroupMembership(Node node)
        {
            var current = Tree.groups.FirstOrDefault(g => g.nodeIds.Contains(node.id));
            Vector2 anchor = node.position.position;
            if (current != null && current.rect.Contains(anchor)) return;

            current?.nodeIds.Remove(node.id);
            var target = Tree.groups.FirstOrDefault(g => g.rect.Contains(anchor));
            target?.nodeIds.Add(node.id);
        }

        public GraphGroup FindGroupOf(Node node) => node == null ? null : Tree.groups.FirstOrDefault(g => g.nodeIds.Contains(node.id));

        #endregion

        #region Groups & notes (editor-only annotations)

        public GraphGroup CreateGroup(string title, Rect rect, IEnumerable<Node> members)
        {
            var group = new GraphGroup { title = string.IsNullOrEmpty(title) ? "Group" : title, rect = rect };
            using (new GraphUndoScope(Tree, "Create Group"))
            {
                foreach (var node in members.Where(n => n != null && Tree.nodes.Contains(n)).Distinct())
                {
                    FindGroupOf(node)?.nodeIds.Remove(node.id);
                    group.nodeIds.Add(node.id);
                }
                Tree.groups.Add(group);
                EditorUtility.SetDirty(Tree);
            }
            return group;
        }

        public bool DeleteGroup(GraphGroup group)
        {
            if (!ValidateGroup(group, nameof(DeleteGroup))) return false;
            using (new GraphUndoScope(Tree, "Delete Group"))
            {
                Tree.groups.Remove(group);
                EditorUtility.SetDirty(Tree);
            }
            return true;
        }

        public bool UpdateGroup(GraphGroup group, string title, Color color)
        {
            if (!ValidateGroup(group, nameof(UpdateGroup))) return false;
            using (new GraphUndoScope(Tree, "Edit Group"))
            {
                group.title = title;
                group.color = color;
                EditorUtility.SetDirty(Tree);
            }
            return true;
        }

        /// <summary>Changes the group's bounds without moving nodes. Membership is unchanged.</summary>
        public bool ResizeGroup(GraphGroup group, Rect rect)
        {
            if (!ValidateGroup(group, nameof(ResizeGroup))) return false;
            using (new GraphUndoScope(Tree, "Resize Group"))
            {
                group.rect = rect;
                EditorUtility.SetDirty(Tree);
            }
            return true;
        }

        /// <summary>Moves the group and every member node by the same delta (one undo step, parents re-sorted).</summary>
        public bool MoveGroup(GraphGroup group, Vector2 newPosition)
        {
            if (!ValidateGroup(group, nameof(MoveGroup))) return false;
            Vector2 delta = newPosition - group.rect.position;
            if (delta == Vector2.zero) return true;

            var members = Tree.nodes.Where(n => n != null && group.nodeIds.Contains(n.id)).ToList();
            var parents = members.Select(GetParent).OfType<CompositeNode>().Distinct().ToList();
            var touched = members.Cast<Object>().Concat(parents).Append(Tree).Distinct().ToArray();

            using (new GraphUndoScope(Tree, "Move Group"))
            {
                group.rect.position = newPosition;
                foreach (var node in members)
                {
                    var rect = node.position;
                    rect.position += delta;
                    node.SetPosition(rect);
                }
                foreach (var parent in parents) ExecutionOrder.SortChildren(parent);
                foreach (var obj in touched) EditorUtility.SetDirty(obj);
            }
            return true;
        }

        public GraphNote CreateNote(Rect rect, string title = "Note", string content = "")
        {
            var note = new GraphNote { rect = rect, title = title, content = content };
            using (new GraphUndoScope(Tree, "Create Note"))
            {
                Tree.notes.Add(note);
                EditorUtility.SetDirty(Tree);
            }
            return note;
        }

        public bool DeleteNote(GraphNote note)
        {
            if (!ValidateNote(note, nameof(DeleteNote))) return false;
            using (new GraphUndoScope(Tree, "Delete Note"))
            {
                Tree.notes.Remove(note);
                EditorUtility.SetDirty(Tree);
            }
            return true;
        }

        public bool UpdateNote(GraphNote note, string title, string content, Color color, Rect rect)
        {
            if (!ValidateNote(note, nameof(UpdateNote))) return false;
            if (note.title == title && note.content == content && note.color == color && note.rect == rect) return true;
            using (new GraphUndoScope(Tree, "Edit Note"))
            {
                note.title = title;
                note.content = content;
                note.color = color;
                note.rect = rect;
                EditorUtility.SetDirty(Tree);
            }
            return true;
        }

        private bool ValidateGroup(GraphGroup group, string op)
        {
            if (group != null && Tree.groups.Contains(group)) return true;
            Debug.LogWarning($"{LogTag} {op} rejected: group '{group?.title}' is not in tree '{Tree.name}'.");
            return false;
        }

        private bool ValidateNote(GraphNote note, string op)
        {
            if (note != null && Tree.notes.Contains(note)) return true;
            Debug.LogWarning($"{LogTag} {op} rejected: note '{note?.title}' is not in tree '{Tree.name}'.");
            return false;
        }

        #endregion

        #region Subtrees

        public bool CanAssignSubtree(SubtreeNode node, BehaviorTree subtree, out string reason)
        {
            reason = null;
            if (node == null || !Tree.nodes.Contains(node)) reason = "node is not in this tree";
            else if (subtree == Tree) reason = "a tree cannot run itself as a subtree";
            else if (subtree != null && GraphValidator.SubtreeReaches(subtree, Tree)) reason = $"'{subtree.name}' already runs '{Tree.name}' (recursive reference)";
            return reason == null;
        }

        public bool AssignSubtree(SubtreeNode node, BehaviorTree subtree)
        {
            if (!CanAssignSubtree(node, subtree, out string reason))
            {
                Debug.LogWarning($"{LogTag} {nameof(AssignSubtree)} rejected: node={node?.name}, subtree={subtree?.name}, reason={reason}");
                return false;
            }
            using (new GraphUndoScope(Tree, "Assign Subtree"))
            {
                node.subtree = subtree;
                EditorUtility.SetDirty(node);
            }
            return true;
        }

        /// <summary>
        /// Moves a selected branch into a new BehaviorTree asset and replaces it with a SubtreeNode.
        /// Rules: the selection must have exactly ONE top node (whose parent is not selected), must not contain the
        /// Root or Event nodes, and must include every child of every selected node (a closed branch).
        /// The new tree shares this tree's blackboard asset, so keys keep resolving by name.
        /// <paramref name="assetPath"/> = null creates the subtree in memory (tests / dry runs).
        /// The change to this tree is one undo step; the new asset file is not removed by undo.
        /// </summary>
        public SubtreeNode ConvertToSubtree(IEnumerable<Node> selection, string assetPath, out string error)
        {
            var set = selection.Where(n => n != null).Distinct().ToList();
            var setHash = new HashSet<Node>(set);
            var tops = set.Where(n => { var p = GetParent(n); return p == null || !setHash.Contains(p); }).ToList();
            var missingChildren = set.SelectMany(n => n.GetChildren()).Where(c => c != null && !setHash.Contains(c)).Distinct().ToList();

            error = null;
            if (set.Count == 0) error = "nothing is selected";
            else if (set.Any(n => !Tree.nodes.Contains(n))) error = "every selected node must belong to this tree";
            else if (set.Any(n => n is RootNode)) error = "the Root node cannot be moved into a subtree";
            else if (set.Any(n => n is EventNode)) error = "event nodes cannot be moved into a subtree (they could no longer be sent)";
            else if (set.Any(n => n is InitNode)) error = "the Init node cannot be moved into a subtree";
            else if (tops.Count != 1) error = $"the selection must be one branch with a single top node; found {tops.Count} top nodes: {string.Join(", ", tops.Select(t => t.name))}";
            else if (missingChildren.Count > 0) error = $"select the whole branch; these children are not selected: {string.Join(", ", missingChildren.Select(c => c.name))}";
            if (error != null)
            {
                Debug.LogWarning($"{LogTag} {nameof(ConvertToSubtree)} rejected: {error}");
                return null;
            }

            var top = tops[0];
            var parent = GetParent(top);
            int parentIndex = parent is CompositeNode parentComposite ? parentComposite.children.IndexOf(top) : -1;
            Vector2 topPosition = top.position.position;

            // 1. Build the subtree asset with a copy of the branch under its root.
            var subtree = ScriptableObject.CreateInstance<BehaviorTree>();
            subtree.name = assetPath != null ? System.IO.Path.GetFileNameWithoutExtension(assetPath) : $"{Tree.name}_Subtree";
            subtree.SchemaVersion = BehaviorTree.CurrentSchemaVersion;
            subtree.blackboard = Tree.blackboard;
            if (assetPath != null)
            {
                AssetDatabase.CreateAsset(subtree, assetPath);
                subtree.EditorInit();
            }
            else
            {
                var root = ScriptableObject.CreateInstance<RootNode>();
                root.name = "Root";
                root.SetPosition(new Rect(250, 100, 150, 100));
                subtree.nodes.Add(root);
                subtree.rootNode = root;
            }

            var subGraph = new BehaviorTreeGraphService(subtree);
            using (var clip = GraphClipboard.Capture(Tree, set))
            {
                Vector2 desiredTop = subtree.rootNode.position.position + new Vector2(0, 160);
                var pasted = subGraph.Paste(clip, clip.GetCenter() + (desiredTop - topPosition));
                subGraph.Connect(subtree.rootNode, pasted[clip.IndexOfSource(top)]);
            }
            subGraph.Save();

            // Building the new asset is not part of anyone's history: it starts clean.
            GraphHistory.For(subtree).Clear();

            // 2. Replace the branch with a SubtreeNode at the same place and child index (one undo step).
            using (new GraphUndoScope(Tree, "Convert to Subtree"))
            {
                DeleteNodes(set);
                var subtreeNode = (SubtreeNode)CreateNode(typeof(SubtreeNode), topPosition);
                AssignSubtree(subtreeNode, subtree);

                if (parent != null)
                {
                    if (parent is CompositeNode composite) composite.children.Insert(Mathf.Clamp(parentIndex, 0, composite.children.Count), subtreeNode);
                    else parent.AddChild(subtreeNode);
                    EditorUtility.SetDirty(parent);
                }
                return subtreeNode;
            }
        }

        /// <summary>
        /// Links a blackboard key to a Key field on a node or attachment (one undo step). Pass null to clear.
        /// Rejects keys that are not in this tree's blackboard and keys whose type the field does not accept.
        /// </summary>
        public bool AssignBlackboardKey(Node target, string propertyPath, Key key, out string reason)
        {
            reason = null;
            var slot = target != null ? KeySlotScanner.Scan(target).FirstOrDefault(s => s.PropertyPath == propertyPath) : null;
            if (target == null) reason = "target is null";
            else if (slot == null) reason = $"'{target.name}' has no blackboard key field '{propertyPath}'";
            else if (key != null && !KeySlotScanner.IsLinked(Tree, key)) reason = $"key '{key.keyName}' is not in the blackboard of '{Tree.name}'";
            else KeySlotScanner.IsCompatible(slot, key, out reason);

            if (reason != null)
            {
                Debug.LogWarning($"{LogTag} {nameof(AssignBlackboardKey)} rejected: target={target?.name}, field={propertyPath}, key={key?.keyName}, reason={reason}");
                return false;
            }

            using (new GraphUndoScope(Tree, key != null ? $"Link Key '{key.keyName}'" : "Clear Key"))
            {
                var so = new SerializedObject(target);
                var prop = so.FindProperty(propertyPath);
                if (prop == null || prop.propertyType != SerializedPropertyType.ObjectReference)
                {
                    reason = $"serialized property '{propertyPath}' not found";
                    Debug.LogError($"{LogTag} {nameof(AssignBlackboardKey)}: {reason} on '{target.name}'.");
                    return false;
                }
                prop.objectReferenceValue = key;
                so.ApplyModifiedPropertiesWithoutUndo(); // recorded by the graph history scope
                EditorUtility.SetDirty(target);
            }
            return true;
        }

        #endregion

        #region Mutations (structure, attachments, paste)

        /// <summary>Moves <paramref name="child"/> to <paramref name="newIndex"/> in its composite's children (Block row order, Switch cases).</summary>
        public bool MoveChild(CompositeNode parent, Node child, int newIndex)
        {
            int index = parent != null ? parent.children.IndexOf(child) : -1;
            if (index < 0 || newIndex < 0 || newIndex >= parent.children.Count)
            {
                Debug.LogWarning($"{LogTag} {nameof(MoveChild)} rejected: parent={parent?.name}, child={child?.name}, index={index}, newIndex={newIndex}");
                return false;
            }
            if (index == newIndex) return true;
            using (new GraphUndoScope(Tree, "Reorder Children"))
            {
                parent.children.RemoveAt(index);
                parent.children.Insert(newIndex, child);
                EditorUtility.SetDirty(parent);
            }
            return true;
        }

        public bool SortChildrenByPosition(CompositeNode composite)
        {
            if (composite == null) return false;
            using (new GraphUndoScope(Tree, "Sort Children Left-to-Right"))
            {
                bool changed = ExecutionOrder.SortChildren(composite);
                if (changed) EditorUtility.SetDirty(composite);
                return changed;
            }
        }

        /// <summary>Deletes nodes, their services and every edge touching them. RootNode is never deleted.</summary>
        public int DeleteNodes(IEnumerable<Node> nodes)
        {
            var targets = new List<Node>();
            foreach (var node in nodes.Where(n => n != null).Distinct())
            {
                if (node is RootNode)
                {
                    Debug.LogWarning($"{LogTag} {nameof(DeleteNodes)} skipped '{node.name}': the RootNode cannot be deleted.");
                    continue;
                }
                if (!Tree.nodes.Contains(node))
                {
                    Debug.LogWarning($"{LogTag} {nameof(DeleteNodes)} skipped '{node.name}': not in tree '{Tree.name}'.");
                    continue;
                }
                targets.Add(node);
            }
            // Children owned by a deleted node (Switch cases, Block rows) go with it.
            for (int i = 0; i < targets.Count; i++)
                if (targets[i] is IOwnedChildren)
                    foreach (var owned in targets[i].GetChildren())
                        if (owned != null && !targets.Contains(owned) && Tree.nodes.Contains(owned)) targets.Add(owned);
            if (targets.Count == 0) return 0;

            var targetSet = new HashSet<Node>(targets);
            var survivors = Tree.nodes.Where(n => n != null && !targetSet.Contains(n)).ToList();

            // Every surviving node that references a deleted node (as child, service or decorator) is updated, so no
            // reference is left pointing at a destroyed object (that is what produces "Broken text PPtr" on save).
            var referrers = survivors.Where(n => n.GetChildren().Any(targetSet.Contains)
                                                 || n.services.Any(s => targetSet.Contains(s))
                                                 || n.decorators.Any(d => targetSet.Contains(d))).ToList();

            // Attachments of deleted nodes are destroyed only if nothing that survives still references them.
            var attachments = targets.SelectMany(n => n.services.Cast<Node>().Concat(n.decorators))
                .Where(a => a != null && !targetSet.Contains(a)).Distinct()
                .Where(a => !survivors.Contains(a) && !survivors.Any(s => s.services.Contains(a as ServiceNode) || s.decorators.Contains(a as DecoratorNode)))
                .ToList();

            using (new GraphUndoScope(Tree, targets.Count == 1 ? "Delete Node" : "Delete Nodes"))
            {

                foreach (var referrer in referrers)
                {
                    // Deleting a reroute keeps the connection: the parent is re-linked (same slot) to the node behind it.
                    if (referrer is CompositeNode composite)
                    {
                        for (int i = composite.children.Count - 1; i >= 0; i--)
                        {
                            var child = composite.children[i];
                            if (!targetSet.Contains(child)) continue;
                            var bridge = BridgeThroughDeletedReroutes(child, targetSet);
                            if (bridge != null && !composite.children.Contains(bridge)) composite.children[i] = bridge;
                            else composite.children.RemoveAt(i);
                        }
                    }
                    foreach (var child in referrer.GetChildren().Where(targetSet.Contains).ToList())
                    {
                        var bridge = BridgeThroughDeletedReroutes(child, targetSet);
                        referrer.RemoveChild(child);
                        if (bridge != null) referrer.AddChild(bridge);
                    }
                    referrer.services.RemoveAll(s => targetSet.Contains(s));
                    referrer.decorators.RemoveAll(d => targetSet.Contains(d));
                    EditorUtility.SetDirty(referrer);
                }
                foreach (var group in Tree.groups) group.nodeIds.RemoveAll(id => targets.Any(t => t.id == id));

                foreach (var node in targets) Tree.nodes.Remove(node);
                foreach (var attachment in attachments) DestroySubObject(attachment);
                foreach (var node in targets) DestroySubObject(node);
                EditorUtility.SetDirty(Tree);
            }
            return targets.Count;
        }

        /// <summary>The surviving node behind a chain of reroutes that are all being deleted (null otherwise).</summary>
        private static Node BridgeThroughDeletedReroutes(Node node, HashSet<Node> deleted)
        {
            for (int guard = 0; guard < 64 && node is RerouteNode reroute && deleted.Contains(node); guard++) node = reroute.child;
            return node != null && !deleted.Contains(node) ? node : null;
        }

        public bool CanAttachDecorator(Node owner, out string reason)
        {
            reason = null;
            if (owner == null) reason = "owner is null";
            else if (!Tree.nodes.Contains(owner)) reason = $"'{owner.name}' is not in this tree";
            else if (owner is RerouteNode) reason = "a reroute is skipped at runtime; attach decorators to the node it leads to";
            else if (owner is DecoratorNode) reason = "decorators cannot be attached to a standalone decorator";
            else if (owner is ServiceNode) reason = "services cannot have decorators";
            return reason == null;
        }

        public DecoratorNode AddDecorator(Node owner, Type decoratorType)
        {
            if (decoratorType == null || decoratorType.IsAbstract || !typeof(DecoratorNode).IsAssignableFrom(decoratorType))
            {
                Debug.LogError($"{LogTag} {nameof(AddDecorator)} rejected: type={decoratorType?.FullName ?? "null"} is not a concrete DecoratorNode.");
                return null;
            }
            if (!CanAttachDecorator(owner, out string reason))
            {
                Debug.LogWarning($"{LogTag} {nameof(AddDecorator)} rejected: owner={owner?.name}, reason={reason}");
                return null;
            }

            using (new GraphUndoScope(Tree, $"Add {decoratorType.Name}"))
            {
                var decorator = (DecoratorNode)ScriptableObject.CreateInstance(decoratorType);
                decorator.name = decoratorType.Name;
                RegisterNewObject(decorator);
                owner.decorators.Add(decorator);
                EditorUtility.SetDirty(owner);
                return decorator;
            }
        }

        public bool RemoveDecorator(Node owner, DecoratorNode decorator)
        {
            if (owner == null || decorator == null || !owner.decorators.Contains(decorator))
            {
                Debug.LogWarning($"{LogTag} {nameof(RemoveDecorator)} rejected: owner={owner?.name}, decorator={decorator?.name} is not attached.");
                return false;
            }

            using (new GraphUndoScope(Tree, "Remove Decorator"))
            {
                owner.decorators.Remove(decorator);
                DestroySubObject(decorator);
                EditorUtility.SetDirty(owner);
            }
            return true;
        }

        /// <summary>Moves a decorator to <paramref name="newIndex"/> (0 = top = evaluated first).</summary>
        public bool MoveDecorator(Node owner, DecoratorNode decorator, int newIndex)
        {
            int oldIndex = owner != null ? owner.decorators.IndexOf(decorator) : -1;
            if (oldIndex < 0 || newIndex < 0 || newIndex >= owner.decorators.Count)
            {
                Debug.LogWarning($"{LogTag} {nameof(MoveDecorator)} rejected: owner={owner?.name}, decorator={decorator?.name}, oldIndex={oldIndex}, newIndex={newIndex}.");
                return false;
            }
            if (oldIndex == newIndex) return true;

            using (new GraphUndoScope(Tree, "Reorder Decorators"))
            {
                owner.decorators.RemoveAt(oldIndex);
                owner.decorators.Insert(newIndex, decorator);
                EditorUtility.SetDirty(owner);
            }
            return true;
        }

        public bool CanAttachService(Node owner, out string reason)
        {
            reason = null;
            if (owner == null) reason = "owner is null";
            else if (!Tree.nodes.Contains(owner)) reason = $"'{owner.name}' is not in this tree";
            else if (owner is RerouteNode) reason = "a reroute is skipped at runtime; attach services to the node it leads to";
            else if (owner is ServiceNode) reason = "services cannot have services";
            else if (owner is DecoratorNode) reason = "decorators cannot have services";
            return reason == null;
        }

        public ServiceNode AddService(Node owner, Type serviceType)
        {
            if (serviceType == null || serviceType.IsAbstract || !typeof(ServiceNode).IsAssignableFrom(serviceType))
            {
                Debug.LogError($"{LogTag} {nameof(AddService)} rejected: type={serviceType?.FullName ?? "null"} is not a concrete ServiceNode.");
                return null;
            }
            if (!CanAttachService(owner, out string reason))
            {
                Debug.LogWarning($"{LogTag} {nameof(AddService)} rejected: owner={owner?.name}, reason={reason}");
                return null;
            }

            using (new GraphUndoScope(Tree, $"Add {serviceType.Name}"))
            {
                var service = (ServiceNode)ScriptableObject.CreateInstance(serviceType);
                service.name = serviceType.Name;
                RegisterNewObject(service);
                owner.AddService(service);
                EditorUtility.SetDirty(owner);
                return service;
            }
        }

        public bool RemoveService(Node owner, ServiceNode service)
        {
            if (owner == null || service == null || !owner.services.Contains(service))
            {
                Debug.LogWarning($"{LogTag} {nameof(RemoveService)} rejected: owner={owner?.name}, service={service?.name} is not attached.");
                return false;
            }

            using (new GraphUndoScope(Tree, "Remove Service"))
            {
                owner.RemoveService(service);
                DestroySubObject(service);
                EditorUtility.SetDirty(owner);
            }
            return true;
        }

        /// <summary>
        /// Pastes a clipboard snapshot centred on <paramref name="targetCenter"/>. New GUIDs are generated,
        /// internal references are remapped to the pasted copies, and composites are re-sorted.
        /// Returns the pasted graph nodes (not services).
        /// </summary>
        public List<Node> Paste(GraphClipboard clipboard, Vector2 targetCenter)
        {
            var pasted = new List<Node>();
            if (clipboard == null || clipboard.IsEmpty)
            {
                Debug.LogWarning($"{LogTag} {nameof(Paste)} rejected: clipboard is empty or its snapshot was destroyed.");
                return pasted;
            }

            Vector2 offset = targetCenter - clipboard.GetCenter();
            var map = new Dictionary<Object, Object>();

            // Build every copy completely BEFORE registering it with Undo: Undo snapshots created objects at registration.
            foreach (var snapshot in clipboard.AllObjects)
            {
                var copy = Object.Instantiate(snapshot);
                copy.name = snapshot.name;
                copy.hideFlags = HideFlags.None;
                if (copy is Node node) node.SetNewID(Guid.NewGuid().ToString());
                map[snapshot] = copy;
            }

            foreach (var snapshotNode in clipboard.GraphNodes)
            {
                var node = (Node)map[snapshotNode];
                var rect = node.position;
                rect.position += offset;
                node.SetPosition(rect);
                pasted.Add(node);
            }

            foreach (var copy in map.Values)
            {
                ReferenceRemapper.Remap(copy, value => map.TryGetValue(value, out var mapped) ? mapped : value);
                if (copy is Node pastedNode)
                {
                    pastedNode.decorators.RemoveAll(d => d == null);
                    pastedNode.services.RemoveAll(s => s == null);
                }
                if (copy is CompositeNode composite)
                {
                    composite.children.RemoveAll(c => c == null);
                    ExecutionOrder.SortChildren(composite);
                }
            }

            using (new GraphUndoScope(Tree, "Paste Nodes"))
            {
                foreach (var copy in map.Values) RegisterNewObject((ScriptableObject)copy);
                Tree.nodes.AddRange(pasted);
                EditorUtility.SetDirty(Tree);
            }
            return pasted;
        }

        public void Save()
        {
            if (IsPersistent) AssetDatabase.SaveAssetIfDirty(Tree);
        }

        #endregion

        private void RegisterNewObject(ScriptableObject obj)
        {
            if (IsPersistent) AssetDatabase.AddObjectToAsset(obj, Tree);
            EditorUtility.SetDirty(obj);
        }

        /// <summary>Destroys a node/attachment sub-asset. Undo recreates it from the graph history.</summary>
        private static void DestroySubObject(Object obj)
        {
            if (obj != null) Object.DestroyImmediate(obj, true);
        }
    }
}
