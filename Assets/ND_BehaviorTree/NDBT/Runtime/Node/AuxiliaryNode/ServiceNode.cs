using UnityEngine;

namespace ND_BehaviorTree
{   
    public abstract class ServiceNode : Node
    {
        public float interval = 1.0f;
        public bool runOnEnter = true;

        [System.NonSerialized] private float m_LastExecutionTime;

        /// <summary>0..1 progress toward the next OnTick (1 when the interval is 0 or less). Read by the editor live view.</summary>
        public float TickProgress => interval <= 0f ? 1f : Mathf.Clamp01((Time.time - m_LastExecutionTime) / interval);

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