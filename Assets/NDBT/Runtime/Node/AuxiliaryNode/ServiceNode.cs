// --- MODIFIED FILE: ServiceNode.cs ---

using UnityEngine;

namespace ND_BehaviorTree
{   
    
    /// <summary>
    /// A service node runs on a timer as long as its parent branch is active.
    /// It's used for checks and updates (e.g., updating a blackboard value), not for direct control flow.
    /// It is attached to a CompositeNode and ticked by it, running in parallel to the composite's children.
    /// </summary>
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
                // We must use Time.time here as OnEnter is only called once.
                lastExecutionTime = Time.time;
                OnTick();
            }
        }

        protected override Status OnProcess()
        {
            // This OnProcess is called by the parent CompositeNode's TickServices method.
            if (Time.time - lastExecutionTime > interval)
            {
                lastExecutionTime = Time.time;
                OnTick();
            }
            // Services always succeed immediately so they don't block anything.
            // Their "running" state is managed internally by the timer.
            return Status.Success;
        }

        protected override void OnExit() { }

        /// <summary>
        /// The logic the service performs on its tick.
        /// </summary>
        protected abstract void OnTick();
    }
}