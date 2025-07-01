// File: CreateNodeContextualCommand.cs
using UnityEditor.Experimental.GraphView;
using UnityEngine; // For GUIUtility
using UnityEngine.UIElements;

namespace ND_BehaviorTree.Editor
{
    public class CreateNodeContextualCommand : IContextualMenuCommand
    {
        public bool CanExecute(ContextualMenuPopulateEvent evt, ND_BehaviorTreeView graphView)
        {
            // Can always create a node if right-clicking on the graph view background
            return evt.target is GraphView || evt.target is GridBackground;
        }

        public void AddToMenu(ContextualMenuPopulateEvent evt, ND_BehaviorTreeView graphView)
        {
            evt.menu.AppendAction("Create Node", (action) =>
            {
                // We need to provide a NodeCreationContext.
                // evt.mousePosition is local to the element that received the event.
                // We need screen position for SearchWindow.Open.
                // GraphView's nodeCreationRequest usually handles this conversion better.
                // For simplicity here, we can try to approximate or directly call the view's search.
                var screenMousePosition = GUIUtility.GUIToScreenPoint(evt.localMousePosition);
                 if (evt.target is VisualElement ve) {
                    screenMousePosition = ve.LocalToWorld(evt.localMousePosition);
                    screenMousePosition = GUIUtility.GUIToScreenPoint(screenMousePosition);
                 }


                graphView.OpenSearchWindow(screenMousePosition);

            }, DropdownMenuAction.AlwaysEnabled);
        }
    }
}