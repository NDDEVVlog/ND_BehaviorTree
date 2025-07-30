using System.Collections.Generic;
using System.Linq;

namespace ND_BehaviorTree.GOAP
{
    public class Planner
    {
        private class PlanNode
        {
            public PlanNode Parent;
            public float Cost;
            public Dictionary<string, object> State;
            public GOAPActionNode Action;

            public PlanNode(PlanNode parent, float cost, Dictionary<string, object> state, GOAPActionNode action)
            {
                Parent = parent; Cost = cost; State = state; Action = action;
            }
        }

        public List<GOAPActionNode> FindPlan(Dictionary<string, object> startState, List<GOAPState> goalState, List<GOAPActionNode> availableActions)
        {
            var usableActions = new HashSet<GOAPActionNode>(availableActions);
            var openSet = new List<PlanNode>();
            var closedSet = new HashSet<Dictionary<string, object>>();

            openSet.Add(new PlanNode(null, 0, startState, null));

            while (openSet.Count > 0)
            {
                var currentNode = openSet.OrderBy(n => n.Cost + Heuristic(n.State, goalState)).First();
                openSet.Remove(currentNode);
                closedSet.Add(currentNode.State);

                if (IsStateSatisfied(goalState, currentNode.State))
                {
                    var plan = new List<GOAPActionNode>();
                    var n = currentNode;
                    while (n.Parent != null) { plan.Insert(0, n.Action); n = n.Parent; }
                    return plan;
                }
                
                foreach (var action in usableActions)
                {
                    if (IsStateSatisfied(action.preconditions, currentNode.State))
                    {
                        var nextState = ApplyEffects(currentNode.State, action.effects);
                        if (closedSet.Any(s => AreStatesEqual(s, nextState))) continue;
                        
                        openSet.Add(new PlanNode(currentNode, currentNode.Cost + action.cost, nextState, action));
                    }
                }
            }
            return null;
        }

        private bool IsStateSatisfied(List<GOAPState> conditions, Dictionary<string, object> worldState)
        {
            foreach (var condition in conditions)
            {
                if (!worldState.TryGetValue(condition.key, out object worldValue)) return false;
                if (!GOAPValueHelper.CompareValues(worldValue, condition)) return false;
            }
            return true;
        }

        private Dictionary<string, object> ApplyEffects(Dictionary<string, object> state, List<GOAPState> effects)
        {
            var newState = new Dictionary<string, object>(state);
            foreach (var effect in effects) { newState[effect.key] = effect.GetValue(); }
            return newState;
        }

        private float Heuristic(Dictionary<string, object> state, List<GOAPState> goal)
        {
            float h = 0;
            foreach (var condition in goal)
            {
                if (!IsStateSatisfied(new List<GOAPState> { condition }, state)) h++;
            }
            return h;
        }

        private bool AreStatesEqual(Dictionary<string, object> s1, Dictionary<string, object> s2)
        {
            if (s1.Count != s2.Count) return false;
            return s1.All(kvp => s2.ContainsKey(kvp.Key) && s2[kvp.Key].Equals(kvp.Value));
        }
    }
}