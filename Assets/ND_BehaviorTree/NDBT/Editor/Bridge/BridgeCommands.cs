using System;
using System.Collections.Generic;
using System.Linq;
using ND_BehaviorTree.Editor.Graph;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ND_BehaviorTree.Editor.Bridge
{
    /// <summary>
    /// Commands served to the ndbt CLI / MCP bridge. Names use snake_case (the CLI also accepts kebab-case).
    /// Read commands need access ReadOnly or higher; write commands need ReadWrite and are refused in Play mode.
    /// Writes go through the graph service and are ONE graph-history step each (Ctrl+Z in the editor undoes them);
    /// saving follows the Behavior Tree window's Live Save toggle.
    /// </summary>
    public static class BridgeCommands
    {
        /// <summary>Read: ReadOnly+. Write: ReadWrite, edit mode only (asset edits). RuntimeWrite: ReadWrite, acts on Play-mode agents.</summary>
        private enum Access { Read, Write, RuntimeWrite }

        private sealed class Command
        {
            public Access Access;
            public string Summary;
            public Func<Dictionary<string, object>, object> Run;
        }

        private static readonly Dictionary<string, Command> s_Commands = new Dictionary<string, Command>
        {
            ["status"] = new Command { Access = Access.Read, Summary = "Bridge, access level, Unity and play-mode state.", Run = Status },
            ["help"] = new Command { Access = Access.Read, Summary = "List of commands.", Run = Help },
            ["node_list"] = new Command { Access = Access.Read, Summary = "Node types. Args: category? (composite|action|decorator|service|event|subtree|init), search?", Run = NodeList },
            ["node_info"] = new Command { Access = Access.Read, Summary = "Fields, defaults, key slots and ports of a node type. Args: type", Run = NodeInfo },
            ["tree_list"] = new Command { Access = Access.Read, Summary = "BehaviorTree assets. Args: folder?", Run = TreeList },
            ["tree_get"] = new Command { Access = Access.Read, Summary = "A tree as json or outline. Args: tree, format? (json|outline)", Run = TreeGet },
            ["tree_validate"] = new Command { Access = Access.Read, Summary = "Validation issues of a tree. Args: tree", Run = TreeValidate },
            ["validate_all"] = new Command { Access = Access.Read, Summary = "Validation issues of every tree.", Run = ValidateAll },
            ["blackboard_get"] = new Command { Access = Access.Read, Summary = "Blackboard keys of a tree. Args: tree", Run = BlackboardGet },
            ["find_usages"] = new Command { Access = Access.Read, Summary = "Where a node type or key is used. Args: type? | key?, tree?", Run = FindUsages },
            ["agent_list"] = new Command { Access = Access.Read, Summary = "Running agents (Play mode).", Run = AgentList },
            ["agent_state"] = new Command { Access = Access.Read, Summary = "Live state of an agent: running path and recent results. Args: agent", Run = AgentState },

            ["tree_create"] = new Command { Access = Access.Write, Summary = "Create a tree. Args: path, blackboard? (asset path | \"new\"), ops?[] (applied after creation)", Run = TreeCreate },
            ["tree_duplicate"] = new Command { Access = Access.Write, Summary = "Copy a tree asset. Args: tree, path", Run = TreeDuplicate },
            ["tree_apply"] = new Command { Access = Access.Write, Summary = "Apply a batch of operations (all or nothing, one undo step). Args: tree, ops[], dryRun?, label?", Run = TreeApply },
            ["subtree_extract"] = new Command { Access = Access.Write, Summary = "Move a branch into a new subtree asset. Args: tree, nodes[], path", Run = SubtreeExtract },
            ["blackboard_key"] = new Command { Access = Access.Write, Summary = "Manage keys. Args: tree, action (add|rename|remove|set), name, type? (add), newName? (rename), value? (set)", Run = BlackboardKey },
            ["history"] = new Command { Access = Access.Write, Summary = "Undo/redo graph edits. Args: tree, action (undo|redo)", Run = History },
            ["tree_save"] = new Command { Access = Access.Write, Summary = "Write a tree to disk (when Live Save is off). Args: tree", Run = TreeSave },
            ["agent_send_event"] = new Command { Access = Access.RuntimeWrite, Summary = "SendEvent on an agent (Play mode). Args: agent, event", Run = AgentSendEvent },
            ["menu_run"] = new Command { Access = Access.Write, Summary = "Run an editor menu item under ND_BehaviorTree/ (e.g. demo scene builders). Args: path", Run = MenuRun },
        };

        private const string MenuPrefix = "ND_BehaviorTree/";

        /// <summary>Runs one of this package's own menu items. Restricted to ND_BehaviorTree/ so AI access cannot reach arbitrary editor menus.</summary>
        private static object MenuRun(Dictionary<string, object> args)
        {
            string path = BridgeUtil.Str(args, "path").Trim();
            if (!path.StartsWith(MenuPrefix, StringComparison.Ordinal))
                throw new BridgeException($"Only menu items under '{MenuPrefix}' can be run (got '{path}').");
            if (!EditorApplication.ExecuteMenuItem(path))
                throw new BridgeException($"Menu item '{path}' does not exist or is disabled.");
            return new Dictionary<string, object> { ["ran"] = path };
        }

        /// <summary>Entry point from the server (main thread). Never throws.</summary>
        public static string Execute(string body)
        {
            try
            {
                var request = MiniJson.Parse(body) as Dictionary<string, object> ?? throw new BridgeException("Body must be a JSON object {\"command\", \"args\"}.");
                string name = BridgeUtil.Str(request, "command").Trim().ToLowerInvariant().Replace('-', '_');
                var args = request.TryGetValue("args", out var a) ? a as Dictionary<string, object> ?? new Dictionary<string, object>() : new Dictionary<string, object>();

                if (!s_Commands.TryGetValue(name, out var command)) throw new BridgeException($"Unknown command '{name}'. Use 'help'.");
                CheckAccess(command.Access, name);

                var result = command.Run(args);
                return MiniJson.Serialize(new Dictionary<string, object> { ["ok"] = true, ["result"] = result });
            }
            catch (BridgeException e)
            {
                return BridgeServer.Error(e.Message);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return BridgeServer.Error($"Internal error: {e.Message}");
            }
        }

        private static void CheckAccess(Access access, string name)
        {
            if (!AiAccess.IsActive) throw new BridgeException("AI access is locked. Ask the project owner to unlock it in Project Settings → ND Behavior Tree → AI Access.");
            if (access != Access.Read && !AiAccess.CanWrite) throw new BridgeException($"'{name}' needs Read + Write access; the project allows {AiAccess.Level}.");
            if (access == Access.Write && EditorApplication.isPlayingOrWillChangePlaymode) throw new BridgeException("Graph edits are refused in Play mode. Stop Play mode and retry.");
        }

        #region Read

        private static object Status(Dictionary<string, object> args) => new Dictionary<string, object>
        {
            ["project"] = System.IO.Path.GetDirectoryName(Application.dataPath),
            ["unityVersion"] = Application.unityVersion,
            ["access"] = AiAccess.Level.ToString(),
            ["playMode"] = EditorApplication.isPlaying,
            ["liveSave"] = ND_BehaviorTreeEditorWindow.IsLiveSaveEnabled,
            ["historyCapacity"] = ND_BehaviorTreeSetting.Instance.historyCapacity
        };

        private static object Help(Dictionary<string, object> args) =>
            s_Commands.Select(kvp => new Dictionary<string, object> { ["command"] = kvp.Key, ["access"] = kvp.Value.Access.ToString(), ["summary"] = kvp.Value.Summary }).ToList();

        private static object NodeList(Dictionary<string, object> args)
        {
            string category = BridgeUtil.Str(args, "category", required: false)?.ToLowerInvariant();
            string search = BridgeUtil.Str(args, "search", required: false);
            return BridgeUtil.NodeTypes()
                .Where(t => !typeof(RootNode).IsAssignableFrom(t))
                .Select(t => new { Type = t, Info = BridgeUtil.Info(t), Kind = BridgeUtil.KindOf(t) })
                .Where(x => category == null || x.Kind == category)
                .Where(x => search == null || x.Type.FullName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                                           || (x.Info?.title ?? "").IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                                           || (x.Info?.menuItem ?? "").IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(x => x.Info?.menuItem ?? x.Type.Name)
                .Select(x => (object)new Dictionary<string, object>
                {
                    ["type"] = x.Type.FullName,
                    ["title"] = BridgeUtil.Title(x.Type),
                    ["menu"] = x.Info?.menuItem,
                    ["kind"] = x.Kind,
                    ["children"] = typeof(CompositeNode).IsAssignableFrom(x.Type) ? "many" : (x.Info?.hasFlowOutput ?? false) ? "one" : "none",
                    ["hasInput"] = x.Info?.hasFlowInput ?? false
                }).ToList();
        }

        private static object NodeInfo(Dictionary<string, object> args)
        {
            var type = BridgeUtil.ResolveNodeType(BridgeUtil.Str(args, "type"), BridgeUtil.TypeKind.Any);
            var info = BridgeUtil.Info(type);
            var instance = (Node)ScriptableObject.CreateInstance(type);
            try
            {
                var slots = KeySlotScanner.Scan(instance);
                var fieldDocs = new Dictionary<string, object>();
                foreach (var kvp in BridgeUtil.FieldsOf(instance, slots.Select(s => s.PropertyPath).ToList()))
                {
                    var field = type.GetField(kvp.Key, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    var tooltip = field?.GetCustomAttributes(typeof(TooltipAttribute), true).OfType<TooltipAttribute>().FirstOrDefault()?.tooltip;
                    fieldDocs[kvp.Key] = new Dictionary<string, object>
                    {
                        ["type"] = field != null ? KeySlotScanner.FriendlyTypeName(field.FieldType) : null,
                        ["default"] = kvp.Value,
                        ["description"] = tooltip
                    };
                }
                return new Dictionary<string, object>
                {
                    ["type"] = type.FullName,
                    ["title"] = BridgeUtil.Title(type),
                    ["menu"] = info?.menuItem,
                    ["kind"] = BridgeUtil.KindOf(type),
                    ["hasInput"] = info?.hasFlowInput ?? false,
                    ["children"] = typeof(CompositeNode).IsAssignableFrom(type) ? "many" : (info?.hasFlowOutput ?? false) ? "one" : "none",
                    ["fields"] = fieldDocs,
                    ["keySlots"] = slots.Select(s => (object)new Dictionary<string, object> { ["path"] = s.PropertyPath, ["label"] = s.Label, ["requires"] = KeySlotScanner.FriendlyTypeName(s.RequiredValueType) }).ToList(),
                    ["attach"] = BridgeUtil.KindOf(type) == "decorator" ? "add-decorator" : BridgeUtil.KindOf(type) == "service" ? "add-service" : "create"
                };
            }
            finally { Object.DestroyImmediate(instance); }
        }

        private static object TreeList(Dictionary<string, object> args)
        {
            string folder = BridgeUtil.Str(args, "folder", required: false);
            var guids = folder == null ? AssetDatabase.FindAssets("t:BehaviorTree") : AssetDatabase.FindAssets("t:BehaviorTree", new[] { folder });
            return guids.Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<BehaviorTree>)
                .Where(t => t != null)
                .Select(t => (object)TreeExporter.Summary(t)).ToList();
        }

        private static object TreeGet(Dictionary<string, object> args)
        {
            var tree = BridgeUtil.ResolveTree(BridgeUtil.Str(args, "tree"));
            string format = (BridgeUtil.Str(args, "format", required: false) ?? "json").ToLowerInvariant();
            return format == "outline" ? (object)TreeExporter.ToOutline(tree) : TreeExporter.ToJson(tree);
        }

        private static object TreeValidate(Dictionary<string, object> args)
        {
            var tree = BridgeUtil.ResolveTree(BridgeUtil.Str(args, "tree"));
            return IssuesOf(tree);
        }

        private static object ValidateAll(Dictionary<string, object> args) =>
            AssetDatabase.FindAssets("t:BehaviorTree").Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<BehaviorTree>).Where(t => t != null)
                .Select(t => (object)new Dictionary<string, object> { ["tree"] = BridgeUtil.PathOf(t), ["result"] = IssuesOf(t) }).ToList();

        private static Dictionary<string, object> IssuesOf(BehaviorTree tree)
        {
            var issues = GraphValidator.Validate(tree);
            return new Dictionary<string, object>
            {
                ["errors"] = issues.Count(i => i.Severity == GraphIssueSeverity.Error),
                ["warnings"] = issues.Count(i => i.Severity == GraphIssueSeverity.Warning),
                ["issues"] = issues.Select(i => (object)new Dictionary<string, object>
                {
                    ["severity"] = i.Severity.ToString(), ["code"] = i.Code, ["message"] = i.Message,
                    ["node"] = i.Node != null ? i.Node.id : null
                }).ToList()
            };
        }

        private static object BlackboardGet(Dictionary<string, object> args)
        {
            var tree = BridgeUtil.ResolveTree(BridgeUtil.Str(args, "tree"));
            return new Dictionary<string, object>
            {
                ["blackboard"] = tree.blackboard != null ? BridgeUtil.PathOf(tree.blackboard) : null,
                ["keys"] = TreeExporter.BlackboardJson(tree.blackboard),
                ["keyTypes"] = TypeCache.GetTypesDerivedFrom<Key>().Where(t => !t.IsAbstract)
                    .Select(t => KeySlotScanner.FriendlyTypeName(((Key)ScriptableObjectCache(t)).GetValueType())).Distinct().OrderBy(n => n).ToList()
            };
        }

        private static readonly Dictionary<Type, ScriptableObject> s_KeyPrototypes = new Dictionary<Type, ScriptableObject>();
        private static ScriptableObject ScriptableObjectCache(Type type)
        {
            if (!s_KeyPrototypes.TryGetValue(type, out var so) || so == null)
            {
                so = ScriptableObject.CreateInstance(type);
                so.hideFlags = HideFlags.HideAndDontSave;
                s_KeyPrototypes[type] = so;
            }
            return so;
        }

        private static object FindUsages(Dictionary<string, object> args)
        {
            string typeArg = BridgeUtil.Str(args, "type", required: false);
            string keyArg = BridgeUtil.Str(args, "key", required: false);
            if (typeArg == null && keyArg == null) throw new BridgeException("Give 'type' or 'key'.");
            Type type = typeArg != null ? BridgeUtil.ResolveNodeType(typeArg, BridgeUtil.TypeKind.Any) : null;

            var trees = BridgeUtil.Str(args, "tree", required: false) is string t
                ? new List<BehaviorTree> { BridgeUtil.ResolveTree(t) }
                : AssetDatabase.FindAssets("t:BehaviorTree").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<BehaviorTree>).Where(x => x != null).ToList();

            var hits = new List<object>();
            foreach (var tree in trees)
            {
                foreach (var node in BridgeUtil.AllNodes(tree))
                {
                    if (type != null && type.IsInstanceOfType(node))
                        hits.Add(new Dictionary<string, object> { ["tree"] = BridgeUtil.PathOf(tree), ["node"] = node.id, ["title"] = BridgeUtil.Title(node.GetType()) });
                    if (keyArg != null)
                    {
                        foreach (var slot in KeySlotScanner.Scan(node).Where(s => s.Current != null && s.Current.keyName == keyArg))
                            hits.Add(new Dictionary<string, object> { ["tree"] = BridgeUtil.PathOf(tree), ["node"] = node.id, ["title"] = BridgeUtil.Title(node.GetType()), ["field"] = slot.PropertyPath });
                    }
                }
            }
            return hits;
        }

        #endregion

        #region Write

        private static object TreeCreate(Dictionary<string, object> args)
        {
            string path = NormalizeNewPath(BridgeUtil.Str(args, "path"));
            var tree = ScriptableObject.CreateInstance<BehaviorTree>();
            tree.SchemaVersion = BehaviorTree.CurrentSchemaVersion;
            AssetDatabase.CreateAsset(tree, path);
            tree.EditorInit();

            string bb = BridgeUtil.Str(args, "blackboard", required: false);
            if (string.Equals(bb, "new", StringComparison.OrdinalIgnoreCase))
            {
                var blackboard = ScriptableObject.CreateInstance<Blackboard>();
                AssetDatabase.CreateAsset(blackboard, AssetDatabase.GenerateUniqueAssetPath(path.Replace(".asset", "_Blackboard.asset")));
                tree.blackboard = blackboard;
            }
            else if (!string.IsNullOrEmpty(bb))
            {
                tree.blackboard = AssetDatabase.LoadAssetAtPath<Blackboard>(bb) ?? throw new BridgeException($"No Blackboard asset at '{bb}'.");
            }
            EditorUtility.SetDirty(tree);
            AssetDatabase.SaveAssetIfDirty(tree);
            GraphHistory.For(tree).Clear();

            var result = new Dictionary<string, object> { ["tree"] = TreeExporter.Summary(tree) };
            var ops = BridgeUtil.List(args, "ops", required: false);
            if (ops != null && ops.Count > 0)
            {
                var applied = ApplyEngine.Execute(tree, ops, dryRun: false, label: "initial content");
                result["apply"] = applied;
                result["saved"] = ND_BehaviorTreeEditorWindow.OnTreeChangedExternally(tree, save: !applied.ContainsKey("error"));
            }
            return result;
        }

        private static object TreeDuplicate(Dictionary<string, object> args)
        {
            var tree = BridgeUtil.ResolveTree(BridgeUtil.Str(args, "tree"));
            string path = NormalizeNewPath(BridgeUtil.Str(args, "path"));
            if (!AssetDatabase.CopyAsset(BridgeUtil.PathOf(tree), path)) throw new BridgeException($"Could not copy to '{path}'.");
            return TreeExporter.Summary(AssetDatabase.LoadAssetAtPath<BehaviorTree>(path));
        }

        private static object TreeApply(Dictionary<string, object> args)
        {
            var tree = BridgeUtil.ResolveTree(BridgeUtil.Str(args, "tree"));
            bool dryRun = BridgeUtil.Bool(args, "dryRun");
            var result = ApplyEngine.Execute(tree, BridgeUtil.List(args, "ops"), dryRun, BridgeUtil.Str(args, "label", required: false));
            bool changed = !dryRun && !result.ContainsKey("error");
            result["saved"] = ND_BehaviorTreeEditorWindow.OnTreeChangedExternally(tree, save: changed);
            if (changed && !(bool)result["saved"]) result["note"] = "Live Save is off: changes are in memory. Call tree_save to write them.";
            return result;
        }

        private static object SubtreeExtract(Dictionary<string, object> args)
        {
            var tree = BridgeUtil.ResolveTree(BridgeUtil.Str(args, "tree"));
            var nodes = BridgeUtil.List(args, "nodes").Select(n => BridgeUtil.ResolveNode(tree, Convert.ToString(n))).ToList();
            string path = NormalizeNewPath(BridgeUtil.Str(args, "path"));
            var subtreeNode = new BehaviorTreeGraphService(tree).ConvertToSubtree(nodes, path, out string error);
            if (subtreeNode == null) throw new BridgeException(error);
            bool saved = ND_BehaviorTreeEditorWindow.OnTreeChangedExternally(tree, save: true);
            return new Dictionary<string, object> { ["subtreeNode"] = subtreeNode.id, ["subtree"] = path, ["saved"] = saved };
        }

        private static object BlackboardKey(Dictionary<string, object> args)
        {
            var tree = BridgeUtil.ResolveTree(BridgeUtil.Str(args, "tree"));
            var blackboard = tree.blackboard ?? throw new BridgeException($"Tree '{tree.name}' has no blackboard. Create one with tree_create blackboard=\"new\" or in the editor.");
            string action = BridgeUtil.Str(args, "action").ToLowerInvariant();
            string name = BridgeUtil.Str(args, "name");
            Key Find() => blackboard.keys.FirstOrDefault(k => k != null && k.keyName == name) ?? throw new BridgeException($"No key '{name}'.");

            // One Unity undo step per command: without a new group, consecutive AI key edits merge into one step and a
            // single Ctrl+Z in the editor reverts all of them (leaving the created key sub-assets orphaned).
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName($"AI: {action} blackboard key '{name}'");

            switch (action)
            {
                case "add":
                {
                    if (blackboard.keys.Any(k => k != null && k.keyName == name)) throw new BridgeException($"Key '{name}' already exists.");
                    string typeArg = BridgeUtil.Str(args, "type");
                    var keyType = TypeCache.GetTypesDerivedFrom<Key>().Where(t => !t.IsAbstract).FirstOrDefault(t =>
                                      string.Equals(t.Name, typeArg, StringComparison.OrdinalIgnoreCase)
                                      || string.Equals(t.Name, typeArg + "Key", StringComparison.OrdinalIgnoreCase)
                                      || string.Equals(KeySlotScanner.FriendlyTypeName(((Key)ScriptableObjectCache(t)).GetValueType()), typeArg, StringComparison.OrdinalIgnoreCase))
                                  ?? throw new BridgeException($"Unknown key type '{typeArg}'. See blackboard_get → keyTypes.");
                    Undo.RecordObject(blackboard, "Add Key");
                    var key = (Key)ScriptableObject.CreateInstance(keyType);
                    key.name = keyType.Name;
                    key.keyName = name;
                    AssetDatabase.AddObjectToAsset(key, blackboard);
                    Undo.RegisterCreatedObjectUndo(key, "Add Key"); // undoing the add also destroys the sub-asset
                    blackboard.keys.Add(key);
                    if (args.TryGetValue("value", out var v)) SetKeyValue(key, v);
                    break;
                }
                case "rename":
                {
                    string newName = BridgeUtil.Str(args, "newName");
                    if (blackboard.keys.Any(k => k != null && k.keyName == newName)) throw new BridgeException($"Key '{newName}' already exists.");
                    var key = Find();
                    Undo.RecordObject(key, "Rename Key");
                    key.keyName = newName;
                    EditorUtility.SetDirty(key);
                    break;
                }
                case "set":
                    SetKeyValue(Find(), args.TryGetValue("value", out var value) ? value : null);
                    break;
                case "remove":
                {
                    var key = Find();
                    Undo.RecordObject(blackboard, "Remove Key");
                    blackboard.keys.Remove(key);
                    Undo.DestroyObjectImmediate(key);
                    break;
                }
                default:
                    throw new BridgeException("action must be add, rename, set or remove.");
            }
            EditorUtility.SetDirty(blackboard);
            AssetDatabase.SaveAssetIfDirty(blackboard);
            ND_BehaviorTreeEditorWindow.OnTreeChangedExternally(tree, save: false);
            return TreeExporter.BlackboardJson(blackboard);
        }

        private static void SetKeyValue(Key key, object value)
        {
            var so = new SerializedObject(key);
            var prop = so.FindProperty("value") ?? throw new BridgeException($"Key '{key.keyName}' has no editable value (set at runtime).");
            BridgeUtil.SetProperty(prop, value);
            so.ApplyModifiedProperties();
        }

        private static object History(Dictionary<string, object> args)
        {
            var tree = BridgeUtil.ResolveTree(BridgeUtil.Str(args, "tree"));
            var history = GraphHistory.For(tree);
            string action = BridgeUtil.Str(args, "action").ToLowerInvariant();
            string name = action == "undo" ? history.NextUndoName : history.NextRedoName;
            bool ok = action == "undo" ? history.Undo() : action == "redo" ? history.Redo() : throw new BridgeException("action must be undo or redo.");
            if (!ok) throw new BridgeException($"Nothing to {action}.");
            bool saved = ND_BehaviorTreeEditorWindow.OnTreeChangedExternally(tree, save: true);
            return new Dictionary<string, object> { ["action"] = action, ["step"] = name, ["undoLeft"] = history.UndoCount, ["redoLeft"] = history.RedoCount, ["saved"] = saved };
        }

        private static object TreeSave(Dictionary<string, object> args)
        {
            var tree = BridgeUtil.ResolveTree(BridgeUtil.Str(args, "tree"));
            EditorUtility.SetDirty(tree);
            new BehaviorTreeGraphService(tree).Save();
            foreach (var window in Resources.FindObjectsOfTypeAll<ND_BehaviorTreeEditorWindow>().Where(w => w.currentGraph == tree)) window.SetUnsavedChanges(false);
            return new Dictionary<string, object> { ["saved"] = BridgeUtil.PathOf(tree) };
        }

        private static string NormalizeNewPath(string path)
        {
            path = path.Trim().Replace('\\', '/');
            if (!path.StartsWith("Assets/")) throw new BridgeException("Paths must start with 'Assets/'.");
            if (!path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)) path += ".asset";
            string folder = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(folder)) throw new BridgeException($"Folder '{folder}' does not exist.");
            if (AssetDatabase.LoadMainAssetAtPath(path) != null) throw new BridgeException($"An asset already exists at '{path}'.");
            return path;
        }

        #endregion

        #region Runtime (Play mode)

        private static BehaviorTreeRunner ResolveAgent(string arg)
        {
            if (!EditorApplication.isPlaying) throw new BridgeException("Agents exist only in Play mode.");
            var runners = Object.FindObjectsByType<BehaviorTreeRunner>(FindObjectsSortMode.None);
            var byId = int.TryParse(arg, out int id) ? runners.FirstOrDefault(r => r.gameObject.GetInstanceID() == id) : null;
            if (byId != null) return byId;
            var byName = runners.Where(r => r.gameObject.name == arg).ToList();
            if (byName.Count == 1) return byName[0];
            if (byName.Count > 1) throw new BridgeException($"{byName.Count} agents are named '{arg}'; use the id from agent_list.");
            throw new BridgeException($"No agent '{arg}'. Use agent_list.");
        }

        private static object AgentList(Dictionary<string, object> args)
        {
            if (!EditorApplication.isPlaying) return new List<object>();
            return Object.FindObjectsByType<BehaviorTreeRunner>(FindObjectsSortMode.None).Select(r => (object)new Dictionary<string, object>
            {
                ["id"] = r.gameObject.GetInstanceID(),
                ["name"] = r.gameObject.name,
                ["tree"] = r.treeAsset != null ? BridgeUtil.PathOf(r.treeAsset) : null,
                ["status"] = r.RuntimeTree != null ? r.RuntimeTree.LastStatus.ToString() : "not started",
                ["running"] = r.RuntimeTree != null ? LiveDebugUtility.DescribeActivePath(r.RuntimeTree, Time.frameCount) : null
            }).ToList();
        }

        private static object AgentState(Dictionary<string, object> args)
        {
            var runner = ResolveAgent(BridgeUtil.Str(args, "agent"));
            var runtime = runner.RuntimeTree ?? throw new BridgeException("This agent's tree has not started yet.");
            return new Dictionary<string, object>
            {
                ["agent"] = runner.gameObject.name,
                ["tree"] = runner.treeAsset != null ? BridgeUtil.PathOf(runner.treeAsset) : null,
                ["status"] = runtime.LastStatus.ToString(),
                ["initialized"] = runtime.IsInitialized,
                ["running"] = LiveDebugUtility.DescribeActivePath(runtime, Time.frameCount),
                ["nodes"] = LiveNodes(runtime, Time.frameCount, 0)
            };
        }

        private static List<object> LiveNodes(BehaviorTree runtime, int frame, int depth)
        {
            var list = new List<object>();
            foreach (var node in runtime.nodes)
            {
                if (node == null) continue;
                var state = LiveDebugUtility.GetState(node, frame);
                if (state == LiveState.Inactive) continue;
                var entry = new Dictionary<string, object> { ["id"] = BridgeUtil.ShortId(node), ["title"] = BridgeUtil.Title(node.GetType()), ["state"] = state.ToString() };
                if (node is SubtreeNode sub && sub.RuntimeSubtree != null && depth < 4) entry["subtree"] = LiveNodes(sub.RuntimeSubtree, frame, depth + 1);
                list.Add(entry);
            }
            return list;
        }

        private static object AgentSendEvent(Dictionary<string, object> args)
        {
            var runner = ResolveAgent(BridgeUtil.Str(args, "agent"));
            string eventName = BridgeUtil.Str(args, "event");
            return new Dictionary<string, object> { ["sent"] = runner.SendEvent(eventName), ["event"] = eventName };
        }

        #endregion
    }
}
