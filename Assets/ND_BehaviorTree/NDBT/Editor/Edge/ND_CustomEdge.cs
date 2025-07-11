using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;

namespace ND_BehaviorTree.Editor
{
    public class ND_CustomEdge : Edge
    {
        public Label EdgeLabel { get; private set; }
        private const string DefaultEdgeText = "";

        public string Text
        {
            get => EdgeLabel.text;
            set
            {
                if (EdgeLabel.text != value)
                {
                    EdgeLabel.text = value;
                    UpdateLabelVisibility();
                    // TODO: Update underlying data model (ND_BTConnection.edgeText)
                }
            }
        }

        public ND_CustomEdge() : base()
        {   
            
            EdgeLabel = new Label(DefaultEdgeText)
            {
                style = {
                    position = Position.Absolute,
                    backgroundColor = new StyleColor(new Color(0.5f, 0.1f, 1f, 0.7f)),
                    color = Color.blue,
                    paddingLeft = 3, paddingRight = 3,
                    unityTextAlign = TextAnchor.MiddleCenter
                },
                pickingMode = PickingMode.Ignore
            };
            UpdateLabelVisibility();
            Add(EdgeLabel);

            this.AddManipulator(new ContextualMenuManipulator(BuildContextualMenuForEdge));
            this.AddToClassList("nd-custom-edge");
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
            Debug.Log("Open Text");
        }
        
    }
}