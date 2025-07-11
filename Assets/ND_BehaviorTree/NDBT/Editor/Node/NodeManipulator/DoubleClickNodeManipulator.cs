

using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND_BehaviorTree.Editor
{
    public class DoubleClickNodeManipulator : Manipulator
    {
        private ND_NodeEditor _nodeEditorVisual; // The main node this manipulator is attached to
        private double _lastClickTime = 0;
        private const double DoubleClickSpeed = 0.3; // Seconds for double click detection

        // The constructor now takes the main node visual element
        public DoubleClickNodeManipulator(ND_NodeEditor nodeEditorVisual)
        {
            _nodeEditorVisual = nodeEditorVisual;
        }

        protected override void RegisterCallbacksOnTarget()
        {
            // The 'target' is the _nodeEditorVisual itself
            target.RegisterCallback<PointerDownEvent>(OnPointerDown);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<PointerDownEvent>(OnPointerDown);
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0)
            {
                return;
            }

            double currentTime = EditorApplication.timeSinceStartup;
            if (currentTime - _lastClickTime < DoubleClickSpeed)
            {
                // Double click detected
                _lastClickTime = 0;
                evt.StopPropagation();
                evt.PreventDefault();

                // Pass the event itself to the handler so we know the click target
                HandleDoubleClick(evt);
            }
            else
            {
                // First click
                _lastClickTime = currentTime;
            }
        }

        private void HandleDoubleClick(PointerDownEvent evt)
        {
            // Start with the most specific element that was clicked
            var clickedElement = evt.target as VisualElement;
            Node targetNode = null;

            // Traverse up the visual tree from the clicked element
            // Stop when we hit the main node's boundary (the 'target' of this manipulator)
            while (clickedElement != null && clickedElement != target)
            {
                // Check if the current element in our traversal has a Node data object attached.
                // This is the "tag" we set in Step 1.
                if (clickedElement.userData is Node childNodeData)
                {
                    // We found a child node visual! This is our target.
                    targetNode = childNodeData;
                    break; // Exit the loop
                }
                // Move up to the parent element
                clickedElement = clickedElement.parent;
            }

            // If the loop finished without finding a child node,
            // it means the user clicked the main node's background.
            // In that case, the target is the main node itself.
            if (targetNode == null)
            {
                targetNode = _nodeEditorVisual.m_Node; // Use the main node's data
            }

            // Now, open the property window for the determined target node.
            if (targetNode != null)
            {
                Debug.Log($"Node '{targetNode.name}' double-clicked. Opening Node Property Editor.");
                NodePropertyEditorWindow.Open(targetNode,_nodeEditorVisual);
            }
            else
            {
                Debug.LogWarning("Double-clicked node or its underlying data is null.");
            }
        }
    }
}
