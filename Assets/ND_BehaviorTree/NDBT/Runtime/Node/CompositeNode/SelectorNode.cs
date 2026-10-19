

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

            if (children.Count == 0) return Status.Failure;

            // Start from the current child and process until one is running or one succeeds.
            if (currentChildIndex < children.Count)
            {
                switch (children[currentChildIndex].Process())
                {
                    case Status.Running:
                        return Status.Running;
                    case Status.Success:
                        ResetChildrenForNextRun();
                        return Status.Success;
                    default:
                        currentChildIndex++;
                        return Status.Running;
                }
            }
            ResetChildrenForNextRun();
            return Status.Failure;
        }

        /// <summary>
        /// Prepares the children for the next activation without touching this node's own lifecycle.
        /// (Calling Reset() here would run OnExit, and Node.Process() then runs OnExit a second time.)
        /// </summary>
        private void ResetChildrenForNextRun()
        {
            currentChildIndex = 0;
            for (int i = 0; i < children.Count; i++) children[i]?.Reset();
        }

        public override void Reset()
        {
            base.Reset();
            currentChildIndex = 0;
        }
    }
}
