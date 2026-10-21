using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using ND_BehaviorTree.Editor.CustomGraph;
using ND_BehaviorTree.Editor.Graph;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND_BehaviorTree.Story.Editor
{
    /// <summary>
    /// Renders a node's [StorySentence] template as words and value pills, e.g. "[Self] says [Hello!]":
    ///   * a pill bound to a key shows the key name (bold);
    ///   * otherwise it is an inline editor for the typed value (text, number, toggle, enum);
    ///   * "@" opens the key menu (bind to a compatible blackboard key, or back to the typed value).
    /// Edits go through <see cref="StoryEdit.SetValue"/> / the canvas, so they are undoable graph edits.
    /// </summary>
    public class StorySentenceView : VisualElement
    {
        private static readonly Regex s_Placeholder = new Regex(@"\{(\w+)\}", RegexOptions.Compiled);
        private const BindingFlags Fields = BindingFlags.Public | BindingFlags.Instance;

        private readonly Node m_Node;
        private string m_Signature;

        public StorySentenceView(Node node)
        {
            m_Node = node;
            AddToClassList("story-sentence");
            Refresh();
        }

        public static string TemplateOf(Type type) => type.GetCustomAttribute<StorySentenceAttribute>(false)?.Template;

        /// <summary>Rebuilds only when the node's data or its bound key names changed (keeps focus while typing elsewhere).</summary>
        public void Refresh()
        {
            string signature = Signature();
            if (signature == m_Signature) return;
            m_Signature = signature;
            Rebuild();
        }

        private string Signature()
        {
            var sb = new System.Text.StringBuilder().Append(EditorUtility.GetDirtyCount(m_Node));
            foreach (var slot in KeySlotScanner.Scan(m_Node)) sb.Append('|').Append(slot.Current != null ? slot.Current.keyName : "-");
            return sb.ToString();
        }

        private void Rebuild()
        {
            Clear();
            string template = TemplateOf(m_Node.GetType()) ?? StoryEdit.Title(m_Node);
            int last = 0;
            foreach (Match match in s_Placeholder.Matches(template))
            {
                AddWords(template.Substring(last, match.Index - last));
                AddPill(match.Groups[1].Value);
                last = match.Index + match.Length;
            }
            AddWords(template.Substring(last));
        }

        private void AddWords(string text)
        {
            foreach (var word in text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var label = new Label(word) { pickingMode = PickingMode.Ignore };
                label.AddToClassList("story-word");
                Add(label);
            }
        }

        private void AddPill(string name)
        {
            var type = m_Node.GetType();
            var valueField = name == "self" ? null : type.GetField(name, Fields);
            var keyField = type.GetField(name == "self" ? "selfKey" : name + "Key", Fields);
            var key = keyField?.GetValue(m_Node) as Key;

            var pill = new VisualElement().NoDrag();
            pill.AddToClassList("story-pill");
            if (key != null)
            {
                pill.AddToClassList("story-pill--key");
                pill.Add(new Label(string.IsNullOrEmpty(key.keyName) ? "(key)" : key.keyName) { tooltip = $"Blackboard key '{key.keyName}'" });
            }
            else if (name == "self")
            {
                pill.AddToClassList("story-pill--key");
                pill.Add(new Label("Self") { tooltip = "This agent. Use @ to make someone else act." });
            }
            else if (valueField != null)
            {
                pill.Add(CreateEditor(valueField));
            }
            else
            {
                pill.AddToClassList("story-pill--empty");
                pill.Add(new Label(ObjectNames.NicifyVariableName(name)) { tooltip = "Not bound: use @ to pick a blackboard key." });
            }

            if (keyField != null)
            {
                var link = new Button(() => ShowKeyMenu(keyField, valueField != null || name == "self")) { text = "@", tooltip = "Bind to a blackboard key" };
                link.AddToClassList("story-link");
                pill.Add(link.NoDrag());
            }
            Add(pill);
        }

        private VisualElement CreateEditor(FieldInfo field)
        {
            object value = field.GetValue(m_Node);
            string name = field.Name;
            VisualElement editor;
            switch (value)
            {
                case string s:
                {
                    var text = new TextField { value = s, isDelayed = true };
                    text.RegisterValueChangedCallback(e => Commit(name, e.newValue));
                    editor = text;
                    break;
                }
                case float f:
                {
                    var number = new FloatField { value = f, isDelayed = true };
                    number.RegisterValueChangedCallback(e => Commit(name, e.newValue));
                    editor = number;
                    break;
                }
                case int i:
                {
                    var number = new IntegerField { value = i, isDelayed = true };
                    number.RegisterValueChangedCallback(e => Commit(name, e.newValue));
                    editor = number;
                    break;
                }
                case bool b:
                {
                    var toggle = new Toggle { value = b };
                    toggle.RegisterValueChangedCallback(e => Commit(name, e.newValue));
                    editor = toggle;
                    break;
                }
                case Enum e:
                {
                    var enumField = new EnumField(e);
                    enumField.RegisterValueChangedCallback(ev => Commit(name, ev.newValue));
                    editor = enumField;
                    break;
                }
                default:
                    editor = new Label(value != null ? value.ToString() : "null");
                    break;
            }
            editor.AddToClassList("story-value");
            return editor;
        }

        private void Commit(string fieldName, object value) =>
            StoryEdit.SetValue(GetFirstAncestorOfType<ND_BTGraphCanvas>(), m_Node, fieldName, value);

        private void ShowKeyMenu(FieldInfo keyField, bool hasTypedValue)
        {
            var canvas = GetFirstAncestorOfType<ND_BTGraphCanvas>();
            if (canvas == null) return;
            var slot = StoryEdit.Slot(m_Node, keyField.Name);
            var menu = new GenericMenu();
            var blackboard = canvas.TreeData.blackboard;
            if (blackboard == null || slot == null)
            {
                menu.AddDisabledItem(new GUIContent(blackboard == null ? "This tree has no Blackboard" : $"'{keyField.Name}' needs [BlackboardKeyType]"));
            }
            else
            {
                var compatible = blackboard.AllKeys.Where(k => k != null && KeySlotScanner.IsCompatible(slot, k, out _)).OrderBy(k => k.keyName).ToList();
                if (compatible.Count == 0) menu.AddDisabledItem(new GUIContent($"No {KeySlotScanner.FriendlyTypeName(slot.RequiredValueType)} keys on the Blackboard"));
                foreach (var key in compatible)
                {
                    var k = key;
                    menu.AddItem(new GUIContent($"{k.keyName}  ({KeySlotScanner.FriendlyTypeName(k.GetValueType())})"), k == slot.Current,
                        () => canvas.AssignKey(m_Node, keyField.Name, k));
                }
            }
            menu.AddSeparator("");
            string unbind = keyField.Name == "selfKey" ? "Use Self (this agent)" : hasTypedValue ? "Use typed value" : "Clear";
            if (slot?.Current != null) menu.AddItem(new GUIContent(unbind), false, () => canvas.AssignKey(m_Node, keyField.Name, null));
            else menu.AddDisabledItem(new GUIContent(unbind));
            menu.ShowAsContext();
        }
    }
}
