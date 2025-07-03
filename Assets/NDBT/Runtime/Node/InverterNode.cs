// --- START OF FILE InverterNode.cs ---

namespace ND_BehaviorTree
{
    [NodeInfo("Inverter", "Decorator/Inverter", true, false)]
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