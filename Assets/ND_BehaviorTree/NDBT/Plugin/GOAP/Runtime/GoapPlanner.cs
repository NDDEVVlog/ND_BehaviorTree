using System;
using System.Collections.Generic;

namespace ND_BehaviorTree.GOAP
{
    /// <summary>A condition on a world-state fact (index into the state array).</summary>
    public readonly struct GoapFactCondition
    {
        public readonly int Fact;
        public readonly ComparisonType Op;
        public readonly object Value;
        public GoapFactCondition(int fact, ComparisonType op, object value) { Fact = fact; Op = op; Value = value; }
        public bool IsMet(object[] state) => ComparisonHelper.PerformComparison(state[Fact], Value, Op);
    }

    /// <summary>A change to a world-state fact.</summary>
    public readonly struct GoapFactEffect
    {
        public readonly int Fact;
        public readonly GoapEffectOperation Op;
        public readonly object Value;
        public GoapFactEffect(int fact, GoapEffectOperation op, object value) { Fact = fact; Op = op; Value = value; }
        public object ApplyTo(object current) => Op == GoapEffectOperation.Add ? GoapPlanner.AddNumbers(current, Value) : Value;
    }

    /// <summary>An action as the search sees it.</summary>
    public sealed class GoapPlanAction
    {
        public float Cost;
        public GoapFactCondition[] Preconditions;
        public GoapFactEffect[] Effects;
    }

    /// <summary>
    /// A* over world states (arrays of fact values). Pure: no Unity objects, no blackboard — the planner node compiles
    /// its goals and actions into facts first. Heuristic: unmet goal conditions × cheapest action cost (not always
    /// admissible when one action satisfies several conditions, so the plan is cheap but not guaranteed optimal).
    /// </summary>
    public static class GoapPlanner
    {
        private sealed class SearchNode
        {
            public object[] State;
            public float G, F;
            public SearchNode Parent;
            public int Action;
            public int Order; // insertion order: deterministic tie-break
        }

        public static bool AllMet(object[] state, GoapFactCondition[] conditions)
        {
            for (int i = 0; i < conditions.Length; i++)
                if (!conditions[i].IsMet(state)) return false;
            return true;
        }

        private static int Unmet(object[] state, GoapFactCondition[] conditions)
        {
            int n = 0;
            for (int i = 0; i < conditions.Length; i++)
                if (!conditions[i].IsMet(state)) n++;
            return n;
        }

        /// <summary>A copy of <paramref name="state"/> with the effects applied.</summary>
        public static object[] Apply(object[] state, GoapFactEffect[] effects)
        {
            var next = (object[])state.Clone();
            for (int i = 0; i < effects.Length; i++) next[effects[i].Fact] = effects[i].ApplyTo(next[effects[i].Fact]);
            return next;
        }

        /// <summary>a + b keeping a's numeric type (int, long, float, double).</summary>
        public static object AddNumbers(object a, object b)
        {
            switch (a)
            {
                case int i: return i + Convert.ToInt32(b);
                case long l: return l + Convert.ToInt64(b);
                case float f: return f + Convert.ToSingle(b);
                case double d: return d + Convert.ToDouble(b);
                default: throw new InvalidOperationException($"[GoapPlanner] Cannot add to a value of type {a?.GetType().Name ?? "null"} (Add effects need a number key).");
            }
        }

        /// <summary>
        /// Indices into <paramref name="actions"/> that reach the goal at low cost; empty if the goal already holds;
        /// null if no plan was found within <paramref name="maxIterations"/> expansions (<paramref name="failure"/> says why).
        /// </summary>
        public static List<int> FindPlan(object[] start, GoapFactCondition[] goal, IReadOnlyList<GoapPlanAction> actions, int maxIterations, out string failure)
        {
            failure = null;
            if (AllMet(start, goal)) return new List<int>();

            float minCost = float.MaxValue;
            for (int i = 0; i < actions.Count; i++) minCost = Math.Min(minCost, Math.Max(0f, actions[i].Cost));
            if (actions.Count == 0) { failure = "there are no actions"; return null; }

            var open = new List<SearchNode>(); // binary min-heap by (F, Order)
            var bestG = new Dictionary<object[], float>(StateComparer.Instance);
            int order = 0;
            Push(open, new SearchNode { State = start, G = 0f, F = Unmet(start, goal) * minCost, Action = -1, Order = order++ });
            bestG[start] = 0f;

            int expansions = 0;
            while (open.Count > 0)
            {
                var current = Pop(open);
                if (bestG.TryGetValue(current.State, out float known) && known < current.G) continue; // stale entry
                if (AllMet(current.State, goal)) return Unwind(current);
                if (++expansions > maxIterations)
                {
                    failure = $"search limit reached ({maxIterations} expansions)";
                    return null;
                }

                for (int a = 0; a < actions.Count; a++)
                {
                    var action = actions[a];
                    if (!AllMet(current.State, action.Preconditions)) continue;
                    var next = Apply(current.State, action.Effects);
                    if (StateComparer.Instance.Equals(next, current.State)) continue; // changes nothing
                    float g = current.G + Math.Max(0f, action.Cost);
                    if (bestG.TryGetValue(next, out float seen) && seen <= g) continue;
                    bestG[next] = g;
                    Push(open, new SearchNode { State = next, G = g, F = g + Unmet(next, goal) * minCost, Parent = current, Action = a, Order = order++ });
                }
            }
            failure = "no sequence of actions reaches the goal";
            return null;
        }

        private static List<int> Unwind(SearchNode node)
        {
            var plan = new List<int>();
            for (var n = node; n.Parent != null; n = n.Parent) plan.Add(n.Action);
            plan.Reverse();
            return plan;
        }

        private static bool Less(SearchNode a, SearchNode b) => a.F < b.F || (a.F == b.F && a.Order < b.Order);

        private static void Push(List<SearchNode> heap, SearchNode node)
        {
            heap.Add(node);
            int i = heap.Count - 1;
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (!Less(heap[i], heap[parent])) break;
                (heap[i], heap[parent]) = (heap[parent], heap[i]);
                i = parent;
            }
        }

        private static SearchNode Pop(List<SearchNode> heap)
        {
            var top = heap[0];
            int last = heap.Count - 1;
            heap[0] = heap[last];
            heap.RemoveAt(last);
            int i = 0;
            while (true)
            {
                int l = 2 * i + 1, r = l + 1, smallest = i;
                if (l < heap.Count && Less(heap[l], heap[smallest])) smallest = l;
                if (r < heap.Count && Less(heap[r], heap[smallest])) smallest = r;
                if (smallest == i) break;
                (heap[i], heap[smallest]) = (heap[smallest], heap[i]);
                i = smallest;
            }
            return top;
        }

        private sealed class StateComparer : IEqualityComparer<object[]>
        {
            public static readonly StateComparer Instance = new StateComparer();

            public bool Equals(object[] x, object[] y)
            {
                if (ReferenceEquals(x, y)) return true;
                if (x == null || y == null || x.Length != y.Length) return false;
                for (int i = 0; i < x.Length; i++)
                    if (!object.Equals(x[i], y[i])) return false;
                return true;
            }

            public int GetHashCode(object[] state)
            {
                unchecked
                {
                    int hash = 17;
                    for (int i = 0; i < state.Length; i++) hash = hash * 31 + (state[i]?.GetHashCode() ?? 0);
                    return hash;
                }
            }
        }
    }
}
