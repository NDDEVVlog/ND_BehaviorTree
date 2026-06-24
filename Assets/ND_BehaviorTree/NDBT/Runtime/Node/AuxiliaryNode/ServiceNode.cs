using UnityEngine;

namespace ND_BehaviorTree
{   
    public abstract class ServiceNode : Node
    {
        public float interval = 1.0f;
        public bool runOnEnter = true;

        [System.NonSerialized] private float m_LastExecutionTime;

        protected override void OnEnter()
        {
            m_LastExecutionTime = runOnEnter ? Time.time - interval - 0.1f : Time.time;
        }

        protected override Status OnProcess()
        {
            if (Time.time - m_LastExecutionTime >= interval)
            {
                m_LastExecutionTime = Time.time;
                OnTick();
            }
            return Status.Running; 
        }

        protected abstract void OnTick();
    }
}