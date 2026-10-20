using System;
using System.Collections.Generic;
using UnityEditor;

namespace ND_BehaviorTree.Editor.Graph
{
    /// <summary>
    /// Keys the user tracks in the Live Debug blackboard table (pinned to the top). Stored per Blackboard ASSET in
    /// EditorPrefs, so trees sharing a blackboard (a tree and its subtrees) share the list, and it survives restarts.
    /// </summary>
    public static class LiveKeyTracking
    {
        public static event Action Changed;

        private static readonly Dictionary<string, HashSet<string>> s_Cache = new Dictionary<string, HashSet<string>>();

        private static string GuidOf(Blackboard blackboardAsset)
        {
            if (blackboardAsset == null) return null;
            string path = AssetDatabase.GetAssetPath(blackboardAsset);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.AssetPathToGUID(path);
        }

        private static HashSet<string> Set(Blackboard blackboardAsset)
        {
            string guid = GuidOf(blackboardAsset);
            if (guid == null) return null;
            if (!s_Cache.TryGetValue(guid, out var set))
            {
                set = new HashSet<string>();
                foreach (var name in EditorPrefs.GetString("NDBT.TrackedKeys." + guid, string.Empty).Split('\n'))
                    if (!string.IsNullOrEmpty(name)) set.Add(name);
                s_Cache[guid] = set;
            }
            return set;
        }

        public static bool IsTracked(Blackboard blackboardAsset, string keyName) =>
            keyName != null && (Set(blackboardAsset)?.Contains(keyName) ?? false);

        public static void SetTracked(Blackboard blackboardAsset, string keyName, bool tracked)
        {
            var set = Set(blackboardAsset);
            if (set == null || string.IsNullOrEmpty(keyName)) return;
            if (tracked ? !set.Add(keyName) : !set.Remove(keyName)) return;
            EditorPrefs.SetString("NDBT.TrackedKeys." + GuidOf(blackboardAsset), string.Join("\n", set));
            Changed?.Invoke();
        }

        public static void Toggle(Blackboard blackboardAsset, string keyName) =>
            SetTracked(blackboardAsset, keyName, !IsTracked(blackboardAsset, keyName));
    }
}
