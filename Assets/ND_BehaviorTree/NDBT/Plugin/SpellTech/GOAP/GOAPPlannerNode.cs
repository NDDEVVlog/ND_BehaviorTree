using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ND_BehaviorTree.GOAP
{
    [NodeDescription("Plans a chain of its GOAP action children that reaches the goal, then runs it.", "Children must be GOAP Action nodes. The plan is the lowest-cost sequence whose effects satisfy the goal from the current world state.")]
    [NodeInfo("GOAPPlannerNode", "GOAP/GOAPPlannerNode", true, true, iconPath:"Assets/ND_BehaviorTree/NDBT/Icons/Brain.png")]
    public class GOAPPlannerNode : CompositeNode
    {
        [Tooltip("The ultimate goal this planner is trying to achieve. Can be composed of multiple conditions.")]
        [SerializeReference]
        public List<IGoapPrecondition> goal = new List<IGoapPrecondition>();

        private Queue<GOAPActionNode> _currentPlan;
        // True when the planner produced a plan this activation (possibly empty = goal already satisfied).
        [System.NonSerialized] private bool _hasPlan;

        private List<GOAPActionNode> ActionPool => children.OfType<GOAPActionNode>().ToList();

        protected override void OnEnter()
        {
            base.OnEnter();
            _currentPlan = new Queue<GOAPActionNode>();
            _hasPlan = false;

            var planner = new Planner();

            var worldState = new Dictionary<string, object>();
            if (blackboard != null)
            {
                foreach (var key in blackboard.keys)
                {
                    if (key != null && !string.IsNullOrEmpty(key.keyName))
                    {
                        worldState[key.keyName] = key.GetValueObject();

                    }
                }
            }

            // Pass the agent's GameObject to the planner for context-aware preconditions.
            GameObject agent = ownerTree != null && ownerTree.Self != null ? ownerTree.Self : null;
            var plan = planner.FindPlan(agent, blackboard, worldState, this.goal, ActionPool);

            // null = no plan (Failure). Empty = goal already satisfied (Success on the first tick).
            if (plan != null)
            {
                _currentPlan = new Queue<GOAPActionNode>(plan);
                _hasPlan = true;
            }
        }

        protected override Status OnProcess()
        {
            if (!_hasPlan || _currentPlan == null)
            {
                return Status.Failure; // No plan exists
            }
            if (_currentPlan.Count == 0)
            {
                return Status.Success; // Goal already satisfied: nothing to do
            }

            var currentAction = _currentPlan.Peek();
            var actionStatus = currentAction.Process();

            switch (actionStatus)
            {
                case Status.Success:
                    _currentPlan.Dequeue(); // Action succeeded, move to the next one
                    return _currentPlan.Count == 0 ? Status.Success : Status.Running;

                case Status.Running:
                    return Status.Running;

                case Status.Failure:
                    _currentPlan.Clear(); // Action failed, invalidate the entire plan
                    _hasPlan = false;
                    return Status.Failure;
            }
            return Status.Running;
        }

        public override void Reset()
        {
            base.Reset();
            if (_currentPlan != null)
            {
                foreach (var action in _currentPlan) action.Reset();
                _currentPlan.Clear();
            }
            _hasPlan = false;
            children.ForEach(c => c.Reset());
        }
    }
}
