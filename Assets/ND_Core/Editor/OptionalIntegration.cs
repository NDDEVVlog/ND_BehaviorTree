using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace ND.Editor
{
    /// <summary>
    /// The gate for code that needs another plugin (another ND plugin, FMOD, Wwise…) living in <c>Assets/</c>, where asmdef
    /// <c>versionDefines</c> cannot see it (they only see Unity packages). The integration code lives in its own assembly
    /// with <c>defineConstraints: [Define]</c> and a reference to the other plugin's assembly; <see cref="Define"/> is set
    /// only while that assembly exists, so a project without the plugin never compiles (or breaks on) that code.
    /// <list type="bullet">
    /// <item><b>Automatic</b> (ND plugins): on whenever the other plugin is in the project, off when it is removed.</item>
    /// <item><b>Manual</b> (FMOD…): a settings switch turns it on, only while the plugin is there.</item>
    /// </list>
    /// The define is the single source of truth for "on". If the plugin is removed while on, the define is removed after
    /// the next compilation so the project builds again.
    /// </summary>
    public sealed class OptionalIntegration
    {
        private const string LogTag = "[ND Integration]";
        private static readonly List<OptionalIntegration> s_All = new List<OptionalIntegration>();

        /// <summary>Scripting define that compiles the integration (e.g. ND_DIALOGUE_FMOD).</summary>
        public string Define { get; }
        /// <summary>Assembly (asmdef name) of the third-party plugin, e.g. FMODUnity.</summary>
        public string RequiredAssembly { get; }
        public string DisplayName { get; }
        /// <summary>On by itself whenever <see cref="RequiredAssembly"/> exists (no switch).</summary>
        public bool Automatic { get; }

        public OptionalIntegration(string displayName, string define, string requiredAssembly, bool automatic = false)
        {
            DisplayName = displayName;
            Define = define;
            RequiredAssembly = requiredAssembly;
            Automatic = automatic;
            s_All.Add(this);
        }

        /// <summary>The plugin's assembly is in the project (read from the asmdefs on disk, not from loaded code).</summary>
        public bool IsAvailable => !string.IsNullOrEmpty(CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(RequiredAssembly));

        public bool IsEnabled => ScriptingDefines.Has(Define);

        /// <summary>Turns the integration on (only when the plugin is there) or off. Recompiles when it changes.</summary>
        public bool SetEnabled(bool enabled)
        {
            if (enabled && !IsAvailable)
            {
                Debug.LogError($"{LogTag} {DisplayName}: cannot turn on, assembly '{RequiredAssembly}' is not in the project. Import the plugin first.");
                return false;
            }
            return ScriptingDefines.Set(Define, enabled);
        }

        [InitializeOnLoadMethod]
        private static void Watch()
        {
            CompilationPipeline.compilationFinished += _ => Sync();
            EditorApplication.delayCall += Sync;
        }

        /// <summary>
        /// Automatic integrations follow their plugin (on when it is there, off when not). Any integration left on after its
        /// plugin was removed would break every build: switch it off.
        /// </summary>
        private static void Sync()
        {
            foreach (var integration in s_All)
            {
                bool available = integration.IsAvailable, enabled = integration.IsEnabled;
                if (integration.Automatic && available && !enabled)
                {
                    Debug.Log($"{LogTag} {integration.DisplayName} found (assembly '{integration.RequiredAssembly}'): adding '{integration.Define}'.");
                    ScriptingDefines.Set(integration.Define, true);
                }
                else if (enabled && !available)
                {
                    Debug.LogWarning($"{LogTag} {integration.DisplayName} was on but assembly '{integration.RequiredAssembly}' is gone: removing '{integration.Define}'.");
                    ScriptingDefines.Set(integration.Define, false);
                }
            }
        }
    }
}
