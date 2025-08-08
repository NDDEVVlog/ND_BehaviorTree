using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ND_BehaviorTree.GOAP
{   
    [NodeInfo("GOAPPlannerNode", "GOAP/GOAPPlannerNode", true, true, iconPath:"Assets/ND_BehaviorTree/NDBT/Icons/Brain.png")]
    public class GOAPPlannerNode : CompositeNode
    {
        [Tooltip("The ultimate goal this planner is trying to achieve. Can be composed of multiple conditions.")]
        [SerializeReference]
        public List<IGoapPrecondition> goal = new List<IGoapPrecondition>();

        private Queue<GOAPActionNode> _currentPlan;

        private List<GOAPActionNode> ActionPool => children.OfType<GOAPActionNode>().ToList();

        protected override void OnEnter()
        {
            base.OnEnter();
            _currentPlan = new Queue<GOAPActionNode>();

            var planner = new Planner();

            // === DEBUG: KIỂM TRA WORLD STATE BAN ĐẦU ===
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
            // var plan = planner.FindPlan(GetOwnerTreeGameObject(), worldState, this.goal, ActionPool); // Giả sử GetOwnerTreeGameObject() tồn tại
            var plan = planner.FindPlan(GetOwnerTreeGameObject(),blackboard, worldState, this.goal, ActionPool);


            if (plan != null && plan.Count > 0)
            {
                _currentPlan = new Queue<GOAPActionNode>(plan);

            }


        }

        protected override Status OnProcess()
        {
            if (_currentPlan == null || _currentPlan.Count == 0)
            {
                return Status.Failure; // No plan exists or plan is finished
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