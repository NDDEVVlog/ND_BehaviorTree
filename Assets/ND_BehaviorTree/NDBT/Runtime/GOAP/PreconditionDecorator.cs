////////////////////////////////////////////////////////////////////////////
//
// FB: https://www.facebook.com/profile.php?id=100090693452227
// Github: https://github.com/NDDEVVlog/ND_BehaviorTree
//
////////////////////////////////////////////////////////////////////////////

using UnityEngine;
using ND_BehaviorTree;

/// <summary>
/// Decorator nodes have a single child and modify its behavior. They can be 
/// used to invert the result of a child, repeat its execution, or act as a 
/// conditional check before running the child.
/// </summary>
[NodeInfo("PreconditionDecorator", "Decorator/PreconditionDecorator", true, true,iconPath:null)]
public class PreconditionDecorator : DecoratorNode
{
    // --- Lifecycle Methods ---

    // OnEnter is called once when the node is first processed.
    protected override void OnEnter() { }

    // OnProcess is where the decorator's logic is implemented.
    // It processes its child and then modifies or reacts to the result.
    protected override Status OnProcess()
    {
        // A decorator is useless without a child.
        if (child == null)
        {
            return Status.Failure;
        }

        // Process the child node and get its status.
        Status childStatus = child.Process();
        return childStatus;
    }

    // OnExit is called once when the node's status is no longer 'Running'.
    protected override void OnExit() { }
}