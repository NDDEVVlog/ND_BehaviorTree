using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using ND_BehaviorTree.Editor.CustomGraph;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;

namespace ND_BehaviorTree.Editor.Graph
{
    /// <summary>
    /// ND_BehaviorTree → Diagnostics → Measure Editor Costs.
    /// Measures time and managed allocations (GC bytes) of the editor's recurring work for the tree open in the
    /// Behavior Tree window, and prints one report to the Console. Read-only: the tree is not modified.
    /// </summary>
    public static class EditorCostDiagnostics
    {
        private const int Iterations = 50;

        [MenuItem("ND_BehaviorTree/Diagnostics/Measure Editor Costs", false, 200)]
        public static void Measure()
        {
            var window = Resources.FindObjectsOfTypeAll<ND_BehaviorTreeEditorWindow>().FirstOrDefault(w => w.currentGraph != null);
            if (window == null)
            {
                Debug.LogWarning("[ND_BehaviorTree] Open a tree in the Behavior Tree window, then run Diagnostics again.");
                return;
            }

            var tree = window.currentGraph;
            var canvas = window.rootVisualElement.Q<ND_BTGraphCanvas>();
            int objectCount = EditorUtility.IsPersistent(tree) ? AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(tree)).Length : tree.nodes.Count + 1;

            var report = new StringBuilder();
            report.AppendLine($"[ND_BehaviorTree] Editor cost report for '{tree.name}' ({objectCount} objects, {tree.nodes.Count} graph nodes), {Iterations} iterations each:");
            report.AppendLine("  operation                                   avg ms     avg GC bytes");

            if (canvas != null)
            {
                Row(report, "idle refresh (nothing changed)", () => canvas.RefreshInfoLabels());
                Row(report, "forced refresh (labels, key slots, cards)", () => canvas.RefreshInfoLabels(force: true));
                Row(report, "change signature", () => canvas.ComputeChangeSignature());
                if (window.IsLiveDebugging) Row(report, "live debug update (one tick)", () => canvas.UpdateLiveDebug(window.CurrentDebugRuntime));
            }

            // History: an empty transaction measures the capture cost (no step is recorded when nothing changed).
            Row(report, "history transaction, nothing changed", () => { using (new GraphUndoScope(tree, "diagnostics")) { } });
            Row(report, "history transaction, 1 node changed", () =>
            {
                var node = tree.nodes.FirstOrDefault(n => n != null);
                using (new GraphUndoScope(tree, "diagnostics")) { if (node != null) EditorUtility.SetDirty(node); }
            });
            Row(report, "validation", () => GraphValidator.Validate(tree));

            var history = GraphHistory.For(tree);
            report.AppendLine($"  history: {history.UndoCount}/{history.Capacity} undo steps, {history.RedoCount} redo steps");
            Debug.Log(report.ToString(), tree);
        }

        private static void Row(StringBuilder report, string label, Action action)
        {
            action(); // warm-up (fills caches)
            long allocBefore = GC.GetAllocatedBytesForCurrentThread();
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < Iterations; i++) action();
            sw.Stop();
            long alloc = GC.GetAllocatedBytesForCurrentThread() - allocBefore;
            report.AppendLine($"  {label,-42} {sw.Elapsed.TotalMilliseconds / Iterations,8:0.000} {alloc / Iterations,14:N0}");
        }
    }
}
