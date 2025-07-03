// --- START OF FILE SelectorNode.cs ---

namespace ND_BehaviorTree
{
    [NodeInfo("Selector", "Composite/Selector", true, true, iconPath: "Assets/NDBT/Icons/antivirus.png", isChildOnly: false)]
    public class SelectorNode : CompositeNode
    {
        protected int currentChildIndex;

        protected override void OnEnter()
        {
            currentChildIndex = 0;
        }

        protected override Status OnProcess()
        {
            // First, check if decorators allow execution.
            if (!AreDecoratorsSatisfied())
            {
                return Status.Failure;
            }
            // Then, tick any attached services.
            TickServices();

            if (children.Count == 0) return Status.Failure;

            // Start from the current child and process until one is running or one succeeds.
            for (int i = currentChildIndex; i < children.Count; ++i)
            {
                currentChildIndex = i;
                var status = children[currentChildIndex].Process();

                if (status == Status.Success)
                {
                    return Status.Success;
                }
                if (status == Status.Running)
                {
                    return Status.Running;
                }
            }

            return Status.Failure;
        }

        public override void Reset()
        {
            base.Reset();
            currentChildIndex = 0;
        }
    }
}
// --- END OF FILE SelectorNode.cs ---