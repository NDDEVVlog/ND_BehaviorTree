using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND_BehaviorTree.Editor.CustomGraph
{
    public enum BTPortDirection { Input, Output }
    public enum BTPortCapacity { Single, Multi }

    public class ND_BTPortView : VisualElement
    {
        public ND_BTNodeView ParentNodeView { get; private set; }
        public BTPortDirection Direction { get; private set; }
        public BTPortCapacity Capacity { get; private set; }
        public List<ND_BTEdgeView> Connections { get; private set; } = new List<ND_BTEdgeView>();
        public Vector2 GlobalCenter => parent.LocalToWorld(layout.center);

        public ND_BTPortView(ND_BTNodeView parentNodeView, BTPortDirection direction, BTPortCapacity capacity)
        {
            ParentNodeView = parentNodeView;
            Direction = direction;
            Capacity = capacity;
            
            var theme = ND_BehaviorTreeSetting.Instance.portTheme;

            style.width = style.height = theme.portSize;
            style.backgroundColor = direction == BTPortDirection.Input ? theme.inputPortColor : theme.outputPortColor;
            style.borderTopLeftRadius = style.borderTopRightRadius = style.borderBottomLeftRadius = style.borderBottomRightRadius = theme.portSize / 2f;
            style.alignSelf = Align.Center;

            if (direction == BTPortDirection.Input) style.marginTop = -theme.portSize / 2f;
            else style.marginBottom = -theme.portSize / 2f;

            RegisterCallback<PointerDownEvent>(evt => {
                if (evt.button == 0) {
                    GetFirstAncestorOfType<ND_BTGraphCanvas>()?.ConnectionManager.StartConnection(this, evt.position);
                    evt.StopPropagation();
                }
            });
        }

        public void Connect(ND_BTEdgeView edge) { if (!Connections.Contains(edge)) Connections.Add(edge); }
        public void Disconnect(ND_BTEdgeView edge) { Connections.Remove(edge); }
    }
}