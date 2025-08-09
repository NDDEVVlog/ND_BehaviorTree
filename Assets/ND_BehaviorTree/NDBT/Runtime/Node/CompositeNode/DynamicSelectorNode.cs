
using UnityEngine;
using ND_BehaviorTree;

/// <summary>
/// A composite node that uses a custom logic object (IDynamicBranchSelector)
/// to decide which single child to execute. This is useful for creating data-driven
/// or state-machine-like behaviors within the tree.
/// </summary>
[NodeInfo("Dynamic Selector", "Composite/DynamicSelector", true, true, iconPath: "Assets/ND_BehaviorTree/NDBT/Icons/adaptation.png")]
public class DynamicSelectorNode : CompositeNode
{
    [Tooltip("The custom logic used to select a child branch. This must be a class that implements IDynamicBranchSelector.")]
    [SerializeReference]
    public IDynamicBranchSelector condition;

    // Runtime state to remember which child is currently running.
    private int _runningChildIndex = -1;

    // --- Lifecycle Methods ---

    protected override void OnEnter()
    {
        // Reset state and initialize the condition logic.
        _runningChildIndex = -1;
        condition?.OnInitialize(GetOwnerTreeGameObject());
    }

    protected override Status OnProcess()
    {
        TickServices();

        // 1. If a child is already running, continue to process it.
        //    This is more efficient and prevents the condition from being re-checked every frame.
        if (_runningChildIndex != -1)
        {
            Node runningChild = children[_runningChildIndex];
            Status childStatus = runningChild.Process();

            // If the child is still running, so are we.
            if (childStatus == Status.Running)
            {
                return Status.Running;
            }

            // If the child finished (succeeded or failed), reset our state and return its status.
            runningChild.Reset();
            _runningChildIndex = -1;
            return childStatus;
        }

        // 2. If no child is running, ask the custom condition to select one.
        if (condition == null)
        {
            Debug.LogError("DynamicSelectorNode has no condition assigned.", this);
            return Status.Failure;
        }

        if (condition.TrySelectBranch(GetOwnerTreeGameObject(), blackboard, out int selectedIndex))
        {
            // Safety check: ensure the returned index is valid.
            if (selectedIndex < 0 || selectedIndex >= children.Count)
            {
                Debug.LogError($"IDynamicBranchSelector returned an invalid index: {selectedIndex}. Node has {children.Count} children.", this);
                return Status.Failure;
            }

            // 3. Process the newly selected child.
            Node newChild = children[selectedIndex];
            Status newStatus = newChild.Process();

            // If the new child starts running, we must remember its index for the next frame.
            if (newStatus == Status.Running)
            {
                _runningChildIndex = selectedIndex;
            }
            
            return newStatus;
        }

        // 4. If the condition failed to select a branch, this node fails.
        return Status.Failure;
    }
    
    public override void Reset()
    {
        base.Reset();
        _runningChildIndex = -1;
    }
}