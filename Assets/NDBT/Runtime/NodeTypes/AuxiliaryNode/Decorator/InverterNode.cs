// --- START OF FILE InverterNode.cs ---

using UnityEngine;

namespace ND_BehaviorTree
{
    [NodeInfo("Inverter", "Decorator/Inverter", true, true,iconPath:"Assets/NDBT/Icons/Inverter.png")]
    public class InverterNode : DecoratorNode
    {
        
        protected override Status OnProcess()
        {
            if (child == null)
            {
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
// --- END OF FILE InverterNode.cs ---