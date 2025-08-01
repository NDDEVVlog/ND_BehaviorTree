using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using System.Reflection; // --- ADD THIS ---
using System; // --- ADD THIS ---

namespace ND_BehaviorTree.Editor
{
    public class ND_CustomEdge : Edge
    {
        public Label EdgeLabel { get; private set; }
        private const string DefaultEdgeText = "";

        private readonly VisualElement arrow;
        
        // --- FIX: Add a static cache for the reflection method info ---
        private static MethodInfo getPointsMethod;

        public string Text
        {
            get => EdgeLabel.text;
            set
            {
                if (EdgeLabel.text != value)
                {
                    EdgeLabel.text = value;
                    UpdateLabelVisibility();
                }
            }
        }

        public ND_CustomEdge() : base()
        {   
            

            edgeControl.AddToClassList("nd-custom-edge");
            this.AddToClassList("nd-custom-edge-container");

            

            arrow = new VisualElement();
            arrow.name = "EdgeArrow"; // Optional for debugging
            arrow.AddToClassList("edge-arrow");

            // --- These lines are essential if USS fails to load or for fallback:
            arrow.style.width = 12;
            arrow.style.height = 12;
            arrow.style.backgroundColor = Color.white;
            arrow.style.position = Position.Absolute;

            // Optional debug shape if USS not working:
            arrow.style.unityBackgroundImageTintColor = Color.red;

            Add(arrow);

            EdgeLabel = new Label(DefaultEdgeText)
            {
                style = {
                    position = Position.Absolute,
                    paddingLeft = 5, paddingRight = 5,
                    color = Color.white,
                    fontSize = 10,
                },
                pickingMode = PickingMode.Ignore
            };
            EdgeLabel.AddToClassList("edge-label-background");
            UpdateLabelVisibility();
            Add(EdgeLabel);

            this.AddManipulator(new ContextualMenuManipulator(BuildContextualMenuForEdge));
            this.RegisterCallback<GeometryChangedEvent>(OnEdgeGeometryChanged);
        }

        // --- FIX: New helper method to get points via reflection ---
        private Vector2[] GetEdgePoints()
        {
            // If we haven't found the method yet, find it via reflection
            if (getPointsMethod == null)
            {
                // The method is on the Edge class, but it's part of an internal interface implementation.
                getPointsMethod = typeof(Edge).GetMethod("GetPoints", BindingFlags.NonPublic | BindingFlags.Instance);
                if (getPointsMethod == null)
                {
                    Debug.LogError("Could not find the internal 'GetPoints' method on the Edge class. The API may have changed.");
                    return new Vector2[0];
                }
            }
            
            // Invoke the method on 'this' edge instance.
            // It's a parameterless method, so the second argument is null.
            return getPointsMethod.Invoke(this, null) as Vector2[];
        }

        private void OnEdgeGeometryChanged(GeometryChangedEvent evt)
        {
            // --- FIX: Call our new reflection-based helper method ---
            Vector2[] points = GetEdgePoints();

            if (points == null || points.Length < 2) return;
            
            UpdateArrow(points);
            UpdateLabelPosition(points);
        }

        // The rest of the file is IDENTICAL to the previous version.
        // I am including it here for completeness.
        private void UpdateArrow(Vector2[] points)
        {
            Vector2 startPoint = points[points.Length - 2];
            Vector2 endPoint = points[points.Length - 1];
            
            Vector2 direction = (endPoint - startPoint).normalized;
            
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            arrow.transform.rotation = Quaternion.Euler(0, 0, angle + 90f);
            
            Vector2 arrowPosition = endPoint - (direction * 8f);
            arrow.transform.position = arrowPosition - new Vector2(arrow.layout.width / 2, arrow.layout.height / 2);
        }
        
        private void UpdateLabelPosition(Vector2[] points)
        {
            if (string.IsNullOrEmpty(Text)) return;

            Vector2 center = Vector2.zero;
            foreach (var point in points)
            {
                center += point;
            }
            center /= points.Length;

            Vector2 labelPosition = new Vector2(
                center.x - EdgeLabel.layout.width / 2,
                center.y - EdgeLabel.layout.height / 2
            );

            EdgeLabel.transform.position = labelPosition;
        }

        private void UpdateLabelVisibility()
        {
            EdgeLabel.style.visibility = string.IsNullOrEmpty(EdgeLabel.text) ? Visibility.Hidden : Visibility.Visible;
        }

        private void BuildContextualMenuForEdge(ContextualMenuPopulateEvent evt)
        {
            evt.menu.AppendAction("Edit Connection Text", (a) => OpenEditEdgeTextDialog(), DropdownMenuAction.AlwaysEnabled);
            evt.menu.AppendSeparator();
        }

        private void OpenEditEdgeTextDialog()
        {
            Text = EditorInputDialog.Show("Edit Edge Label", "Enter new text for the edge:", Text);
        }
    }
}