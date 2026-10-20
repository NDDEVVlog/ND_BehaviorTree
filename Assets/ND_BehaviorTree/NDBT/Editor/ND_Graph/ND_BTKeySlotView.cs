using System.Collections.Generic;
using System.Linq;
using ND_BehaviorTree.Editor.Graph;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND_BehaviorTree.Editor.CustomGraph
{
    /// <summary>
    /// A node's blackboard Key fields as slots: "Label  [● KeyName]" (styled by ND_NodeAttachments.uss).
    /// Drop a key from the Blackboard panel (or the Project window) on a slot to link it; click a slot for a
    /// menu of compatible keys. A detached key (local value, not on the blackboard) shows "= value".
    /// Refresh() only writes to the UI when a slot's key, display text or link state changed.
    /// </summary>
    public class ND_BTKeySlotView : VisualElement
    {
        private sealed class Row
        {
            public KeySlot Slot;
            public VisualElement Chip;
            public Label Text;
            public VisualElement Dot;
            public Key ShownKey;
            public string ShownText;
            public string ShownState;
        }

        private readonly Node m_Node;
        private readonly List<Row> m_Rows = new List<Row>();

        public bool HasSlots => m_Rows.Count > 0;

        public ND_BTKeySlotView(Node node, bool compact)
        {
            m_Node = node;
            AddToClassList("nd-slots");
            if (compact) AddToClassList("nd-slots--compact");
            Rebuild();
        }

        public void Rebuild()
        {
            Clear();
            m_Rows.Clear();
            foreach (var slot in KeySlotScanner.Scan(m_Node))
            {
                var row = new VisualElement();
                row.AddToClassList("nd-slot");
                var label = new Label(slot.Label) { pickingMode = PickingMode.Ignore };
                label.AddToClassList("nd-slot__label");

                var chip = new VisualElement();
                chip.AddToClassList("nd-slot__chip");
                var dot = new VisualElement { pickingMode = PickingMode.Ignore };
                dot.AddToClassList("nd-slot__dot");
                var text = new Label { pickingMode = PickingMode.Ignore };
                text.AddToClassList("nd-slot__text");
                chip.Add(dot);
                chip.Add(text);

                var captured = slot;
                chip.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button != 0) return;
                    ShowKeyMenu(captured);
                    evt.StopPropagation();
                });
                RegisterDropTarget(chip, captured);

                row.Add(label);
                row.Add(chip);
                Add(row);
                m_Rows.Add(new Row { Slot = slot, Chip = chip, Text = text, Dot = dot });
            }
            UpdateChips(force: true);
            style.display = m_Rows.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>
        /// Re-reads current keys; rebuilds rows if the node's key fields changed (e.g. a different
        /// [SerializeReference] strategy was chosen in the property window).
        /// </summary>
        public void Refresh()
        {
            var scanned = KeySlotScanner.Scan(m_Node);
            bool structureChanged = scanned.Count != m_Rows.Count;
            for (int i = 0; !structureChanged && i < scanned.Count; i++)
                structureChanged = scanned[i].PropertyPath != m_Rows[i].Slot.PropertyPath || scanned[i].RequiredValueType != m_Rows[i].Slot.RequiredValueType;

            if (structureChanged)
            {
                Rebuild();
                return;
            }
            for (int i = 0; i < scanned.Count; i++) m_Rows[i].Slot.Current = scanned[i].Current;
            UpdateChips(force: false);
        }

        private void UpdateChips(bool force)
        {
            var tree = GetTree();
            foreach (var row in m_Rows)
            {
                var key = row.Slot.Current;
                bool linked = KeySlotScanner.IsLinked(tree, key);
                string state = key == null ? "empty" : linked ? "linked" : "detached";
                string display = key == null ? $"drop {KeySlotScanner.FriendlyTypeName(row.Slot.RequiredValueType)} key"
                    : linked ? (string.IsNullOrEmpty(key.keyName) ? "(unnamed key)" : key.keyName)
                    : $"= {FormatValue(key.GetValueObject())}";

                if (!force && key == row.ShownKey && display == row.ShownText && state == row.ShownState) continue;
                row.ShownKey = key;
                row.ShownText = display;
                row.ShownState = state;

                row.Text.text = display;
                row.Chip.EnableInClassList("nd-slot__chip--empty", state == "empty");
                row.Chip.EnableInClassList("nd-slot__chip--linked", state == "linked");
                row.Chip.EnableInClassList("nd-slot__chip--detached", state == "detached");
                row.Dot.style.backgroundColor = key != null ? KeySlotScanner.KeyColor(key.GetType()) : KeySlotScanner.ValueTypeColor(row.Slot.RequiredValueType);

                string required = KeySlotScanner.FriendlyTypeName(row.Slot.RequiredValueType);
                row.Chip.tooltip = $"{row.Slot.Label}: needs {required}" +
                                   (key == null ? "\nDrag a key from the Blackboard here, or click to choose." :
                                    linked ? $"\nLinked to '{key.keyName}' ({KeySlotScanner.FriendlyTypeName(key.GetValueType())}). Click to change." :
                                             "\nDetached local value (not on the blackboard). Click to link a key.");
            }
        }

        private static string FormatValue(object value) => value == null ? "null" : value is float f ? f.ToString("0.##") : value.ToString();

        private BehaviorTree GetTree() => GetFirstAncestorOfType<ND_BTGraphCanvas>()?.TreeData;

        private void ShowKeyMenu(KeySlot slot)
        {
            var canvas = GetFirstAncestorOfType<ND_BTGraphCanvas>();
            if (canvas == null) return;
            var menu = new GenericMenu();
            var blackboard = canvas.TreeData.blackboard;

            if (blackboard == null)
            {
                menu.AddDisabledItem(new GUIContent("This tree has no Blackboard"));
            }
            else
            {
                var compatible = blackboard.keys.Where(k => k != null && KeySlotScanner.IsCompatible(slot, k, out _)).OrderBy(k => k.keyName).ToList();
                if (compatible.Count == 0) menu.AddDisabledItem(new GUIContent($"No {KeySlotScanner.FriendlyTypeName(slot.RequiredValueType)} keys on the Blackboard"));
                foreach (var key in compatible)
                {
                    var k = key;
                    menu.AddItem(new GUIContent($"{k.keyName}  ({KeySlotScanner.FriendlyTypeName(k.GetValueType())})"), k == slot.Current, () => canvas.AssignKey(m_Node, slot.PropertyPath, k));
                }
            }
            menu.AddSeparator("");
            if (slot.Current != null) menu.AddItem(new GUIContent("Clear"), false, () => canvas.AssignKey(m_Node, slot.PropertyPath, null));
            else menu.AddDisabledItem(new GUIContent("Clear"));
            // Track the linked variable in the Live Debug blackboard table (pinned to the top).
            if (slot.Current != null && blackboard != null && KeySlotScanner.IsLinked(canvas.TreeData, slot.Current))
            {
                string keyName = slot.Current.keyName;
                bool tracked = LiveKeyTracking.IsTracked(blackboard, keyName);
                menu.AddItem(new GUIContent(tracked ? $"Untrack '{keyName}' in Live Debug" : $"Track '{keyName}' in Live Debug"), tracked,
                    () => LiveKeyTracking.Toggle(blackboard, keyName));
            }
            menu.AddItem(new GUIContent("Edit Properties..."), false, () => NodePropertyEditorWindow.Open(m_Node, GetFirstAncestorOfType<ND_BTNodeView>()));
            menu.ShowAsContext();
        }

        private void RegisterDropTarget(VisualElement chip, KeySlot slot)
        {
            void ClearDropState()
            {
                chip.RemoveFromClassList("nd-slot__chip--drop-ok");
                chip.RemoveFromClassList("nd-slot__chip--drop-bad");
            }

            chip.RegisterCallback<DragUpdatedEvent>(evt =>
            {
                var key = DraggedKey();
                if (key == null) return;
                bool ok = KeySlotScanner.IsLinked(GetTree(), key) && KeySlotScanner.IsCompatible(slot, key, out _);
                DragAndDrop.visualMode = ok ? DragAndDropVisualMode.Link : DragAndDropVisualMode.Rejected;
                chip.EnableInClassList("nd-slot__chip--drop-ok", ok);
                chip.EnableInClassList("nd-slot__chip--drop-bad", !ok);
                evt.StopPropagation();
            });
            chip.RegisterCallback<DragPerformEvent>(evt =>
            {
                var key = DraggedKey();
                if (key == null) return;
                DragAndDrop.AcceptDrag();
                ClearDropState();
                GetFirstAncestorOfType<ND_BTGraphCanvas>()?.AssignKey(m_Node, slot.PropertyPath, key);
                evt.StopPropagation();
            });
            chip.RegisterCallback<DragLeaveEvent>(evt => ClearDropState());
            chip.RegisterCallback<DragExitedEvent>(evt => ClearDropState());
        }

        private static Key DraggedKey() => DragAndDrop.objectReferences?.OfType<Key>().FirstOrDefault();
    }
}
