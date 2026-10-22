using System;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND.Dialogue.Editor
{
    /// <summary>
    /// Character UI shared by the Dialogue Editor's Characters section and the Character Database editor: the card of
    /// the horizontal strip, and the detail groups (Identity, Emotions, Attributes with the template's (kept, value editable),
    /// subclass settings) bound to wherever the character is stored.
    /// </summary>
    public static class CharacterSections
    {
        /// <summary>A selectable card: portrait (or a coloured initial), title, subtitle.</summary>
        public static VisualElement Card(Actor actor, Sprite portrait, string title, string subtitle, string tooltip, bool selected, bool shared)
        {
            var card = new VisualElement { tooltip = tooltip ?? string.Empty };
            card.AddToClassList("dlg-charcard");
            card.EnableInClassList("dlg-charcard--selected", selected);
            card.EnableInClassList("dlg-charcard--shared", shared);
            if (actor == null)
            {
                card.Add(new Label("(empty)") { pickingMode = PickingMode.Ignore });
                return card;
            }
            var face = new VisualElement { pickingMode = PickingMode.Ignore };
            face.AddToClassList("dlg-charcard__face");
            if (portrait != null) face.style.backgroundImage = new StyleBackground(portrait);
            else
            {
                face.style.backgroundColor = DialogueNodeView.ActorColor(actor.name);
                face.Add(new Label(string.IsNullOrEmpty(actor.name) ? "?" : actor.name.Substring(0, 1).ToUpperInvariant()) { pickingMode = PickingMode.Ignore });
            }
            card.Add(face);
            var name = new Label(title) { pickingMode = PickingMode.Ignore };
            name.AddToClassList("dlg-charcard__name");
            card.Add(name);
            var sub = new Label(subtitle) { pickingMode = PickingMode.Ignore };
            sub.AddToClassList("dlg-charcard__sub");
            card.Add(sub);
            return card;
        }

        /// <summary>The character's own default-emotion portrait (no database look applied).</summary>
        public static Sprite DefaultPortrait(Actor actor, string locale)
        {
            if (actor == null) return null;
            var emotion = actor.emotions.FirstOrDefault(e => e != null && e.id == actor.defaultEmotion) ?? actor.emotions.FirstOrDefault(e => e != null);
            return emotion != null ? DialogueNodeView.EditorPortrait(emotion, locale) : null;
        }

        /// <summary>"player", the subclass name, or the character's id name.</summary>
        public static string Kind(Actor actor) =>
            actor.isPlayer ? "player" : actor.GetType() == typeof(Actor) ? actor.name : ObjectNames.NicifyVariableName(actor.GetType().Name);

        /// <summary>
        /// Adds the detail groups for <paramref name="actor"/>, whose serialized form is <paramref name="property"/> of
        /// <paramref name="owner"/> (the dialogue or character database it is stored in). The id is not editable.
        /// </summary>
        public static void BuildDetails(VisualElement target, Actor actor, SerializedObject owner, SerializedProperty property, Action<string> setDefaultEmotion, Action changed = null)
        {
            bool ownAttributes = actor.GetType().GetProperty(nameof(Actor.AttributeDefinitions))?.DeclaringType == typeof(Actor);
            var identity = Group("Identity");
            var emotions = Group("Emotions", "How this character looks per emotion. Lines pick one; empty = the default emotion.");
            var attributes = Group("Attributes", $"State that changes during play. Conditions and actions use them as '{actor.name}.<attribute>'.");
            var more = Group($"{ObjectNames.NicifyVariableName(actor.GetType().Name)} settings");

            // Every field, the subclass's included (managed reference), routed into groups.
            var child = property.Copy();
            var end = property.GetEndProperty();
            for (bool enter = true; child.NextVisible(enter) && !SerializedProperty.EqualContents(child, end); enter = false)
            {
                switch (child.name)
                {
                    case nameof(Actor.id): break;
                    case nameof(Actor.name):
                    case nameof(Actor.displayName):
                    case nameof(Actor.isPlayer): identity.Add(Bound(child.Copy(), owner)); break;
                    case nameof(Actor.defaultEmotion): identity.Add(DefaultEmotionField(actor, setDefaultEmotion)); break;
                    case nameof(Actor.emotions): emotions.Add(Bound(child.Copy(), owner)); break;
                    case nameof(Actor.template): attributes.Add(Bound(child.Copy(), owner)); break;
                    case nameof(Actor.attributes):
                        if (!ownAttributes) break;
                        attributes.Add(TemplateAttributes(actor, owner.targetObject, changed));
                        attributes.Add(Bound(child.Copy(), owner));
                        break;
                    default: more.Add(Bound(child.Copy(), owner)); break;
                }
            }
            if (!ownAttributes) attributes.Add(AttributeOverview(actor));
            target.Add(identity);
            target.Add(emotions);
            target.Add(attributes);
            if (more.childCount > 1) target.Add(more);
        }

        public static VisualElement DefaultEmotionField(Actor actor, Action<string> set)
        {
            var ids = actor.emotions.Where(e => e != null && !string.IsNullOrEmpty(e.id)).Select(e => e.id).Distinct().ToList();
            if (!ids.Contains(actor.defaultEmotion)) ids.Insert(0, actor.defaultEmotion ?? string.Empty);
            var field = new PopupField<string>("Default Emotion", ids, actor.defaultEmotion ?? string.Empty) { tooltip = "Emotion used by lines that do not name one." };
            field.RegisterValueChangedCallback(evt => set?.Invoke(evt.newValue));
            return field;
        }

        /// <summary>
        /// The template's attributes on this character: they cannot be removed or renamed here (that is done on the
        /// template), but each one can start with this character's own value; ↺ goes back to the template's.
        /// Edits are recorded on <paramref name="owner"/> (the asset that stores the character).
        /// </summary>
        public static VisualElement TemplateAttributes(Actor actor, UnityEngine.Object owner, Action changed = null)
        {
            var box = new VisualElement();
            if (actor.template == null) return box;
            box.Add(new Label($"From template '{actor.template.name}' — cannot be removed here; the value can be this character's own:")
                { style = { color = new Color(0.63f, 0.66f, 0.72f), marginTop = 4, whiteSpace = WhiteSpace.Normal } });
            if (actor.template.templateAttributes.Count == 0) box.Add(new Label("  (the template declares none yet)") { style = { color = new Color(0.55f, 0.57f, 0.62f) } });
            foreach (var a in actor.template.templateAttributes.Where(a => a != null && !string.IsNullOrEmpty(a.name)))
            {
                var declared = a;
                var own = actor.FindTemplateValue(declared.name);
                bool overridden = own != null && own.initialValue.type == declared.initialValue.type;
                var color = DialogueVariableStyle.ForType(declared.initialValue.type);

                var row = new VisualElement { tooltip = string.IsNullOrEmpty(declared.description) ? string.Empty : declared.description };
                row.AddToClassList("dlg-varrow");
                row.AddToClassList("dlg-templaterow");
                var dot = new VisualElement { style = { backgroundColor = color } };
                dot.AddToClassList("dlg-varrow__dot");
                row.Add(dot);
                row.Add(new Label($"🔒 {declared.name}") { style = { width = 130, unityTextAlign = TextAnchor.MiddleLeft }, tooltip = "From the template: cannot be removed or renamed here." });
                row.Add(ValueEditor(overridden ? own.initialValue : declared.initialValue, value =>
                {
                    Undo.RecordObject(owner, $"Change {declared.name} of {actor.name}");
                    actor.SetTemplateValue(declared.name, value);
                    EditorUtility.SetDirty(owner);
                    changed?.Invoke();
                }));
                var type = new Label(overridden ? $"{declared.initialValue.type} · own" : $"{declared.initialValue.type} · template")
                    { style = { color = color, width = 110, unityTextAlign = TextAnchor.MiddleRight } };
                type.AddToClassList("dlg-varrow__type");
                row.Add(type);
                var reset = new Button(() =>
                {
                    Undo.RecordObject(owner, $"Reset {declared.name} of {actor.name}");
                    actor.SetTemplateValue(declared.name, null);
                    EditorUtility.SetDirty(owner);
                    changed?.Invoke();
                }) { text = "↺", tooltip = $"Back to the template's value ({declared.initialValue})" };
                reset.AddToClassList("dlg-inspector__remove");
                reset.SetEnabled(overridden);
                row.Add(reset);
                box.Add(row);
            }
            box.Add(new Label("Own attributes:") { style = { color = new Color(0.63f, 0.66f, 0.72f), marginTop = 4 } });
            return box;
        }

        /// <summary>A field matching the value's type (checkbox, number, text, xy/xyz, colour, object); calls <paramref name="set"/> on change.</summary>
        public static VisualElement ValueEditor(VariableValue value, Action<VariableValue> set)
        {
            VisualElement field;
            switch (value.type)
            {
                case VariableType.Bool:
                {
                    var f = new Toggle { value = value.boolValue };
                    f.RegisterValueChangedCallback(e => set(VariableValue.Bool(e.newValue)));
                    field = f;
                    break;
                }
                case VariableType.Int:
                {
                    var f = new IntegerField { value = value.intValue, isDelayed = true };
                    f.RegisterValueChangedCallback(e => set(VariableValue.Int(e.newValue)));
                    field = f;
                    break;
                }
                case VariableType.Float:
                {
                    var f = new FloatField { value = value.floatValue, isDelayed = true };
                    f.RegisterValueChangedCallback(e => set(VariableValue.Float(e.newValue)));
                    field = f;
                    break;
                }
                case VariableType.Vector2:
                {
                    var f = new Vector2Field { value = value.vectorValue };
                    f.RegisterValueChangedCallback(e => set(VariableValue.Vector2(e.newValue)));
                    field = f;
                    break;
                }
                case VariableType.Vector3:
                {
                    var f = new Vector3Field { value = value.vectorValue };
                    f.RegisterValueChangedCallback(e => set(VariableValue.Vector3(e.newValue)));
                    field = f;
                    break;
                }
                case VariableType.Color:
                {
                    var f = new ColorField { value = value.colorValue };
                    f.RegisterValueChangedCallback(e => set(VariableValue.Color(e.newValue)));
                    field = f;
                    break;
                }
                case VariableType.Object:
                {
                    var f = new ObjectField { objectType = typeof(UnityEngine.Object), allowSceneObjects = false, value = value.objectValue };
                    f.RegisterValueChangedCallback(e => set(VariableValue.Object(e.newValue)));
                    field = f;
                    break;
                }
                default:
                {
                    var f = new TextField { value = value.stringValue ?? string.Empty, isDelayed = true };
                    f.RegisterValueChangedCallback(e => set(VariableValue.String(e.newValue)));
                    field = f;
                    break;
                }
            }
            field.style.flexGrow = 1;
            field.style.flexShrink = 1;
            return field;
        }

        /// <summary>Read-only list of attributes that come from elsewhere (e.g. a character's blackboard).</summary>
        public static VisualElement AttributeOverview(Actor actor)
        {
            var box = new VisualElement();
            var list = actor.AttributeDefinitions.Where(a => a != null).ToList();
            if (list.Count == 0) box.Add(new Label("(none)") { style = { color = new Color(0.6f, 0.6f, 0.65f) } });
            foreach (var attribute in list) box.Add(VariableRow(attribute.name, attribute.initialValue.type.ToString(), DialogueVariableStyle.ForType(attribute.initialValue.type)));
            return box;
        }

        public static VisualElement Bound(SerializedProperty property, SerializedObject owner)
        {
            if (property == null) return new HelpBox("Missing property (asset version mismatch?).", HelpBoxMessageType.Error);
            var field = new PropertyField(property);
            field.Bind(owner);
            return field;
        }

        /// <summary>A titled card that groups related fields.</summary>
        public static VisualElement Group(string title, string hint = null)
        {
            var group = new VisualElement();
            group.AddToClassList("dlg-group");
            var label = new Label(title) { tooltip = hint ?? string.Empty };
            label.AddToClassList("dlg-group__title");
            group.Add(label);
            if (!string.IsNullOrEmpty(hint))
            {
                var sub = new Label(hint);
                sub.AddToClassList("dlg-group__hint");
                group.Add(sub);
            }
            return group;
        }

        /// <summary><c>● name ........ Type</c></summary>
        public static VisualElement VariableRow(string name, string typeLabel, Color color)
        {
            var row = new VisualElement();
            row.AddToClassList("dlg-varrow");
            var dot = new VisualElement { style = { backgroundColor = color } };
            dot.AddToClassList("dlg-varrow__dot");
            row.Add(dot);
            row.Add(new Label(name) { style = { flexGrow = 1 } });
            var type = new Label(typeLabel) { style = { color = color } };
            type.AddToClassList("dlg-varrow__type");
            row.Add(type);
            return row;
        }

        public static Label Pill(string text, string extraClass = null)
        {
            var label = new Label(text);
            label.AddToClassList("dlg-pill");
            if (extraClass != null) label.AddToClassList(extraClass);
            return label;
        }
    }
}
