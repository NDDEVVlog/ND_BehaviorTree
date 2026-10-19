using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND_BehaviorTree.Editor.CustomGraph
{
    /// <summary>
    /// Group frame: tinted body, solid coloured header with title + member count, rounded border.
    /// Drag the header to move the group with its member nodes; drag any edge/corner (or the ◢ grip) to resize.
    /// The body ignores the pointer so box-selection and clicks still reach the canvas.
    /// Right-click the header: Rename, Color, Delete. Double-click the title to rename.
    /// </summary>
    public class ND_BTGroupView : ND_BTBoxView
    {
        public GraphGroup Group { get; }
        private readonly ND_BTGraphCanvas m_Canvas;
        private readonly Label m_Title;
        private readonly Label m_CountBadge;
        private readonly VisualElement m_AccentBar;
        private readonly Dictionary<ND_BTNodeView, Vector2> m_MemberStartPositions = new Dictionary<ND_BTNodeView, Vector2>();

        public ND_BTGroupView(GraphGroup group, ND_BTGraphCanvas canvas)
        {
            Group = group;
            m_Canvas = canvas;
            pickingMode = PickingMode.Ignore;

            Header.style.minHeight = 30;
            Header.style.borderTopLeftRadius = Header.style.borderTopRightRadius = 9;

            m_AccentBar = new VisualElement { pickingMode = PickingMode.Ignore, style = { width = 4, height = 16, marginRight = 8, borderTopLeftRadius = 2, borderTopRightRadius = 2, borderBottomLeftRadius = 2, borderBottomRightRadius = 2 } };
            m_Title = new Label { pickingMode = PickingMode.Ignore, style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 14, color = Color.white, flexGrow = 1, unityTextAlign = TextAnchor.MiddleLeft } };
            m_CountBadge = new Label
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    fontSize = 10, color = new Color(1f, 1f, 1f, 0.85f),
                    backgroundColor = new Color(0f, 0f, 0f, 0.25f),
                    paddingLeft = 6, paddingRight = 6, paddingTop = 1, paddingBottom = 1,
                    borderTopLeftRadius = 8, borderTopRightRadius = 8, borderBottomLeftRadius = 8, borderBottomRightRadius = 8
                }
            };
            Header.Add(m_AccentBar);
            Header.Add(m_Title);
            Header.Add(m_CountBadge);
            Header.tooltip = "Drag to move the group and its nodes. Double-click to rename. Right-click for options.";
            Header.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button == 0 && evt.clickCount == 2) { BeginRename(); evt.StopPropagation(); }
            }, TrickleDown.TrickleDown);
            Header.AddManipulator(new ContextualMenuManipulator(BuildMenu));

            Refresh();
        }

        public void Refresh()
        {
            SetViewRect(Group.rect);
            m_Title.text = string.IsNullOrEmpty(Group.title) ? "Group" : Group.title;
            int count = Group.nodeIds.Count;
            m_CountBadge.text = count == 1 ? "1 node" : $"{count} nodes";

            var c = Group.color;
            style.backgroundColor = new Color(c.r, c.g, c.b, 0.10f);
            Header.style.backgroundColor = new Color(c.r * 0.75f, c.g * 0.75f, c.b * 0.75f, 0.95f);
            m_AccentBar.style.backgroundColor = Color.Lerp(new Color(c.r, c.g, c.b, 1f), Color.white, 0.45f);
            SetBorder(this, new Color(c.r, c.g, c.b, 0.85f), 2f, 10f);
            AccentColor = new Color(c.r, c.g, c.b, 0.8f);
            SetGripColor(new Color(c.r, c.g, c.b, 0.9f));
        }

        protected override void OnMovePreview(Vector2 delta)
        {
            if (m_MemberStartPositions.Count == 0)
            {
                foreach (var id in Group.nodeIds)
                {
                    var view = m_Canvas.GetNodeView(id);
                    if (view != null) m_MemberStartPositions[view] = view.ViewPosition;
                }
            }
            foreach (var kvp in m_MemberStartPositions) kvp.Key.SetPosition(kvp.Value + delta);
        }

        protected override void OnMoveCommitted(Rect startRect, Rect finalRect)
        {
            m_MemberStartPositions.Clear();
            m_Canvas.CommitGroupMove(Group, finalRect.position);
        }

        protected override void OnResizeCommitted(Rect finalRect) => m_Canvas.CommitGroupResize(Group, finalRect);

        private void BuildMenu(ContextualMenuPopulateEvent evt)
        {
            evt.menu.AppendAction("Rename", a => BeginRename());
            foreach (var (name, color) in ColorPresets)
            {
                var preset = color;
                evt.menu.AppendAction($"Color/{name}", a => m_Canvas.CommitGroupEdit(Group, Group.title, preset));
            }
            evt.menu.AppendSeparator();
            evt.menu.AppendAction("Delete Group (keep nodes)", a => m_Canvas.DeleteGroup(Group));
            evt.StopPropagation();
        }

        private void BeginRename()
        {
            var field = new TextField { value = Group.title, style = { flexGrow = 1 } };
            int index = Header.IndexOf(m_Title);
            m_Title.style.display = DisplayStyle.None;
            Header.Insert(index, field);
            field.schedule.Execute(() => field.Q("unity-text-input")?.Focus());

            void Finish(bool apply)
            {
                if (field.parent == null) return;
                string value = field.value;
                Header.Remove(field);
                m_Title.style.display = DisplayStyle.Flex;
                if (apply && value != Group.title) m_Canvas.CommitGroupEdit(Group, value, Group.color);
            }

            field.RegisterCallback<FocusOutEvent>(e => Finish(true));
            field.RegisterCallback<DetachFromPanelEvent>(e => Finish(true));
            field.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) { Finish(true); e.StopPropagation(); }
                else if (e.keyCode == KeyCode.Escape) { Finish(false); e.StopPropagation(); }
            });
        }
    }
}
