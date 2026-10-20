

using UnityEngine;

namespace ND_BehaviorTree
{   
    [NodeDescription("Flips its node's result: Success <-> Failure (Running stays Running).", "Useful to negate a check, e.g. 'not in range'.")]
    [NodeInfo("Inverter", "Decorator/Inverter", true, true,iconPath:"Assets/ND_BehaviorTree/NDBT/Icons/Inverter.png")]
    public class InverterNode : DecoratorNode
    {
        [System.NonSerialized] private bool m_NoChildWarned;

        /// <summary>
        /// Revert the status of the child node
        /// </summary>
        /// <returns></returns>
        protected override Status OnProcess()
        {
            if (child == null)
            {
                if (!m_NoChildWarned)
                {
                    m_NoChildWarned = true;
                    Debug.LogWarning($"[InverterNode] '{name}' has no child; it always succeeds. (Logged once.)", this);
                }
                return Status.Success;
            }
            
            var status = child.Process();
            switch (status)
            {
                case Status.Success:
                    return Status.Failure;
                case Status.Failure:
                    return Status.Success;
                case Status.Running:
                    return Status.Running;
            }
            return Status.Failure;
        }
    }
}
