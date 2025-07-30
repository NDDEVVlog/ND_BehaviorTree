// --- REFACTORED INTERFACE ---

using UnityEngine;

namespace ND_BehaviorTree
{
    /// <summary>
    /// An interface for custom logic that dynamically selects which child branch of a
    /// DynamicSelectorNode should be executed.
    /// </summary>
    
    public interface IDynamicBranchSelector
    {
        /// <summary>
        /// Called once when the DynamicSelectorNode is entered. Use this to cache
        /// components or perform any one-time setup.
        /// </summary>
        /// <param name="ownerGameObject">The GameObject running the behavior tree.</param>
        void OnInitialize(GameObject ownerGameObject);

        /// <summary>
        /// Attempts to select a child node to execute based on custom logic.
        /// This is the core method called by the DynamicSelectorNode.
        /// </summary>
        /// <param name="ownerGameObject">The GameObject running the behavior tree.</param>
        /// <param name="blackboard">The blackboard associated with the tree.</param>
        /// <param name="selectedIndex">The index of the child to execute. This is only valid if the method returns true.</param>
        /// <returns>True if a child was successfully selected, false otherwise. If false, the DynamicSelectorNode will fail.</returns>
        bool TrySelectBranch(GameObject ownerGameObject, Blackboard blackboard, out int selectedIndex);
    }
}