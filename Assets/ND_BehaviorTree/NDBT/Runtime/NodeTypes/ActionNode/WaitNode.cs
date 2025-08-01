
using UnityEngine;

namespace ND_BehaviorTree
{
    [NodeInfo("Wait", "Action/Wait", true, false,iconPath:"Assets/ND_BehaviorTree/NDBT/Icons/Wait.png")]
    public class WaitNode : ActionNode
    {   
        
        public float duration = 1.0f;
        [HideInInspector]
        protected float _startTime;

        protected override void OnEnter()
        {
            _startTime = Time.time;
            
        }

        protected override Status OnProcess()
        {
            if (Time.time - _startTime > duration)
            {   
                return Status.Success;
            }
            return Status.Running;
        }
    }
}
