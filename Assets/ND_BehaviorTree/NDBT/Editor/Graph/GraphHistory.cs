using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ND_BehaviorTree.Editor.Graph
{
    /// <summary>
    /// Undo/redo history for one BehaviorTree's graph edits (replaces Unity's Undo for the graph).
    ///
    /// How it works:
    ///   - Every edit runs in a transaction (<see cref="GraphUndoScope"/>). At the start and the end, every object of the
    ///     tree (the tree itself, nodes, attachments, other sub-assets) is serialized with EditorJsonUtility.
    ///   - Only the DIFF is stored: objects that changed, were created or were destroyed (before/after JSON).
    ///     Undo/redo restore only those objects, so unrelated edits elsewhere are not reverted.
    ///   - Destroyed objects are recreated from JSON (and re-added to the .asset). Object references are recorded per
    ///     property path (not taken from the JSON) and re-linked through an alias table (old instance id → new), so every
    ///     restored reference points at a live object.
    ///   - Capacity: <see cref="Capacity"/> steps (Project Settings → ND Behavior Tree → History Capacity); oldest dropped.
    ///   - Consecutive edits with the same coalesce key within <see cref="CoalesceSeconds"/> merge into one step
    ///     (e.g. dragging a value in the property window).
    /// History lives for the editor session (cleared by a domain reload). Blackboard assets are not covered.
    /// </summary>
    public sealed class GraphHistory
    {
        public const double CoalesceSeconds = 1.0;

        private sealed class ObjectState
        {
            public int Id;
            public Type Type;
            public string Name;
            public string Json; // null = the object does not exist in this state
            // Object references, recorded separately from Json: EditorJsonUtility writes them as fileID/guid (sub-assets,
            // which change when an object is recreated) or {"instanceID":0} (in-memory objects), so Json alone cannot
            // restore them. Ids are alias-resolved original ids; RefSignature makes reference-only edits visible to the diff.
            public List<(string path, int id)> Refs;
            public string RefSignature;
        }

        private sealed class Entry
        {
            public string Name;
            public string CoalesceKey;
            public double Time;
            public Dictionary<int, ObjectState> Before = new Dictionary<int, ObjectState>();
            public Dictionary<int, ObjectState> After = new Dictionary<int, ObjectState>();
        }

        private static readonly Dictionary<int, GraphHistory> s_Histories = new Dictionary<int, GraphHistory>();

        private readonly BehaviorTree m_Tree;
        private readonly LinkedList<Entry> m_Undo = new LinkedList<Entry>();
        private readonly Stack<Entry> m_Redo = new Stack<Entry>();
        private readonly Dictionary<int, int> m_Alias = new Dictionary<int, int>();        // dead id → id of its recreation
        private readonly Dictionary<int, int> m_ReverseAlias = new Dictionary<int, int>(); // live id → id it replaced

        // Incremental capture: an object is re-serialized only when its dirty counter (or name) changed since it was
        // last captured. Edits inside a transaction must mark objects dirty (EditorUtility.SetDirty or
        // SerializedObject.Apply*), which every graph-service operation does.
        private readonly Dictionary<int, (int dirtyCount, ObjectState state)> m_Cache = new Dictionary<int, (int, ObjectState)>();

        private int m_Depth;
        private Dictionary<int, ObjectState> m_TransactionBefore;
        private string m_TransactionName;
        private string m_TransactionCoalesceKey;
        private int? m_CapacityOverride;

        /// <summary>Raised after any change to the stacks (push, undo, redo, clear).</summary>
        public event Action Changed;

        private GraphHistory(BehaviorTree tree) { m_Tree = tree; }

        public static GraphHistory For(BehaviorTree tree)
        {
            if (tree == null) throw new ArgumentNullException(nameof(tree));
            int key = tree.GetInstanceID();
            if (!s_Histories.TryGetValue(key, out var history) || history.m_Tree == null)
            {
                history = new GraphHistory(tree);
                s_Histories[key] = history;
            }
            return history;
        }

        public static void ClearAll()
        {
            foreach (var history in s_Histories.Values) history.Clear();
        }

        /// <summary>Max undo steps. Defaults to the project setting; tests may override.</summary>
        public int Capacity
        {
            get => Mathf.Max(1, m_CapacityOverride ?? ND_BehaviorTreeSetting.Instance.historyCapacity);
            set => m_CapacityOverride = value;
        }

        public int UndoCount => m_Undo.Count;
        public int RedoCount => m_Redo.Count;
        public bool CanUndo => m_Undo.Count > 0 && m_Depth == 0;
        public bool CanRedo => m_Redo.Count > 0 && m_Depth == 0;
        public string NextUndoName => m_Undo.Last?.Value.Name;
        public string NextRedoName => m_Redo.Count > 0 ? m_Redo.Peek().Name : null;

        /// <summary>Increments whenever a step is recorded, merged, undone or redone.</summary>
        public int Version { get; private set; }

        /// <summary>
        /// Reverts the most recent step and forgets it (no redo). Used to roll back a failed or dry-run batch.
        /// </summary>
        public bool RollbackLastStep()
        {
            if (!CanUndo) return false;
            var entry = m_Undo.Last.Value;
            m_Undo.RemoveLast();
            Apply(entry.Before);
            Version++;
            Changed?.Invoke();
            return true;
        }

        public void Clear()
        {
            m_Undo.Clear();
            m_Redo.Clear();
            Version++;
            Changed?.Invoke();
        }

        #region Transactions

        internal void Begin(string name, string coalesceKey)
        {
            if (m_Depth++ > 0) return; // nested: the outer transaction owns the step
            m_TransactionName = name;
            m_TransactionCoalesceKey = coalesceKey;
            m_TransactionBefore = Capture();
        }

        internal void End()
        {
            if (m_Depth == 0) { Debug.LogError("[GraphHistory] End() without Begin()."); return; }
            if (--m_Depth > 0) return;

            var before = m_TransactionBefore;
            m_TransactionBefore = null;
            var after = Capture();

            var entry = new Entry { Name = m_TransactionName, CoalesceKey = m_TransactionCoalesceKey, Time = EditorApplication.timeSinceStartup };
            foreach (int id in before.Keys.Union(after.Keys))
            {
                before.TryGetValue(id, out var b);
                after.TryGetValue(id, out var a);
                if (b != null && a != null && b.Json == a.Json && b.RefSignature == a.RefSignature && b.Name == a.Name) continue;
                entry.Before[id] = b ?? Missing(a);
                entry.After[id] = a ?? Missing(b);
            }
            if (entry.Before.Count == 0) return; // nothing changed: no step

            m_Redo.Clear();
            var last = m_Undo.Last?.Value;
            if (last != null && entry.CoalesceKey != null && last.CoalesceKey == entry.CoalesceKey && entry.Time - last.Time <= CoalesceSeconds)
            {
                foreach (var kvp in entry.Before) if (!last.Before.ContainsKey(kvp.Key)) last.Before[kvp.Key] = kvp.Value;
                foreach (var kvp in entry.After) last.After[kvp.Key] = kvp.Value;
                last.Time = entry.Time;
            }
            else
            {
                m_Undo.AddLast(entry);
                while (m_Undo.Count > Capacity) m_Undo.RemoveFirst();
            }
            Version++;
            Changed?.Invoke();
        }

        private static ObjectState Missing(ObjectState other) => new ObjectState { Id = other.Id, Type = other.Type, Name = other.Name, Json = null };

        #endregion

        #region Undo / Redo

        public bool Undo()
        {
            if (!CanUndo) return false;
            var entry = m_Undo.Last.Value;
            m_Undo.RemoveLast();
            Apply(entry.Before);
            m_Redo.Push(entry);
            Version++;
            Changed?.Invoke();
            return true;
        }

        public bool Redo()
        {
            if (!CanRedo) return false;
            var entry = m_Redo.Pop();
            Apply(entry.After);
            m_Undo.AddLast(entry);
            Version++;
            Changed?.Invoke();
            return true;
        }

        private void Apply(Dictionary<int, ObjectState> states)
        {
            bool persistent = EditorUtility.IsPersistent(m_Tree);

            // 1. Make sure every object that must exist exists (recreate destroyed ones, remember the alias).
            var targets = new Dictionary<ObjectState, Object>();
            foreach (var state in states.Values.Where(s => s.Json != null))
            {
                var obj = Resolve(state.Id);
                if (obj == null)
                {
                    obj = ScriptableObject.CreateInstance(state.Type);
                    obj.name = state.Name;
                    if (persistent) AssetDatabase.AddObjectToAsset(obj, m_Tree);
                    int deadId = CurrentId(state.Id);
                    m_Alias[deadId] = obj.GetInstanceID();
                    m_ReverseAlias[obj.GetInstanceID()] = deadId;
                }
                targets[state] = obj;
            }
            foreach (var state in states.Values) m_Cache.Remove(state.Id); // restored objects must be re-captured

            // 2. Overwrite their data, then re-link every object reference to the live (possibly recreated) object.
            foreach (var kvp in targets)
            {
                EditorJsonUtility.FromJsonOverwrite(kvp.Key.Json, kvp.Value);
                kvp.Value.name = kvp.Key.Name;
            }
            foreach (var kvp in targets)
            {
                RestoreRefs(kvp.Value, kvp.Key.Refs);
                EditorUtility.SetDirty(kvp.Value);
            }

            // 3. Destroy objects that must not exist.
            foreach (var state in states.Values.Where(s => s.Json == null))
            {
                var obj = Resolve(state.Id);
                if (obj != null && obj != m_Tree) Object.DestroyImmediate(obj, true);
            }
            EditorUtility.SetDirty(m_Tree);
        }

        /// <summary>Follows the alias chain to the instance id the object has now.</summary>
        private int CurrentId(int id)
        {
            int guard = 0;
            while (m_Alias.TryGetValue(id, out int next) && guard++ < 64) id = next;
            return id;
        }

        // Deprecated in Unity 6.3 in favour of EntityId, but still the way to resolve any live object from the int ids
        // that EditorJsonUtility writes into the snapshots. Revisit when the int API is removed.
#pragma warning disable CS0618
        private Object Resolve(int id) => EditorUtility.InstanceIDToObject(CurrentId(id));
#pragma warning restore CS0618

        /// <summary>Sets every recorded reference by property path; anything not recorded stays as the JSON left it (null).</summary>
        private void RestoreRefs(Object obj, List<(string path, int id)> refs)
        {
            var so = new SerializedObject(obj);
            // Clear references the JSON may have left pointing at stale objects, then apply the recorded ones.
            var it = so.GetIterator();
            while (it.Next(true))
            {
                if (it.propertyType == SerializedPropertyType.ObjectReference && it.propertyPath != "m_Script" && it.objectReferenceValue != null)
                    it.objectReferenceValue = null;
            }
            if (refs != null)
            {
                foreach (var (path, id) in refs)
                {
                    var prop = so.FindProperty(path);
                    if (prop != null && prop.propertyType == SerializedPropertyType.ObjectReference) prop.objectReferenceValue = Resolve(id);
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private List<(string path, int id)> CaptureRefs(Object obj, out string signature)
        {
            var refs = new List<(string, int)>();
            var sb = new System.Text.StringBuilder();
            var so = new SerializedObject(obj);
            var it = so.GetIterator();
            while (it.Next(true))
            {
                if (it.propertyType != SerializedPropertyType.ObjectReference || it.propertyPath == "m_Script") continue;
                var target = it.objectReferenceValue;
                if (target == null) continue;
                int id = OriginalId(target.GetInstanceID());
                refs.Add((it.propertyPath, id));
                sb.Append(it.propertyPath).Append('=').Append(id).Append(';');
            }
            signature = sb.ToString();
            return refs;
        }

        #endregion

        #region Capture

        /// <summary>
        /// All objects that belong to the tree, keyed by their original identity (alias-resolved).
        /// Unchanged objects reuse their cached state, so the cost is proportional to what changed.
        /// </summary>
        private Dictionary<int, ObjectState> Capture()
        {
            var result = new Dictionary<int, ObjectState>();
            foreach (var obj in CollectObjects())
            {
                int id = OriginalId(obj.GetInstanceID());
                if (result.ContainsKey(id)) continue;

                int dirty = EditorUtility.GetDirtyCount(obj);
                if (m_Cache.TryGetValue(id, out var cached) && cached.dirtyCount == dirty && cached.state.Name == obj.name && cached.state.Type == obj.GetType())
                {
                    result[id] = cached.state;
                    continue;
                }

                var state = new ObjectState { Id = id, Type = obj.GetType(), Name = obj.name, Json = EditorJsonUtility.ToJson(obj) };
                state.Refs = CaptureRefs(obj, out state.RefSignature);
                m_Cache[id] = (dirty, state);
                result[id] = state;
            }

            // Forget objects that no longer exist so the cache cannot grow without bound.
            if (m_Cache.Count > result.Count)
            {
                var stale = new List<int>();
                foreach (var id in m_Cache.Keys) if (!result.ContainsKey(id)) stale.Add(id);
                foreach (var id in stale) m_Cache.Remove(id);
            }
            return result;
        }

        /// <summary>
        /// Maps a live instance id back to the id it was first recorded with, so states recorded before an object
        /// was recreated still match it. (Inverse of the alias chain; O(chain length).)
        /// </summary>
        private int OriginalId(int liveId)
        {
            int guard = 0;
            while (m_ReverseAlias.TryGetValue(liveId, out int previous) && guard++ < 64) liveId = previous;
            return liveId;
        }

        private IEnumerable<Object> CollectObjects()
        {
            if (EditorUtility.IsPersistent(m_Tree))
            {
                foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(m_Tree)))
                    if (obj != null) yield return obj;
                yield break;
            }

            // In-memory tree (tests / dry runs): the tree, its nodes and their attachments.
            yield return m_Tree;
            foreach (var node in m_Tree.nodes)
            {
                if (node == null) continue;
                yield return node;
                foreach (var s in node.services) if (s != null) yield return s;
                foreach (var d in node.decorators) if (d != null) yield return d;
            }
        }

        #endregion
    }
}
