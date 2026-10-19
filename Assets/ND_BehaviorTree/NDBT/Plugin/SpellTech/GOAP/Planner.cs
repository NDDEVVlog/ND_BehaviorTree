
using System.Collections.Generic;
using UnityEngine;

namespace ND_BehaviorTree.GOAP
{
    public class Planner
    {
        /// <summary>
        /// A safeguard to prevent the planner from getting stuck in an infinite loop
        /// or taking too long on a very complex plan.
        /// </summary>
        private const int MAX_ITERATIONS = 1000;

        /// <summary>
        /// A node within the A* search tree.
        /// It contains the world state, the action that led to this state,
        /// the cost to get here, and its parent node.
        /// </summary>
        private class PlanNode
        {
            public PlanNode Parent;
            public float Cost; // g(n): Cost from the start node to the current node
            public float F;    // f(n) = g(n) + h(n), computed once when the node is created
            public Dictionary<string, object> State;
            public int StateHash;
            public GOAPActionNode Action;

            public PlanNode(PlanNode parent, float cost, Dictionary<string, object> state, GOAPActionNode action)
            {
                Parent = parent;
                Cost = cost;
                State = state;
                StateHash = ComputeStateHash(state);
                Action = action;
            }
        }

        /// <summary>
        /// Finds a sequence of actions (a plan) to get from a starting state to a goal state.
        /// </summary>
        /// <param name="agent">The GameObject running the AI, used for context-sensitive conditions (like distance).</param>
        /// <param name="blackboard">The blackboard instance to resolve world state values from.</param>
        /// <param name="startState">The initial world state, derived from the Blackboard.</param>
        /// <param name="goal">A list of conditions that define the goal state.</param>
        /// <param name="availableActions">All actions the AI can perform.</param>
        /// <returns>
        /// A list of GOAPActionNodes (the plan). An EMPTY list means the goal is already satisfied.
        /// Null means no plan was found.
        /// </returns>
        public List<GOAPActionNode> FindPlan(GameObject agent, Blackboard blackboard, Dictionary<string, object> startState, List<IGoapPrecondition> goal, List<GOAPActionNode> availableActions)
        {
            if (goal == null) goal = new List<IGoapPrecondition>();
            if (startState == null) startState = new Dictionary<string, object>();

            var usableActions = new List<GOAPActionNode>();
            if (availableActions != null)
            {
                foreach (var action in availableActions)
                    if (action != null && !usableActions.Contains(action)) usableActions.Add(action);
            }

            var openSet = new List<PlanNode>();
            // Closed set keyed by state hash; colliding states are disambiguated by full comparison.
            var closedSet = new Dictionary<int, List<Dictionary<string, object>>>();

            // Start with a root node with no action, zero cost, and the initial state.
            var start = new PlanNode(null, 0, startState, null);
            start.F = Heuristic(agent, blackboard, start.State, goal);
            openSet.Add(start);

            int iterations = 0;

            // The main A* algorithm loop.
            while (openSet.Count > 0)
            {
                if (++iterations > MAX_ITERATIONS)
                {
                    Debug.LogWarning($"[Planner] Search aborted after {MAX_ITERATIONS} iterations. This could indicate a very complex/unsolvable plan. Goal was: {DescribeGoal(goal)}");
                    return null;
                }

                // Find the node in the openSet with the lowest (cached) f(n). Linear scan, no sorting/allocation.
                int bestIndex = 0;
                for (int i = 1; i < openSet.Count; i++)
                {
                    if (openSet[i].F < openSet[bestIndex].F) bestIndex = i;
                }
                var currentNode = openSet[bestIndex];
                openSet[bestIndex] = openSet[openSet.Count - 1];
                openSet.RemoveAt(openSet.Count - 1);

                // A state already expanded (reached at lower or equal f) is skipped.
                if (IsClosed(closedSet, currentNode)) continue;
                AddClosed(closedSet, currentNode);

                // Check if the goal has been met.
                if (ArePreconditionsMet(agent, blackboard, goal, currentNode.State))
                {
                    // Reconstruct the plan by tracing back from the current node to the root.
                    // (Empty when the start state already satisfies the goal.)
                    var plan = new List<GOAPActionNode>();
                    var n = currentNode;
                    while (n.Parent != null)
                    {
                        plan.Add(n.Action);
                        n = n.Parent;
                    }
                    plan.Reverse();
                    return plan;
                }

                // Consider viable actions from the current node.
                foreach (var action in usableActions)
                {
                    // Check if the action's preconditions are met by the current state.
                    if (ArePreconditionsMet(agent, blackboard, action.preconditions, currentNode.State))
                    {
                        var nextState = ApplyEffects(currentNode.State, action.effects);
                        var neighborNode = new PlanNode(currentNode, currentNode.Cost + action.cost, nextState, action);
                        if (IsClosed(closedSet, neighborNode)) continue;

                        neighborNode.F = neighborNode.Cost + Heuristic(agent, blackboard, nextState, goal);
                        openSet.Add(neighborNode);
                    }
                }
            }

            Debug.LogWarning($"[Planner] Search finished. No valid plan found because the open set is empty. Goal was: {DescribeGoal(goal)}");
            return null;
        }

        private static string DescribeGoal(List<IGoapPrecondition> goal)
        {
            var parts = new List<string>(goal.Count);
            foreach (var g in goal) parts.Add(g != null ? g.GetDescription() : "<null>");
            return $"[ {string.Join(" AND ", parts)} ]";
        }

        private static bool IsClosed(Dictionary<int, List<Dictionary<string, object>>> closedSet, PlanNode node)
        {
            if (!closedSet.TryGetValue(node.StateHash, out var bucket)) return false;
            for (int i = 0; i < bucket.Count; i++)
            {
                if (StatesEqual(bucket[i], node.State)) return true;
            }
            return false;
        }

        private static void AddClosed(Dictionary<int, List<Dictionary<string, object>>> closedSet, PlanNode node)
        {
            if (!closedSet.TryGetValue(node.StateHash, out var bucket))
            {
                bucket = new List<Dictionary<string, object>>(1);
                closedSet.Add(node.StateHash, bucket);
            }
            bucket.Add(node.State);
        }

        /// <summary>Order-independent hash of a world state (sum of per-entry hashes).</summary>
        private static int ComputeStateHash(Dictionary<string, object> state)
        {
            unchecked
            {
                int hash = 17;
                foreach (var kv in state)
                {
                    int keyHash = kv.Key != null ? kv.Key.GetHashCode() : 0;
                    int valueHash = kv.Value != null ? kv.Value.GetHashCode() : 0;
                    hash += (keyHash * 397) ^ valueHash;
                }
                return hash;
            }
        }

        private static bool StatesEqual(Dictionary<string, object> a, Dictionary<string, object> b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a.Count != b.Count) return false;
            foreach (var kv in a)
            {
                if (!b.TryGetValue(kv.Key, out var other)) return false;
                if (!Equals(kv.Value, other)) return false;
            }
            return true;
        }

        /// <summary>
        /// Checks if a set of conditions is met by a specific world state.
        /// </summary>
        private bool ArePreconditionsMet(GameObject agent, Blackboard blackboard, List<IGoapPrecondition> conditions, Dictionary<string, object> worldState)
        {
            if (conditions == null) return true;
            foreach (var condition in conditions)
            {
                if (condition == null) continue;
                if (!condition.IsMet(agent, blackboard, worldState)) return false;
            }
            return true;
        }

        /// <summary>
        /// Applies the effects of an action to create a new world state.
        /// </summary>
        private Dictionary<string, object> ApplyEffects(Dictionary<string, object> state, List<GOAPState> effects)
        {
            // Create a copy of the current state to avoid modifying the original.
            var newState = new Dictionary<string, object>(state);
            if (effects == null) return newState;
            foreach (var effect in effects)
            {
                if (effect == null || effect.key == null) continue;
                // Overwrite or add the new value to the state.
                newState[effect.key] = effect.GetValue();
            }
            return newState;
        }

        /// <summary>
        /// The heuristic function (h-cost) for the A* algorithm.
        /// It estimates the remaining "distance" from the current state to the goal:
        /// the number of goal conditions that are not yet met.
        /// </summary>
        private float Heuristic(GameObject agent, Blackboard blackboard,  Dictionary<string, object> state, List<IGoapPrecondition> goal)
        {
            int unmet = 0;
            for (int i = 0; i < goal.Count; i++)
            {
                var condition = goal[i];
                if (condition != null && !condition.IsMet(agent, blackboard, state)) unmet++;
            }
            return unmet;
        }
    }
}
