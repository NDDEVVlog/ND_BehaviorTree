// --- START OF FILE SequenceNode.cs ---

namespace ND_BehaviorTree
{   
    [NodeInfo("Sequence", "Composite/Sequence", true, true,iconPath:"Assets/NDBT/Icons/antivirus.png" ,isChildOnly: false)]
    public class SequenceNode : CompositeNode
    {
        protected int currentChildIndex;

        protected override void OnEnter()
        {
            currentChildIndex = 0;
        }

        protected override Status OnProcess()
        {
            if (children.Count == 0) return Status.Success;

            // Start from the current child and process until one is running or all have succeeded.
            for (int i = currentChildIndex; i < children.Count; ++i)
            {
                currentChildIndex = i;
                var status = children[currentChildIndex].Process();

                if (status == Status.Failure)
                {
                    return Status.Failure;
                }
                if (status == Status.Running)
                {
                    return Status.Running;
                }
            }

            return Status.Success;
        }

        public override void Reset()
        {
            base.Reset();
            currentChildIndex = 0;
        }
    }
}
// --- END OF FILE SequenceNode.cs ---