using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ND_BehaviorTree.Editor.Graph
{
    /// <summary>
    /// A detached snapshot of copied nodes. Changing or deleting the source nodes after Copy
    /// does not affect what is pasted.
    ///
    /// Snapshot rules (applied at Capture time):
    ///   - RootNode is never copied.
    ///   - Attached decorators and services of copied nodes are copied with them (order preserved).
    ///   - References to copied objects are remapped to their snapshot copies.
    ///   - References to Nodes outside the selection are cleared (no edge to an uncopied node).
    ///   - Other ScriptableObject sub-assets of the source tree file (except Keys/Blackboards) are deep-copied.
    ///   - Everything else (Keys, blackboards, scene/project assets) keeps its reference.
    /// </summary>
    public sealed class GraphClipboard : IDisposable
    {
        private readonly List<Node> m_GraphNodes = new List<Node>();
        private readonly List<Node> m_SourceNodes = new List<Node>();

        /// <summary>Index into GraphNodes (and the list returned by Paste) of the snapshot made from <paramref name="source"/>, or -1.</summary>
        public int IndexOfSource(Node source) => m_SourceNodes.IndexOf(source);
        private readonly List<ScriptableObject> m_AllObjects = new List<ScriptableObject>();

        /// <summary>Copied nodes that live in BehaviorTree.nodes (excludes services and owned sub-assets).</summary>
        public IReadOnlyList<Node> GraphNodes => m_GraphNodes;
        /// <summary>Every snapshot object, including services and deep-copied sub-assets.</summary>
        public IReadOnlyList<ScriptableObject> AllObjects => m_AllObjects;
        public int Count => m_GraphNodes.Count;
        public bool IsEmpty => m_GraphNodes.Count == 0 || m_GraphNodes.Any(n => n == null);

        private GraphClipboard() { }

        public static GraphClipboard Capture(BehaviorTree source, IEnumerable<Node> selection)
        {
            var clip = new GraphClipboard();
            var selected = selection.Where(n => n != null && !(n is RootNode)).Distinct().ToList();
            if (selected.Count == 0) return clip;

            string sourcePath = source != null && EditorUtility.IsPersistent(source) ? AssetDatabase.GetAssetPath(source) : null;
            var map = new Dictionary<Object, Object>();
            var pending = new Queue<ScriptableObject>();

            ScriptableObject Snapshot(ScriptableObject original)
            {
                var copy = Object.Instantiate(original);
                copy.name = original.name;
                copy.hideFlags = HideFlags.HideAndDontSave;
                map[original] = copy;
                clip.m_AllObjects.Add(copy);
                pending.Enqueue(copy);
                return copy;
            }

            foreach (var node in selected)
            {
                clip.m_GraphNodes.Add((Node)Snapshot(node));
                clip.m_SourceNodes.Add(node);
                foreach (var decorator in node.decorators.Where(d => d != null && !map.ContainsKey(d)))
                    Snapshot(decorator);
                foreach (var service in node.services.Where(s => s != null && !map.ContainsKey(s)))
                    Snapshot(service);
            }

            while (pending.Count > 0)
            {
                ReferenceRemapper.Remap(pending.Dequeue(), value =>
                {
                    if (map.TryGetValue(value, out var mapped)) return mapped;
                    if (value is Node) return null;
                    if (sourcePath != null && value is ScriptableObject so && IsOwnedSubAsset(so, sourcePath))
                        return Snapshot(so);
                    return value;
                });
            }

            return clip;
        }

        private static bool IsOwnedSubAsset(ScriptableObject obj, string treePath)
        {
            if (obj is Key || obj is Blackboard || obj is BehaviorTree) return false;
            return !AssetDatabase.IsMainAsset(obj) && AssetDatabase.GetAssetPath(obj) == treePath;
        }

        public Vector2 GetCenter()
        {
            if (m_GraphNodes.Count == 0) return Vector2.zero;
            Vector2 sum = Vector2.zero;
            foreach (var n in m_GraphNodes) sum += n.position.position;
            return sum / m_GraphNodes.Count;
        }

        public void Dispose()
        {
            foreach (var obj in m_AllObjects)
            {
                if (obj != null) Object.DestroyImmediate(obj);
            }
            m_AllObjects.Clear();
            m_GraphNodes.Clear();
            m_SourceNodes.Clear();
        }
    }

    internal static class ReferenceRemapper
    {
        /// <summary>
        /// Rewrites every serialized object reference on <paramref name="target"/> (including inside lists and
        /// [SerializeReference] objects) using <paramref name="resolve"/>. Does not record Undo.
        /// </summary>
        public static void Remap(Object target, Func<Object, Object> resolve)
        {
            var so = new SerializedObject(target);
            var it = so.GetIterator();
            bool enterChildren = true;
            while (it.Next(enterChildren))
            {
                enterChildren = it.propertyType != SerializedPropertyType.String;
                if (it.propertyType != SerializedPropertyType.ObjectReference || it.name == "m_Script") continue;
                var value = it.objectReferenceValue;
                if (value == null) continue;
                var resolved = resolve(value);
                if (resolved != value) it.objectReferenceValue = resolved;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
