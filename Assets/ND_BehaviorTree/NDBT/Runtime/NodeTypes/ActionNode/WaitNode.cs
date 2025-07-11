
using UnityEngine;

namespace ND_BehaviorTree
{
    [NodeInfo("Wait", "Action/Wait", true, false,iconPath:"Assets/ND_BehaviorTree/NDBT/Icons/Wait.png")]
    public class WaitNode : ActionNode
    {
        public float duration = 1.0f;
        private float _startTime;

        protected override void OnEnter()
        {
            _startTime = Time.time;
            Debug.Log("Enter WaitNode at :" + _startTime);
        }

        protected override Status OnProcess()
        {
            if (Time.time - _startTime > duration)
            {   
                Debug.Log("Complete WaitNode at :" + _startTime);
                return Status.Success;
            }
            return Status.Running;
        }
    }
}
