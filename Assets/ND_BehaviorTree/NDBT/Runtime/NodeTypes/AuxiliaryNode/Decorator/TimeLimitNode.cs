using UnityEngine;

namespace ND_BehaviorTree
{
    [NodeInfo("Time Limit", "Decorator/TimeLimit", true, true, iconPath: "Assets/ND_BehaviorTree/NDBT/Icons/Timer.png")]
    public class TimeLimitNode : DecoratorNode
    {
        [Tooltip("The maximum time in seconds the child node can be in the Running state.")]
        public float timeLimit = 1.0f;

        private float startTime;

        protected override void OnEnter()
        {
            startTime = Time.time;
        }

        protected override Status OnProcess()
        {
            if (child == null)
            {
                return Status.Failure;
            }
            
            // Check if the time limit has been exceeded.
            if (Time.time - startTime > timeLimit)
            {
                Debug.LogWarning($"TimeLimitNode cut off child '{child.name}' after {timeLimit}s.");
                // Interrupt the child to stop it from continuing its action.
                child.InteruptAction?.Invoke();
                child.Reset();
                return Status.Failure;
            }

            // Process the child and return its status.
            return child.Process();
        }
    }
}