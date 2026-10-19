using UnityEngine;
using UnityEngine.UIElements;

namespace ND_BehaviorTree.Editor.CustomGraph
{
    /// <summary>
    /// Sticky-note card: coloured top band, dark text on a light paper colour, editable title and multi-line body.
    /// Drag by the ≡ grip/header, resize from any edge/corner (or the ◢ grip).
    /// Text is saved 0.6 s after typing stops, on focus loss, and before the view is removed — never lost on close.
    /// </summary>
    public class ND_BTNoteView : ND_BTBoxView
    {
        private const long SaveDelayMs = 600;
        private static readonly Color TextColor = new Color(0.12f, 0.12f, 0.12f, 1f);

        public GraphNote Note { get; }
        private readonly ND_BTGraphCanvas m_Canvas;
        private readonly TextField m_TitleField;
        private readonly TextField m_ContentField;
        private readonly Label m_GripLabel;
        private IVisualElementScheduledItem m_PendingSave;
        private bool m_TextDirty;

        public ND_BTNoteView(GraphNote note, ND_BTGraphCanvas canvas)
        {
            Note = note;
            m_Canvas = canvas;

            Header.style.minHeight = 28;
            Header.style.borderTopLeftRadius = Header.style.borderTopRightRadius = 7;
            Header.style.paddingLeft = 4;
            m_GripLabel = new Label("≡") { tooltip = "Drag to move", style = { fontSize = 16, width = 18, unityTextAlign = TextAnchor.MiddleCenter } };
            Header.Add(m_GripLabel);
            m_TitleField = CreateTransparentField(multiline: false, bold: true, fontSize: 13);
            m_TitleField.style.flexGrow = 1;
            Header.Add(m_TitleField);
            Header.tooltip = "Drag the ≡ grip to move. Right-click for options.";
            Header.AddManipulator(new ContextualMenuManipulator(BuildMenu));

            m_ContentField = CreateTransparentField(multiline: true, bold: false, fontSize: 12);
            m_ContentField.style.flexGrow = 1;
            m_ContentField.style.marginLeft = m_ContentField.style.marginRight = 6;
            m_ContentField.style.marginTop = 4;
            m_ContentField.style.marginBottom = 14;
            Add(m_ContentField);

            foreach (var field in new[] { m_TitleField, m_ContentField })
            {
                field.RegisterValueChangedCallback(e => ScheduleSave());
                field.RegisterCallback<FocusOutEvent>(e => SaveTextNow());
            }
            RegisterCallback<DetachFromPanelEvent>(e => SaveTextNow());

            Refresh();
        }

        private static TextField CreateTransparentField(bool multiline, bool bold, int fontSize)
        {
            var field = new TextField { multiline = multiline };
            field.style.whiteSpace = WhiteSpace.Normal;
            field.style.marginLeft = field.style.marginRight = field.style.marginTop = field.style.marginBottom = 0;
            var input = field.Q("unity-text-input");
            if (input != null)
            {
                input.style.backgroundColor = Color.clear;
                input.style.borderTopWidth = input.style.borderBottomWidth = input.style.borderLeftWidth = input.style.borderRightWidth = 0;
                input.style.color = TextColor;
                input.style.fontSize = fontSize;
                input.style.unityFontStyleAndWeight = bold ? FontStyle.Bold : FontStyle.Normal;
                input.style.whiteSpace = WhiteSpace.Normal;
                if (multiline) input.style.unityTextAlign = TextAnchor.UpperLeft;
            }
            return field;
        }

        public void Refresh()
        {
            SetViewRect(Note.rect);
            if (!m_TextDirty)
            {
                m_TitleField.SetValueWithoutNotify(Note.title);
                m_ContentField.SetValueWithoutNotify(Note.content);
            }
            var c = Note.color;
            var paper = Color.Lerp(new Color(c.r, c.g, c.b, 1f), Color.white, 0.55f);
            var band = new Color(c.r * 0.85f, c.g * 0.85f, c.b * 0.85f, 1f);
            style.backgroundColor = paper;
            Header.style.backgroundColor = band;
            m_GripLabel.style.color = TextColor;
            SetBorder(this, new Color(c.r * 0.55f, c.g * 0.55f, c.b * 0.55f, 1f), 1f, 8f);
            AccentColor = new Color(c.r * 0.6f, c.g * 0.6f, c.b * 0.6f, 0.8f);
            SetGripColor(new Color(c.r * 0.5f, c.g * 0.5f, c.b * 0.5f, 0.9f));
        }

        private void ScheduleSave()
        {
            m_TextDirty = true;
            if (m_PendingSave == null) m_PendingSave = schedule.Execute(SaveTextNow);
            m_PendingSave.ExecuteLater(SaveDelayMs);
        }

        private void SaveTextNow()
        {
            m_PendingSave?.Pause();
            if (!m_TextDirty) return;
            m_TextDirty = false;
            m_Canvas.CommitNoteEdit(Note, m_TitleField.value, m_ContentField.value, Note.color, Note.rect);
        }

        protected override void OnMoveCommitted(Rect startRect, Rect finalRect)
        {
            SaveTextNow();
            m_Canvas.CommitNoteEdit(Note, Note.title, Note.content, Note.color, finalRect);
        }

        protected override void OnResizeCommitted(Rect finalRect)
        {
            SaveTextNow();
            m_Canvas.CommitNoteEdit(Note, Note.title, Note.content, Note.color, finalRect);
        }

        private void BuildMenu(ContextualMenuPopulateEvent evt)
        {
            foreach (var (name, color) in ColorPresets)
            {
                var preset = color;
                evt.menu.AppendAction($"Color/{name}", a =>
                {
                    SaveTextNow();
                    m_Canvas.CommitNoteEdit(Note, Note.title, Note.content, preset, Note.rect);
                });
            }
            evt.menu.AppendSeparator();
            evt.menu.AppendAction("Delete Note", a => { m_TextDirty = false; m_Canvas.DeleteNote(Note); });
            evt.StopPropagation();
        }
    }
}
