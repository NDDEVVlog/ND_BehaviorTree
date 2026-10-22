using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Ndbt
{
    /// <summary>
    /// ndbt — bridge between AI tools and the ND Behavior Tree editor running in Unity.
    ///
    ///   ndbt <command> [--arg value ...] [--json '{...}'] [--project path]   CLI (one command, prints JSON / text)
    ///   ndbt mcp [--project path]                                            MCP server over stdio (Claude Code etc.)
    ///
    /// Commands are executed inside the Unity Editor (it must be open, with AI access unlocked in
    /// Project Settings → ND Behavior Tree → AI Access). Connection info (port + session token) is read from
    /// <project>/Library/NDBT/bridge.json, which Unity writes while the bridge is running.
    /// </summary>
    public static class Program
    {
        private static readonly HttpClient s_Http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };

        public static async Task<int> Main(string[] args)
        {
            Console.OutputEncoding = new UTF8Encoding(false);
            if (args.Length == 0 || args[0] is "-h" or "--help" or "help-cli")
            {
                PrintUsage();
                return 0;
            }

            var (command, options) = ParseArgs(args);
            string project = FindProject(options.TryGetValue("project", out var p) ? p?.ToString() : null);
            options.Remove("project");

            if (command == "mcp") return await Mcp.Run(project);

            try
            {
                var response = await CallUnity(project, command.Replace('-', '_'), options);
                return PrintResponse(response);
            }
            catch (BridgeUnavailableException e)
            {
                Console.Error.WriteLine(e.Message);
                return 2;
            }
        }

        private static void PrintUsage()
        {
            Console.WriteLine(@"ndbt — ND Behavior Tree bridge

Usage:
  ndbt <command> [--arg value ...] [--json '{""arg"": ...}'] [--project <unity project>]
  ndbt mcp [--project <unity project>]      run as an MCP server (stdio)

Argument values are parsed as JSON when possible (numbers, true/false, [..], {..}), else as text.
--<name>-file <path> loads that argument from a JSON file, e.g. --ops-file ops.json.

Read:   status, help, node-list, node-info, tree-list, tree-get, tree-validate, validate-all,
        blackboard-get, find-usages, agent-list, agent-state
Write:  tree-create, tree-duplicate, tree-apply, subtree-extract, blackboard-key, history,
        tree-save, agent-send-event
Dialogue (ND Dialogue bridge, its own access settings): dlg-help, dlg-database-list, dlg-database-get,
        dlg-conversation-get, dlg-validate, dlg-entry-add, dlg-entry-set, dlg-link-add, dlg-condition-add, …

Examples:
  ndbt node-list --category decorator
  ndbt tree-get --tree Wolf --format outline
  ndbt tree-apply --tree Assets/AI/Wolf.asset --ops-file ops.json --dryRun true

Unity must be open with AI access unlocked (Project Settings → ND Behavior Tree → AI Access).");
        }

        private static (string command, Dictionary<string, object> options) ParseArgs(string[] args)
        {
            string command = args[0].ToLowerInvariant();
            var options = new Dictionary<string, object>(StringComparer.Ordinal);
            for (int i = 1; i < args.Length; i++)
            {
                if (!args[i].StartsWith("--")) throw new ArgumentException($"Unexpected argument '{args[i]}'. Use --name value.");
                string name = args[i].Substring(2);
                string value = i + 1 < args.Length && !args[i + 1].StartsWith("--") ? args[++i] : "true";

                if (name == "json")
                {
                    var obj = JsonNode.Parse(value) as JsonObject ?? throw new ArgumentException("--json must be a JSON object.");
                    foreach (var kvp in obj) options[kvp.Key] = kvp.Value?.DeepClone();
                }
                else if (name.EndsWith("-file"))
                {
                    options[name.Substring(0, name.Length - 5)] = JsonNode.Parse(File.ReadAllText(value));
                }
                else
                {
                    options[name] = TryParseJson(value);
                }
            }
            return (command, options);
        }

        private static object TryParseJson(string value)
        {
            string t = value.Trim();
            if (t.Length > 0 && (t[0] == '{' || t[0] == '[' || t == "true" || t == "false" || t == "null" || char.IsDigit(t[0]) || (t[0] == '-' && t.Length > 1 && char.IsDigit(t[1]))))
            {
                try { return JsonNode.Parse(t); } catch (JsonException) { /* plain text */ }
            }
            return value;
        }

        /// <summary>--project, NDBT_PROJECT, or the first parent of the current directory that is a Unity project.</summary>
        public static string FindProject(string explicitPath)
        {
            if (!string.IsNullOrEmpty(explicitPath)) return Path.GetFullPath(explicitPath);
            var env = Environment.GetEnvironmentVariable("NDBT_PROJECT");
            if (!string.IsNullOrEmpty(env)) return Path.GetFullPath(env);
            for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir != null; dir = dir.Parent)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "Assets")) && Directory.Exists(Path.Combine(dir.FullName, "ProjectSettings"))) return dir.FullName;
            }
            return Directory.GetCurrentDirectory();
        }

        public static async Task<JsonObject> CallUnity(string project, string command, IDictionary<string, object> args)
        {
            // "dlg_*" commands go to the ND Dialogue bridge (its own port, token and access level).
            bool dialogue = command.StartsWith("dlg_", StringComparison.Ordinal);
            string infoPath = Path.Combine(project, "Library", dialogue ? "NDDialogue" : "NDBT", "bridge.json");
            if (!File.Exists(infoPath))
                throw new BridgeUnavailableException(dialogue
                    ? $"The ND Dialogue bridge is not running for '{project}'. In Unity: Project Settings → ND Dialogue → Advanced → " +
                      "Enable AI Bridge, then on the AI Bridge page unlock with the owner password and choose an access level."
                    : $"The ND Behavior Tree bridge is not running for '{project}'. Open the project in Unity, then in " +
                      "Project Settings → ND Behavior Tree → AI Access unlock with the owner password and choose an access level.");

            var info = JsonNode.Parse(File.ReadAllText(infoPath))!.AsObject();
            int port = info["port"]!.GetValue<int>();
            string token = info["token"]!.GetValue<string>();

            var argsObject = new JsonObject();
            foreach (var kvp in args) argsObject[kvp.Key] = kvp.Value is JsonNode node ? node.DeepClone() : JsonValue.Create(kvp.Value?.ToString());
            var body = new JsonObject { ["command"] = command, ["args"] = argsObject };

            using var request = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{port}/")
            {
                Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")
            };
            request.Headers.Add("X-NDBT-Token", token);

            try
            {
                using var response = await s_Http.SendAsync(request);
                string text = await response.Content.ReadAsStringAsync();
                return JsonNode.Parse(text) as JsonObject ?? new JsonObject { ["ok"] = false, ["error"] = $"Unexpected response: {text}" };
            }
            catch (HttpRequestException e)
            {
                throw new BridgeUnavailableException($"Could not reach Unity on port {port} ({e.Message}). Unity may be reloading scripts — retry in a few seconds.");
            }
            catch (TaskCanceledException)
            {
                throw new BridgeUnavailableException("Unity did not answer in time (busy compiling or showing a dialog).");
            }
        }

        private static int PrintResponse(JsonObject response)
        {
            bool ok = response["ok"]?.GetValue<bool>() ?? false;
            if (!ok)
            {
                Console.Error.WriteLine("error: " + (response["error"]?.GetValue<string>() ?? response.ToJsonString()));
                return 1;
            }
            var result = response["result"];
            if (result is JsonValue value && value.TryGetValue(out string text)) Console.WriteLine(text);
            else Console.WriteLine(result?.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) ?? "null");
            return 0;
        }
    }

    public sealed class BridgeUnavailableException : Exception
    {
        public BridgeUnavailableException(string message) : base(message) { }
    }
}
