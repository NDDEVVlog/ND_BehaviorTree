using UnityEngine;

namespace ND_BehaviorTree
{
    /// <summary>
    /// A composite node that randomly selects and runs one of its children.
    /// Each child has an equal probability of being chosen.
    /// 
    /// Behavior:
    /// - On activation, it randomly picks one child to execute.
    /// - If the selected child returns Success or Failure, the RandomSelector returns that same status. It does NOT try another child.
    /// - If the selected child returns Running, the selector will continue to process that same child in subsequent ticks until it completes.
    /// - If it has no children, it will fail.
    /// </summary>
    [NodeInfo("Random Selector", "Composite/RandomSelector", true, true, iconPath: "Assets/ND_BehaviorTree/NDBT/Icons/RandomBox.png", isChildOnly: false)]
    public class RandomSelectorNode : CompositeNode
    {
        private Node runningChild = null;

        protected override void OnEnter()
        {
            // Clear any previously running child when the node is entered.
            runningChild = null;
        }

        protected override Status OnProcess()
        {
            // Tick any attached services.
            TickServices();

            Node childToProcess;

            // If a child was already running, we must continue processing it.
            if (runningChild != null)
            {
                childToProcess = runningChild;
            }
            else
            {
                // If there are no children, we cannot select one, so we fail.
                if (children.Count == 0)
                {
                    return Status.Failure;
                }

                // Select a new child at random.
                int index = Random.Range(0, children.Count);
                childToProcess = children[index];
            }

            // Process the chosen child.
            var status = childToProcess.Process();

            switch (status)
            {
                case Status.Running:
                    // The child is still running, so store it and report Running status.
                    runningChild = childToProcess;
                    return Status.Running;

                case Status.Success:
                    // The child succeeded. We are done, so reset and report Success.
                    runningChild = null;
                    return Status.Success;
                
                default: // Status.Failure
                    // The child failed. We are done, so reset and report Failure.
                    runningChild = null;
                    return Status.Failure;
            }
        }

        public override void Reset()
        {
            base.Reset();
            runningChild = null;
        }
    }
}