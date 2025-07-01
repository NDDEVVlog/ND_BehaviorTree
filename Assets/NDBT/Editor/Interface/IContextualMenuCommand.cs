// File: IContextualMenuCommand.cs
using UnityEditor.Experimental.GraphView; // For ContextualMenuPopulateEvent
using UnityEngine.UIElements; // For DropdownMenuAction

namespace ND_BehaviorTree.Editor
{
    public interface IContextualMenuCommand
    {
        /// <summary>
        /// Checks if this command should be added to the menu based on the current context.
        /// </summary>
        bool CanExecute(ContextualMenuPopulateEvent evt, ND_BehaviorTreeView graphView);

        /// <summary>
        /// Adds the command to the menu.
        /// </summary>
        void AddToMenu(ContextualMenuPopulateEvent evt, ND_BehaviorTreeView graphView);
    }
}