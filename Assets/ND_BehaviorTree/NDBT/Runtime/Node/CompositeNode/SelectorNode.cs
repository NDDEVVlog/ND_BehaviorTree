

namespace ND_BehaviorTree
{
    [NodeInfo("Selector", "Composite/Selector", true, true, iconPath: "Assets/ND_BehaviorTree/NDBT/Icons/Selector.png", isChildOnly: false)]
    public class SelectorNode : CompositeNode
    {
        protected int currentChildIndex;

        protected override void OnEnter()
        {
            currentChildIndex = 0;
        }

        protected override Status OnProcess()
        {
            // Tick any attached services.
            TickServices();

            if (children.Count == 0) return Status.Failure;

            // Start from the current child and process until one is running or one succeeds.
            if (currentChildIndex < children.Count)
            {
                switch (children[currentChildIndex].Process())
                {
                    case Status.Running:
                        return Status.Running;
                    case Status.Success:
                        Reset();
                        return Status.Success;
                    default:
                        currentChildIndex++;
                        return Status.Running;
                }
            }
            Reset();
            return Status.Failure;
        }

        public override void Reset()
        {
            base.Reset();
            currentChildIndex = 0;
        }
    }
}