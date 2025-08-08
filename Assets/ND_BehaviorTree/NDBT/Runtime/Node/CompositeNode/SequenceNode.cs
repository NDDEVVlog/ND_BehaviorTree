
using UnityEngine;
namespace ND_BehaviorTree
{
    [NodeInfo("Sequence", "Composite/Sequence", true, true, iconPath: "Assets/ND_BehaviorTree//NDBT/Icons/ProcessSequence.png", isChildOnly: false)]
    public class SequenceNode : CompositeNode
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

            if (children.Count == 0) return Status.Success;

            // Start from the current child and process until one is running or all have succeeded.
            if (currentChildIndex < children.Count)
            {
                //Debug.LogWarning(children[currentChild].name + " return : " + children[currentChild].Process());
                switch (children[currentChildIndex].Process())
                {
                    case Status.Running:
                        //Debug.LogWarning(children[currentChild].name + " return : " + children[currentChild].Process());
                        return Status.Running;
                    case Status.Failure:
                        //Debug.LogWarning(" Name :" + name + " return Fail");
                        currentChildIndex = 0;
                        return Status.Failure;
                    default:
                        currentChildIndex++;

                        return currentChildIndex == children.Count ? Status.Success : Status.Running;
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