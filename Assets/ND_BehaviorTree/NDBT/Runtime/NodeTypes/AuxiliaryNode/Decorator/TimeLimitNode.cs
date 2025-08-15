
using UnityEngine;

namespace ND_BehaviorTree
{
    [NodeInfo("Time Limit", "Decorator/TimeLimit", true, true, iconPath: "Assets/ND_BehaviorTree/NDBT/Icons/Timer.png")]
    public class TimeLimitNode : DecoratorNode
    {
        [Tooltip("The maximum time in seconds the child node can be in the Running state.")]
        public float timeLimit = 1.0f;
        
        // This attribute now points directly to our fields.
        // It reads "elapsedTime" for the current value and "timeLimit" for the max value.
        [NodeProgressBar(nameof(elapsedTime), nameof(timeLimit))]
        [ExposeProperty] // We can even expose the numeric value at the same time!
        private float elapsedTime;

        private float startTime;

        protected override void OnEnter()
        {
            startTime = Time.time;
            elapsedTime = 0f; // Reset our tracking field
        }

        protected override Status OnProcess()
        {
            if (child == null)
            {
                return Status.Failure;
            }
            
            // Update the tracking field every frame. The UI will read this value.
            elapsedTime = Time.time - startTime;

            // Check if the time limit has been exceeded.
            if (elapsedTime > timeLimit)
            {
                child.InteruptAction?.Invoke();
                child.Reset();
                return Status.Failure;
            }

            // Process the child and return its status.
            return child.Process();
        }
    }
}
