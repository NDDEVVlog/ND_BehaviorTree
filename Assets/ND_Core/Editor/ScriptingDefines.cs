using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace ND.Editor
{
    /// <summary>
    /// Adds and removes scripting define symbols on every common build target at once, so a feature switched on in
    /// settings compiles the same in the editor and in builds. Each change recompiles scripts.
    /// </summary>
    public static class ScriptingDefines
    {
        private const string LogTag = "[ND ScriptingDefines]";

        private static readonly NamedBuildTarget[] s_Targets =
        {
            NamedBuildTarget.Standalone, NamedBuildTarget.Android, NamedBuildTarget.iOS, NamedBuildTarget.WebGL,
            NamedBuildTarget.Server, NamedBuildTarget.tvOS, NamedBuildTarget.VisionOS
        };

        /// <summary>The define is set for the build target the editor uses now.</summary>
        public static bool Has(string define) => Read(Current).Contains(define);

        /// <summary>Sets or clears <paramref name="define"/> on every common build target. True when something changed.</summary>
        public static bool Set(string define, bool enabled)
        {
            if (string.IsNullOrWhiteSpace(define) || define.IndexOfAny(new[] { ';', ' ', ',' }) >= 0)
            {
                Debug.LogError($"{LogTag} {nameof(Set)} rejected: '{define}' is not a valid define symbol.");
                return false;
            }
            bool changed = false;
            foreach (var target in s_Targets.Append(Current).Distinct())
            {
                var defines = Read(target);
                if (enabled == defines.Contains(define)) continue;
                if (enabled) defines.Add(define);
                else defines.Remove(define);
                PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", defines));
                changed = true;
            }
            if (changed) Debug.Log($"{LogTag} {(enabled ? "Added" : "Removed")} '{define}' (scripts recompile).");
            return changed;
        }

        private static NamedBuildTarget Current =>
            NamedBuildTarget.FromBuildTargetGroup(BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget));

        private static List<string> Read(NamedBuildTarget target) =>
            PlayerSettings.GetScriptingDefineSymbols(target)
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(d => d.Trim())
                .Where(d => d.Length > 0)
                .ToList();
    }
}
