using System.Collections.Generic;
using UnityEngine;

namespace ND_BehaviorTree
{
    /// <summary>
    /// One runtime instance per shared (parent) blackboard asset, created on first use and kept for the play session.
    /// Agents whose blackboard has that asset as <see cref="Blackboard.parent"/> resolve world keys into it, and
    /// non-tree systems (dialogue, quests, UI) get the same instance here.
    /// </summary>
    public static class SharedBlackboards
    {
        private static readonly Dictionary<Blackboard, Blackboard> s_Runtime = new Dictionary<Blackboard, Blackboard>();

        /// <summary>The play-session instance of <paramref name="asset"/> (created from the asset's values on first call).</summary>
        public static Blackboard GetRuntime(Blackboard asset)
        {
            if (asset == null)
            {
                Debug.LogError("[SharedBlackboards] GetRuntime called with a null blackboard asset.");
                return null;
            }
            if (s_Runtime.TryGetValue(asset, out var runtime) && runtime != null) return runtime;
            if (s_Runtime.ContainsValue(asset)) return asset; // already a runtime instance

            runtime = asset.Clone();
            runtime.name = $"{asset.name} (Shared Runtime)";
            s_Runtime[asset] = runtime;
            return runtime;
        }

        public static bool TryGetExisting(Blackboard asset, out Blackboard runtime)
        {
            runtime = null;
            return asset != null && s_Runtime.TryGetValue(asset, out runtime) && runtime != null;
        }

        /// <summary>Destroys every shared instance (and its keys). Called when play mode ends and before a new session.</summary>
        public static void Clear()
        {
            foreach (var runtime in s_Runtime.Values)
            {
                if (runtime == null) continue;
                foreach (var key in runtime.keys) DestroyObject(key);
                DestroyObject(runtime);
            }
            s_Runtime.Clear();
        }

        private static void DestroyObject(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Object.Destroy(obj);
            else Object.DestroyImmediate(obj); // edit-mode tests
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            s_Runtime.Clear(); // with domain reload off, instances from the last session are already gone
            Application.quitting -= Clear;
            Application.quitting += Clear;
        }
    }
}
