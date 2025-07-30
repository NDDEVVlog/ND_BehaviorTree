using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ND_BehaviorTree.GOAP
{   
    
    [NodeInfo("GOAPPlannerNode", "GOAP/GOAPPlannerNode", true, true,iconPath:"Assets/ND_BehaviorTree/NDBT/Icons/Brain.png")]
    public class GOAPPlannerNode : CompositeNode
    {
        [Tooltip("The ultimate goal this planner is trying to achieve.")]
        public List<GOAPState> goal = new List<GOAPState>();

        private Queue<GOAPActionNode> _currentPlan;

        private List<GOAPActionNode> ActionPool => children.OfType<GOAPActionNode>().ToList();

        protected override void OnEnter()
        {
            base.OnEnter();
            _currentPlan = new Queue<GOAPActionNode>();

            var planner = new Planner();

            var worldState = new Dictionary<string, object>();
            if (blackboard != null)
            {
                foreach (var key in blackboard.keys)
                {
                    worldState.Add(key.keyName, key.GetValueObject());
                }
            }

            var plan = planner.FindPlan(worldState, this.goal, ActionPool);

            if (plan != null)
            {
                _currentPlan = new Queue<GOAPActionNode>(plan);
                Debug.Log($"GOAP Plan Found: {string.Join(" -> ", plan.Select(a => a.typeName))}");
            }
            else
            {
                Debug.LogWarning("GOAP Planner could not find a valid plan.");
            }
        }

        protected override Status OnProcess()
        {
            if (_currentPlan == null || _currentPlan.Count == 0)
            {
                return Status.Failure;
            }

            var currentAction = _currentPlan.Peek();
            var actionStatus = currentAction.Process();

            switch (actionStatus)
            {
                case Status.Success:
                    _currentPlan.Dequeue();
                    return _currentPlan.Count == 0 ? Status.Success : Status.Running;

                case Status.Running:
                    return Status.Running;

                case Status.Failure:
                    _currentPlan.Clear();
                    Debug.LogWarning($"Action '{currentAction.name}' failed. Invalidating plan.");
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
            children.ForEach(c => c.Reset());
        }
    }
}