// --- START OF FILE ServiceNode.cs ---

using UnityEngine;

namespace ND_BehaviorTree
{   
    
    /// <summary>
    /// A service node runs on a timer as long as its parent branch is active.
    /// It's used for checks and updates, not for direct control flow.
    /// In this implementation, it's a non-blocking node that ticks its logic and returns Success.
    /// </summary>
    /// 
    /// 
    public abstract class ServiceNode : AuxiliaryNode
    {
        public float interval = 1.0f;
        public bool runOnEnter = true;

        [System.NonSerialized]
        private float lastExecutionTime;

        protected override void OnEnter()
        {
            lastExecutionTime = -interval; // Ensure it can run immediately if runOnEnter is true
            if (runOnEnter)
            {
                OnTick();
            }
        }

        protected override Status OnProcess()
        {
            if (Time.time - lastExecutionTime > interval)
            {
                lastExecutionTime = Time.time;
                OnTick();
            }
            // Services always succeed immediately so they don't block the execution flow.
            return Status.Success;
        }

        protected override void OnExit() { }

        /// <summary>
        /// The logic the service performs on its tick.
        /// </summary>
        protected abstract void OnTick();
    }
}
// --- END OF FILE ServiceNode.cs ---