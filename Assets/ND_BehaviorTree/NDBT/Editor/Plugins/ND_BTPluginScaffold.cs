using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace ND_BehaviorTree.Editor.Plugins
{
    /// <summary>
    /// Generates a ready-to-compile plugin skeleton: Runtime + Editor assemblies (+ optional Tests), an example node,
    /// the <see cref="ND_BTPlugin"/> entry point (+ optional custom node view and stylesheet).
    /// <see cref="Generate"/> is pure (testable); <see cref="Create"/> writes the files and imports them.
    /// </summary>
    public static class ND_BTPluginScaffold
    {
        public struct Options
        {
            public bool IncludeNodeView;
            public bool IncludeTests;
        }

        private static readonly Regex s_ValidName = new Regex("^[A-Z][A-Za-z0-9]{1,39}$");
        private static readonly string[] s_Reserved = { "Editor", "Runtime", "Tests", "Graph", "Plugins", "Story", "Node", "Nodes", "Theme" };

        /// <summary>Default folder for new plugins: the "Plugin" folder next to the core Runtime folder.</summary>
        public static string DefaultRoot()
        {
            string runtimeAsmdef = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName("ND_BehaviorTree.Runtime");
            if (string.IsNullOrEmpty(runtimeAsmdef)) return "Assets/ND_BehaviorTree/NDBT/Plugin";
            string packageRoot = Path.GetDirectoryName(Path.GetDirectoryName(runtimeAsmdef));
            return (packageRoot + "/Plugin").Replace('\\', '/');
        }

        /// <summary>Null when <paramref name="name"/> can be used for a new plugin under <paramref name="root"/>, else why not.</summary>
        public static string Validate(string name, string root)
        {
            if (string.IsNullOrEmpty(name) || !s_ValidName.IsMatch(name))
                return "Use a PascalCase name: a capital letter, then letters or digits (2-40 characters), e.g. 'DialogueNodes'.";
            if (s_Reserved.Contains(name)) return $"'{name}' is reserved by the core package; pick another name.";
            if (Directory.Exists(Path.Combine(root ?? string.Empty, name))) return $"The folder '{root}/{name}' already exists.";
            string asm = AssemblyName(name);
            if (CompilationPipeline.GetAssemblies().Any(a => a.name == asm || a.name.StartsWith(asm + ".")))
                return $"An assembly named '{asm}' already exists.";
            return null;
        }

        public static string AssemblyName(string name) => "ND_BehaviorTree." + name;

        /// <summary>Relative path → file content for a new plugin called <paramref name="name"/>.</summary>
        public static Dictionary<string, string> Generate(string name, Options options)
        {
            string asm = AssemblyName(name);
            string ns = "ND_BehaviorTree." + name;
            string nice = ObjectNames.NicifyVariableName(name);
            string node = name + "ExampleNode";
            string cssClass = Regex.Replace(name, "(?<!^)([A-Z])", "-$1").ToLowerInvariant() + "-node";

            var files = new Dictionary<string, string>
            {
                [$"Runtime/{asm}.asmdef"] = Asmdef(asm, ns, new[] { "ND_BehaviorTree.Runtime" }, editorOnly: false),
                [$"Runtime/{node}.cs"] =
$@"using UnityEngine;

namespace {ns}
{{
    // A node needs [NodeInfo] (title, search path, input port, output port) or it has no ports in the graph.
    [NodeInfo(""{nice} Example"", ""{name}/Example"", true, false)]
    [NodeDescription(""Example action of the {nice} plugin: logs a message and succeeds."",
        ""Replace with your own logic. OnEnter runs once when the node starts, OnProcess every tick while it returns Running, OnExit when it stops."")]
    public class {node} : ActionNode
    {{
        [Tooltip(""Text written to the Console."")]
        public string message = ""Hello from {nice}"";

        protected override Status OnProcess()
        {{
            Debug.Log($""[{node}] {{message}}"", this);
            return Status.Success;
        }}
    }}
}}
",
                [$"Editor/{asm}.Editor.asmdef"] = Asmdef(asm + ".Editor", ns + ".Editor", new[] { "ND.GraphUI", "ND_BehaviorTree.Runtime", "ND_BehaviorTreeEditor", asm }, editorOnly: true),
                [$"Editor/{name}Plugin.cs"] =
$@"using ND_BehaviorTree.Editor.Plugins;
using UnityEngine;

namespace {ns}.Editor
{{
    /// <summary>
    /// Entry point of the {nice} plugin. Found automatically (no registration): declare everything the plugin adds
    /// to the editor in Register. Node types, [CustomNodeView] views and stylesheets need no registration.
    /// </summary>
    public sealed class {name}Plugin : ND_BTPlugin
    {{
        public override string DisplayName => ""{nice}"";
        public override string Version => ""0.1.0"";
        public override string Description => ""TODO: one line about what this plugin adds."";

        public override void Register(ND_BTPluginContext context)
        {{
            context.AddNodeMenuItem<{node}>(""{nice}/Log Message Now"",
                args => Debug.Log((({node})args.Node).message));

            // More doors (uncomment what you need):
            // context.AddTheme(""{nice}"", () => new ND_BehaviorTree.Editor.ThemeData {{ ... }});
            // context.OnNodeCreated<{node}>((graph, created) => {{ /* change the graph via graph.*: same undo step as the creation */ }});
            // context.OnNodeEdited<{node}>((graph, edited) => {{ /* react to Node Properties edits (same undo step) */ }});
            // context.AddCanvasMenuItem(""{nice}/Do Something"", args => {{ /* args.Graph, args.Position, args.Selection */ }});
            // context.AddToolbarItem(""{nice}/Do Something"", args => {{ /* args.Tree may be null */ }});
            // context.AddValidationRule(""My rule"", tree => System.Linq.Enumerable.Empty<ND_BehaviorTree.Editor.Graph.GraphIssue>());
        }}
    }}
}}
",
            };

            if (options.IncludeNodeView)
            {
                files[$"Editor/{node}View.cs"] =
$@"using ND_BehaviorTree.Editor;
using ND_BehaviorTree.Editor.CustomGraph;

namespace {ns}.Editor
{{
    /// <summary>Custom look for {node}: picked automatically through [CustomNodeView].</summary>
    [CustomNodeView(typeof({node}))]
    public class {node}View : ND_BTNodeView
    {{
        public {node}View(Node node, ViewThemeData themeData) : base(node, themeData) {{ }}

        protected override void ApplyThemeAndLayout()
        {{
            base.ApplyThemeAndLayout();
            ND_BTStyles.Apply(this, ND_BTStyles.Find(""{name}Nodes"")); // Styles/{name}Nodes.uss (found by file name)
            AddToClassList(""{cssClass}"");
        }}
    }}
}}
";
                files[$"Editor/Styles/{name}Nodes.uss"] =
$@"/* {nice} plugin node styles. Start selectors with the view class so they win over theme stylesheets. */
.{cssClass} > .node-border {{
    border-color: #3FA7D6;
}}
";
            }

            if (options.IncludeTests)
            {
                files[$"Tests/Editor/{asm}.Tests.asmdef"] =
$@"{{
    ""name"": ""{asm}.Tests"",
    ""rootNamespace"": ""{ns}.Tests"",
    ""references"": [
        ""ND.GraphUI"",
        ""ND_BehaviorTree.Runtime"",
        ""ND_BehaviorTreeEditor"",
        ""{asm}"",
        ""{asm}.Editor"",
        ""UnityEngine.TestRunner"",
        ""UnityEditor.TestRunner""
    ],
    ""includePlatforms"": [
        ""Editor""
    ],
    ""excludePlatforms"": [],
    ""allowUnsafeCode"": false,
    ""overrideReferences"": true,
    ""precompiledReferences"": [
        ""nunit.framework.dll""
    ],
    ""autoReferenced"": false,
    ""defineConstraints"": [
        ""UNITY_INCLUDE_TESTS""
    ],
    ""versionDefines"": [],
    ""noEngineReferences"": false
}}
";
                files[$"Tests/Editor/{name}Tests.cs"] =
$@"using System.Linq;
using System.Text.RegularExpressions;
using ND_BehaviorTree.Editor.Plugins;
using {ns}.Editor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace {ns}.Tests
{{
    // Runs with ND_BehaviorTree > Diagnostics > Run Editor Tests (assemblies named ND_BehaviorTree*.Tests are picked up).
    public class {name}Tests
    {{
        [Test]
        public void Plugin_IsRegistered() =>
            Assert.IsTrue(ND_BTPluginRegistry.Entries.Any(e => e.Plugin is {name}Plugin && e.Installed));

        [Test]
        public void ExampleNode_LogsAndSucceeds()
        {{
            var node = ScriptableObject.CreateInstance<{node}>();
            try
            {{
                LogAssert.Expect(LogType.Log, new Regex(Regex.Escape(node.message)));
                Assert.AreEqual(Node.Status.Success, node.Process());
            }}
            finally {{ Object.DestroyImmediate(node); }}
        }}
    }}
}}
";
            }
            return files;
        }

        private static string Asmdef(string name, string rootNamespace, string[] references, bool editorOnly) =>
$@"{{
    ""name"": ""{name}"",
    ""rootNamespace"": ""{rootNamespace}"",
    ""references"": [
{string.Join(",\n", references.Select(r => $"        \"{r}\""))}
    ],
    ""includePlatforms"": [{(editorOnly ? "\n        \"Editor\"\n    " : "")}],
    ""excludePlatforms"": [],
    ""allowUnsafeCode"": false,
    ""overrideReferences"": false,
    ""precompiledReferences"": [],
    ""autoReferenced"": true,
    ""defineConstraints"": [],
    ""versionDefines"": [],
    ""noEngineReferences"": false
}}
";

        /// <summary>Writes the skeleton to <paramref name="root"/>/<paramref name="name"/> and imports it. Returns the plugin folder, or null (logged) if refused.</summary>
        public static string Create(string name, string root, Options options)
        {
            string error = Validate(name, root);
            if (error != null)
            {
                Debug.LogError($"[ND_BTPluginScaffold] Cannot create plugin '{name}': {error}");
                return null;
            }
            string folder = $"{root}/{name}";
            foreach (var file in Generate(name, options))
            {
                string path = Path.Combine(folder, file.Key);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, file.Value.Replace("\r\n", "\n"));
            }
            AssetDatabase.Refresh();
            Debug.Log($"[ND_BTPluginScaffold] Created plugin '{name}' in {folder}. Unity compiles it now; it appears in ND_BehaviorTree > Plugins after the reload.");
            return folder;
        }
    }
}
