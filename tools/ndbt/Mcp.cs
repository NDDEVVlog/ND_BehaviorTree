using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Ndbt
{
    /// <summary>
    /// Minimal MCP server over stdio (newline-delimited JSON-RPC 2.0): initialize, tools/list, tools/call, ping.
    /// Each tool forwards to the Unity command of the same name. Logs go to stderr only (stdout is the protocol).
    /// </summary>
    public static class Mcp
    {
        private const string ServerVersion = "1.0.0";

        private const string Instructions =
            "Tools for reading and editing ND Behavior Tree assets in the open Unity project.\n" +
            "Workflow: node_list / node_info to learn node types → tree_get (format \"outline\" is compact) to read a tree → " +
            "tree_apply with dryRun=true to preview → tree_apply to commit (one undo step; the user can Ctrl+Z it).\n" +
            "Node ids: \"root\", a full id, or the 8-char short id shown in outlines. Inside one tree_apply batch, " +
            "give new nodes a \"ref\" and use it in later operations. Blackboard keys are linked by key name with link-key.";

        private static readonly JsonSerializerOptions s_Json = new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

        public static async Task<int> Run(string project)
        {
            Console.Error.WriteLine($"[ndbt] MCP server for {project}");
            var stdin = new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(false));
            var stdout = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = true };

            string line;
            while ((line = await stdin.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                JsonObject message;
                try { message = JsonNode.Parse(line) as JsonObject; }
                catch (JsonException e) { await Write(stdout, Error(null, -32700, $"Parse error: {e.Message}")); continue; }
                if (message == null) continue;

                var id = message["id"]?.DeepClone();
                string method = message["method"]?.GetValue<string>();
                if (id == null) continue; // notification (e.g. notifications/initialized): no response

                JsonObject response;
                try
                {
                    response = method switch
                    {
                        "initialize" => Result(id, Initialize(message["params"] as JsonObject)),
                        "ping" => Result(id, new JsonObject()),
                        "tools/list" => Result(id, new JsonObject { ["tools"] = Tools() }),
                        "tools/call" => Result(id, await CallTool(project, message["params"] as JsonObject)),
                        _ => Error(id, -32601, $"Method not found: {method}")
                    };
                }
                catch (Exception e)
                {
                    response = Error(id, -32603, e.Message);
                }
                await Write(stdout, response);
            }
            return 0;
        }

        private static JsonObject Initialize(JsonObject parameters) => new JsonObject
        {
            ["protocolVersion"] = parameters?["protocolVersion"]?.GetValue<string>() ?? "2025-06-18",
            ["capabilities"] = new JsonObject { ["tools"] = new JsonObject { ["listChanged"] = false } },
            ["serverInfo"] = new JsonObject { ["name"] = "ndbt", ["version"] = ServerVersion },
            ["instructions"] = Instructions
        };

        private static async Task<JsonObject> CallTool(string project, JsonObject parameters)
        {
            string name = parameters?["name"]?.GetValue<string>() ?? throw new ArgumentException("Missing tool name.");
            var arguments = parameters["arguments"] as JsonObject ?? new JsonObject();
            var args = arguments.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value?.DeepClone());

            string text;
            bool isError;
            try
            {
                var response = await Program.CallUnity(project, name, args);
                isError = !(response["ok"]?.GetValue<bool>() ?? false);
                var payload = isError ? response["error"] : response["result"];
                text = payload is JsonValue v && v.TryGetValue(out string s) ? s : payload?.ToJsonString(s_Json) ?? "null";
            }
            catch (BridgeUnavailableException e)
            {
                isError = true;
                text = e.Message;
            }

            return new JsonObject
            {
                ["content"] = new JsonArray { new JsonObject { ["type"] = "text", ["text"] = text } },
                ["isError"] = isError
            };
        }

        private static async Task Write(StreamWriter stdout, JsonObject message) => await stdout.WriteLineAsync(message.ToJsonString(s_Json));

        private static JsonObject Result(JsonNode id, JsonNode result) => new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id, ["result"] = result };

        private static JsonObject Error(JsonNode id, int code, string message) =>
            new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id, ["error"] = new JsonObject { ["code"] = code, ["message"] = message } };

        #region Tool definitions

        private static JsonArray Tools() => new JsonArray
        {
            Tool("status", "Bridge status: access level, Unity version, Play mode, Live Save."),
            Tool("help", "List the commands Unity exposes."),
            Tool("node_list", "List node types you can use (title, type, kind, menu path, children: none/one/many).",
                Prop("category", "string", "Filter: composite, action, decorator, service, event, init, subtree."),
                Prop("search", "string", "Filter by text in type, title or menu path.")),
            Tool("node_info", "Details of one node type: fields with defaults and descriptions, blackboard key slots with required value types, ports, and how to add it (create / add-decorator / add-service).",
                Req(Prop("type", "string", "Type: full name, class name, title or menu path (e.g. \"Wait\", \"Composite/Selector\")."))),
            Tool("tree_list", "List BehaviorTree assets (path, guid, node count, blackboard).",
                Prop("folder", "string", "Only trees under this folder, e.g. Assets/AI.")),
            Tool("tree_get", "Read a tree. format \"outline\" is a compact indented view with 8-char node ids; \"json\" has every node, field, key link, decorator, service, group and note.",
                Req(Prop("tree", "string", "Asset path, GUID or unique name.")),
                Prop("format", "string", "outline (default for reading) or json.", new[] { "outline", "json" })),
            Tool("tree_validate", "Validation issues of a tree (errors and warnings with node ids).",
                Req(Prop("tree", "string", "Asset path, GUID or unique name."))),
            Tool("validate_all", "Validation issues of every tree in the project."),
            Tool("blackboard_get", "Blackboard keys of a tree (name, type, value) and the key types available.",
                Req(Prop("tree", "string", "Asset path, GUID or unique name."))),
            Tool("find_usages", "Where a node type or a blackboard key is used.",
                Prop("type", "string", "Node type to search for."),
                Prop("key", "string", "Blackboard key name to search for."),
                Prop("tree", "string", "Limit to one tree.")),
            Tool("agent_list", "Running agents in Play mode (id, name, tree, status, running path)."),
            Tool("agent_state", "Live state of one agent: running path (into subtrees) and recently finished nodes.",
                Req(Prop("agent", "string", "Agent name or id from agent_list."))),

            Tool("tree_create", "Create a new BehaviorTree asset (with a Root). Optionally build its content with the same operations as tree_apply.",
                Req(Prop("path", "string", "New asset path under Assets/, e.g. Assets/AI/Guard.asset.")),
                Prop("blackboard", "string", "Existing Blackboard asset path, or \"new\" to create one next to the tree."),
                OpsProp("Operations to apply after creation (see tree_apply).")),
            Tool("tree_duplicate", "Copy a tree asset to a new path.",
                Req(Prop("tree", "string", "Source tree.")), Req(Prop("path", "string", "New asset path under Assets/."))),
            Tool("tree_apply",
                "Apply a batch of graph operations as ONE undo step; all succeed or everything is rolled back. Use dryRun first. " +
                "Operations (field \"op\"): create{type, ref?, parent?, position?[x,y], fields?, label?} · delete{nodes[]} · connect{parent, child} · " +
                "disconnect{parent, child} · move{node, position} · set-field{node, path, value | fields{}} · link-key{node, path, key|null} · " +
                "add-decorator{node, type, ref?, fields?} · add-service{node, type, ref?, fields?} · remove-attachment{node, attachment} · " +
                "move-decorator{node, attachment, index} · sort-children{node} · group{title, nodes[]} · note{title, content?, position?} · set-subtree{node, subtree}. " +
                "Node references: \"root\", node id / 8-char short id, or a ref declared earlier in the batch. Children run left-to-right by x position.",
                Req(Prop("tree", "string", "Asset path, GUID or unique name.")),
                Req(OpsProp("The operations, in order.")),
                Prop("dryRun", "boolean", "Preview only: returns the result, validation and an outline, then rolls back."),
                Prop("label", "string", "Name of the undo step shown in the editor.")),
            Tool("subtree_extract", "Move a closed branch (one top node + all its descendants) into a new subtree asset and replace it with a Run Subtree node.",
                Req(Prop("tree", "string", "Tree.")), Req(Prop("nodes", "array", "Node ids of the branch.")), Req(Prop("path", "string", "New subtree asset path."))),
            Tool("blackboard_key", "Add, rename, set or remove a blackboard key of a tree.",
                Req(Prop("tree", "string", "Tree.")),
                Req(Prop("action", "string", "add | rename | set | remove", new[] { "add", "rename", "set", "remove" })),
                Req(Prop("name", "string", "Key name.")),
                Prop("type", "string", "For add: value type (e.g. Float, Bool, Transform, NavMeshAgent) — see blackboard_get keyTypes."),
                Prop("newName", "string", "For rename."),
                Prop("value", "string", "For add/set: initial value (JSON for numbers/bools/vectors).")),
            Tool("history", "Undo or redo the last graph edit of a tree (same history as Ctrl+Z in the editor).",
                Req(Prop("tree", "string", "Tree.")), Req(Prop("action", "string", "undo | redo", new[] { "undo", "redo" }))),
            Tool("tree_save", "Write a tree to disk (needed when the editor's Live Save is off).",
                Req(Prop("tree", "string", "Tree."))),
            Tool("agent_send_event", "Fire an event on a running agent (Play mode).",
                Req(Prop("agent", "string", "Agent name or id.")), Req(Prop("event", "string", "Event name."))),
            Tool("menu_run", "Run an editor menu item of this package (path must start with ND_BehaviorTree/), e.g. a demo scene builder.",
                Req(Prop("path", "string", "Menu path, e.g. ND_BehaviorTree/Demo/Build Xenomorph Test Scene."))),
        };

        private sealed class PropDef
        {
            public string Name;
            public JsonObject Schema;
            public bool Required;
        }

        private static PropDef Prop(string name, string type, string description, string[] values = null)
        {
            var schema = new JsonObject { ["type"] = type, ["description"] = description };
            if (type == "array") schema["items"] = new JsonObject();
            if (values != null) schema["enum"] = new JsonArray(values.Select(v => (JsonNode)v).ToArray());
            return new PropDef { Name = name, Schema = schema };
        }

        private static PropDef OpsProp(string description) => new PropDef
        {
            Name = "ops",
            Schema = new JsonObject { ["type"] = "array", ["description"] = description, ["items"] = new JsonObject { ["type"] = "object" } }
        };

        private static PropDef Req(PropDef p)
        {
            p.Required = true;
            return p;
        }

        private static JsonObject Tool(string name, string description, params PropDef[] props)
        {
            var properties = new JsonObject();
            foreach (var p in props) properties[p.Name] = p.Schema;
            var schema = new JsonObject { ["type"] = "object", ["properties"] = properties };
            var required = props.Where(p => p.Required).Select(p => (JsonNode)p.Name).ToArray();
            if (required.Length > 0) schema["required"] = new JsonArray(required);
            return new JsonObject { ["name"] = name, ["description"] = description, ["inputSchema"] = schema };
        }

        #endregion
    }
}
