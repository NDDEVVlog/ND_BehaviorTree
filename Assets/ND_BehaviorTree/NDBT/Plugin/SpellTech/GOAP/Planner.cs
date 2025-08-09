
using System.Collections.Generic;
using System.Linq;
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
            public Dictionary<string, object> State;
            public GOAPActionNode Action;

            public PlanNode(PlanNode parent, float cost, Dictionary<string, object> state, GOAPActionNode action)
            {
                Parent = parent;
                Cost = cost;
                State = state;
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
        /// <returns>A list of GOAPActionNodes (the plan), or null if no plan was found.</returns>
        public List<GOAPActionNode> FindPlan(GameObject agent, Blackboard blackboard, Dictionary<string, object> startState, List<IGoapPrecondition> goal, List<GOAPActionNode> availableActions)
        {
            // Log the goal being pursued.
            var goalDescriptions = goal.Select(g => g.GetDescription());
            string goalString = $"[ {string.Join(" AND ", goalDescriptions)} ]";
            //Debug.Log($"[Planner] Starting search for goal: {goalString}");

            var usableActions = new HashSet<GOAPActionNode>(availableActions);
            var openSet = new List<PlanNode>();
            var closedSet = new HashSet<PlanNode>();

            // Start with a root node with no action, zero cost, and the initial state.
            openSet.Add(new PlanNode(null, 0, startState, null));
            
            int iterations = 0;

            // The main A* algorithm loop.
            while (openSet.Count > 0)
            {
                // ** NEW: INFINITE LOOP PREVENTION **
                if (++iterations > MAX_ITERATIONS)
                {
                    Debug.LogWarning($"[Planner] Search aborted after {MAX_ITERATIONS} iterations. This could indicate an infinite loop or a very complex/unsolvable plan. Goal was: {goalString}");
                    return null;
                }

                // Find the node in the openSet with the lowest f(n) = g(n) + h(n) cost.
                // g(n) is currentNode.Cost
                // h(n) is the Heuristic()
                var currentNode = openSet.OrderBy(n => n.Cost + Heuristic(agent, blackboard, n.State, goal)).First();
                
                openSet.Remove(currentNode);
                closedSet.Add(currentNode);

                // Check if the goal has been met.
                if (ArePreconditionsMet(agent, blackboard, goal, currentNode.State, isGoalCheck: true))
                {
                    // If the goal is met, reconstruct the plan by tracing back from the current node to the root.
                    var plan = new List<GOAPActionNode>();
                    var n = currentNode;
                    while (n.Parent != null)
                    {
                        plan.Insert(0, n.Action);
                        n = n.Parent;
                    }
                    Debug.Log($"[Planner] Plan found! Goal: {goalString}");
                    return plan;
                }
                
                // Consider viable actions from the current node.
                foreach (var action in usableActions)
                {
                    // Check if the action's preconditions are met by the current state.
                    if (ArePreconditionsMet(agent, blackboard, action.preconditions, currentNode.State))
                    {
                        var nextState = ApplyEffects(currentNode.State, action.effects);
                        
                        // Create a new plan node and add it to the openSet for future consideration.
                        var neighborNode = new PlanNode(currentNode, currentNode.Cost + action.cost, nextState, action);
                        openSet.Add(neighborNode);
                    }
                }
            }

            Debug.LogWarning($"[Planner] Search finished. No valid plan found because the open set is empty. Goal was: {goalString}");
            return null;
        }
        
        /// <summary>
        /// Checks if a set of conditions is met by a specific world state.
        /// </summary>
        private bool ArePreconditionsMet(GameObject agent, Blackboard blackboard, List<IGoapPrecondition> conditions, Dictionary<string, object> worldState, bool isGoalCheck = false)
        {
            string logPrefix = isGoalCheck ? "[Goal Check]" : "[Action Precondition Check]";

            foreach (var condition in conditions)
            {
                bool met = condition.IsMet(agent, blackboard, worldState);
                if (!met)
                {
                    // This log is very useful for debugging which specific condition failed.
                    // Debug.Log($"<color=orange>{logPrefix} NOT MET:</color> {condition.GetDescription()}");
                    return false;
                }
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
            foreach (var effect in effects)
            {
                // Overwrite or add the new value to the state.
                newState[effect.key] = effect.GetValue();
            }
            return newState;
        }
        
        /// <summary>
        /// The heuristic function (h-cost) for the A* algorithm.
        /// It estimates the remaining "distance" from the current state to the goal.
        /// A simple heuristic is to count the number of goal conditions that are not yet met.
        /// </summary>
        private float Heuristic(GameObject agent, Blackboard blackboard,  Dictionary<string, object> state, List<IGoapPrecondition> goal)
        {
            return goal.Count(condition => !condition.IsMet(agent, blackboard, state));
        }
    }
}