using System;
using System.Collections.Generic;
using System.Linq;
using ND_BehaviorTree.Editor.CustomGraph;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND_BehaviorTree.Editor
{
    /// <summary>
    /// Floating Blackboard panel (styled by ND_Blackboard.uss).
    ///   Header: title, asset name, key count, [+] add key, [▾] collapse — drag the header to move the panel.
    ///   Search box filters keys by name or type.
    ///   Each key is a two-line card:  ⠿ ● Name ........ Type ✕  /  value editor (full width).
    ///   Drag ⠿ / ● / type onto a node's key slot to link the key. Resize the panel from its right edge.
    /// Width and collapsed state are remembered per user (EditorPrefs). The theme's custom USS (Settings →
    /// Blackboard Theme) is applied on top so it can override these classes.
    /// </summary>
    public class BlackboardView : VisualElement
    {
        private const float MinWidth = 240f, MaxWidth = 560f, DefaultWidth = 300f;
        private const string WidthPref = "ND_BehaviorTree.Blackboard.Width";
        private const string CollapsedPref = "ND_BehaviorTree.Blackboard.Collapsed";

        private readonly SerializedObject m_treeSerializer;
        private readonly BehaviorTree m_BTree;

        private readonly Label m_Subtitle;
        private readonly Label m_Count;
        private readonly Button m_AddButton;
        private readonly Button m_CollapseButton;
        private readonly TextField m_Search;
        private readonly ScrollView m_List;

        public BlackboardView(SerializedObject treeSerializer)
        {
            m_treeSerializer = treeSerializer;
            m_BTree = treeSerializer.targetObject as BehaviorTree;

            AddToClassList("nd-bb-panel");
            ND_BTStyles.Apply(this, ND_BTStyles.Blackboard);
            var themeUss = ND_BehaviorTreeSetting.Instance.blackboardTheme.customUSS;
            if (themeUss != null) styleSheets.Add(themeUss);

            style.width = Mathf.Clamp(EditorPrefs.GetFloat(WidthPref, DefaultWidth), MinWidth, MaxWidth);
            style.maxHeight = Length.Percent(85);

            // Header
            var header = new VisualElement();
            header.AddToClassList("nd-bb-header");
            var texts = new VisualElement();
            texts.AddToClassList("nd-bb-header__texts");
            var title = new Label("Blackboard");
            title.AddToClassList("nd-bb-title");
            m_Subtitle = new Label();
            m_Subtitle.AddToClassList("nd-bb-subtitle");
            texts.Add(title);
            texts.Add(m_Subtitle);
            m_Count = new Label();
            m_Count.AddToClassList("nd-bb-count");
            m_AddButton = new Button(OnAddKeyClicked) { text = "+", tooltip = "Add a key" };
            m_AddButton.AddToClassList("nd-bb-iconbtn");
            m_AddButton.AddToClassList("nd-bb-iconbtn--accent");
            m_CollapseButton = new Button(ToggleCollapsed) { tooltip = "Collapse / expand" };
            m_CollapseButton.AddToClassList("nd-bb-iconbtn");
            header.Add(texts);
            header.Add(m_Count);
            header.Add(m_AddButton);
            header.Add(m_CollapseButton);
            Add(header);
            SetupHeaderDrag(header);

            // Body
            var body = new VisualElement();
            body.AddToClassList("nd-bb-body");
            m_Search = new TextField();
            m_Search.AddToClassList("nd-bb-search");
            m_Search.textEdition.placeholder = "Search keys…";
            m_Search.RegisterValueChangedCallback(e => PopulateView());
            body.Add(m_Search);
            m_List = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            m_List.AddToClassList("nd-bb-list");
            body.Add(m_List);
            Add(body);

            var resize = new VisualElement { tooltip = "Drag to resize" };
            resize.AddToClassList("nd-bb-resize");
            hierarchy.Add(resize);
            SetupResize(resize);

            SetCollapsed(EditorPrefs.GetBool(CollapsedPref, false));
            PopulateView();
        }

        #region Layout: move, resize, collapse

        private void SetupHeaderDrag(VisualElement header)
        {
            bool dragging = false;
            Vector2 startMouse = default, startPos = default;
            header.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0 || e.target is Button) return;
                dragging = true;
                startMouse = e.position;
                startPos = new Vector2(resolvedStyle.left, resolvedStyle.top);
                header.CapturePointer(e.pointerId);
                BringToFront();
                e.StopPropagation();
            });
            header.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!dragging || !header.HasPointerCapture(e.pointerId)) return;
                Vector2 d = (Vector2)e.position - startMouse;
                style.left = Mathf.Max(0, startPos.x + d.x);
                style.top = Mathf.Max(0, startPos.y + d.y);
                e.StopPropagation();
            });
            header.RegisterCallback<PointerUpEvent>(e =>
            {
                if (!dragging || !header.HasPointerCapture(e.pointerId)) return;
                dragging = false;
                header.ReleasePointer(e.pointerId);
                e.StopPropagation();
            });
        }

        private void SetupResize(VisualElement handle)
        {
            bool resizing = false;
            float startMouseX = 0, startWidth = 0;
            handle.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0) return;
                resizing = true;
                startMouseX = e.position.x;
                startWidth = resolvedStyle.width;
                handle.CapturePointer(e.pointerId);
                e.StopPropagation();
            });
            handle.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!resizing || !handle.HasPointerCapture(e.pointerId)) return;
                style.width = Mathf.Clamp(startWidth + (e.position.x - startMouseX), MinWidth, MaxWidth);
                e.StopPropagation();
            });
            handle.RegisterCallback<PointerUpEvent>(e =>
            {
                if (!resizing || !handle.HasPointerCapture(e.pointerId)) return;
                resizing = false;
                handle.ReleasePointer(e.pointerId);
                EditorPrefs.SetFloat(WidthPref, resolvedStyle.width);
                e.StopPropagation();
            });
        }

        private void ToggleCollapsed() => SetCollapsed(!ClassListContains("nd-bb-panel--collapsed"));

        private void SetCollapsed(bool collapsed)
        {
            EnableInClassList("nd-bb-panel--collapsed", collapsed);
            m_CollapseButton.text = collapsed ? "▸" : "▾";
            EditorPrefs.SetBool(CollapsedPref, collapsed);
        }

        #endregion

        #region Keys

        public void PopulateView()
        {
            m_List.Clear();
            var blackboard = m_BTree.blackboard;
            m_Subtitle.text = blackboard != null ? blackboard.name : "No blackboard asset";
            m_AddButton.SetEnabled(true);

            if (blackboard == null)
            {
                m_Count.style.display = DisplayStyle.None;
                m_Search.style.display = DisplayStyle.None;
                m_List.Add(EmptyState("This tree has no Blackboard yet.", "Create Blackboard", CreateAndAssignBlackboard));
                return;
            }

            var keys = blackboard.keys.Where(k => k != null).ToList();
            m_Count.text = keys.Count.ToString();
            m_Count.style.display = DisplayStyle.Flex;
            m_Search.style.display = keys.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;

            string filter = m_Search.value?.Trim() ?? string.Empty;
            var shown = keys.Where(k => filter.Length == 0
                                        || (k.keyName ?? string.Empty).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
                                        || Graph.KeySlotScanner.FriendlyTypeName(k.GetValueType()).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                            .ToList();

            if (keys.Count == 0) m_List.Add(EmptyState("No keys yet. Keys are the shared variables your nodes read and write.", "Add Key", OnAddKeyClicked));
            else if (shown.Count == 0) m_List.Add(EmptyState($"No key matches \"{filter}\".", null, null));

            foreach (var key in shown) m_List.Add(CreateKeyCard(key));
        }

        private static VisualElement EmptyState(string text, string buttonText, Action onClick)
        {
            var box = new VisualElement();
            box.AddToClassList("nd-bb-empty");
            var label = new Label(text);
            label.AddToClassList("nd-bb-empty__text");
            box.Add(label);
            if (buttonText != null) box.Add(new Button(onClick) { text = buttonText });
            return box;
        }

        private VisualElement CreateKeyCard(Key key)
        {
            Color typeColor = Graph.KeySlotScanner.KeyColor(key.GetType());
            string typeName = Graph.KeySlotScanner.FriendlyTypeName(key.GetValueType());

            var card = new VisualElement { tooltip = $"'{key.keyName}' · {typeName}\nDrag ⠿ onto a node's key slot to link it." };
            card.AddToClassList("nd-bb-key");

            var top = new VisualElement();
            top.AddToClassList("nd-bb-key__top");
            var grip = new Label("⠿") { tooltip = "Drag onto a node's key slot" };
            grip.AddToClassList("nd-bb-grip");
            var dot = new VisualElement { style = { backgroundColor = typeColor } };
            dot.AddToClassList("nd-bb-dot");
            var nameField = new TextField { value = key.keyName, isDelayed = true }; // commit on Enter / focus loss
            nameField.AddToClassList("nd-bb-name");
            nameField.RegisterValueChangedCallback(e => RenameKey(key, e.newValue));
            var type = new Label(typeName) { style = { color = typeColor } };
            type.AddToClassList("nd-bb-type");
            var delete = new Button(() => DeleteKey(key)) { text = "✕", tooltip = "Delete key" };
            delete.AddToClassList("nd-bb-delete");
            top.Add(grip); top.Add(dot); top.Add(nameField); top.Add(type); top.Add(delete);
            card.Add(top);

            var valueRow = new VisualElement();
            valueRow.AddToClassList("nd-bb-key__value");
            valueRow.Add(CreateValueEditor(key));
            card.Add(valueRow);

            foreach (var handle in new VisualElement[] { grip, dot, type }) RegisterKeyDragSource(handle, key);

            // Right-click: pin this key to the top of the Live Debug blackboard table.
            card.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                var owner = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GetAssetPath(key)) as Blackboard;
                if (owner == null) return;
                bool tracked = Graph.LiveKeyTracking.IsTracked(owner, key.keyName);
                evt.menu.AppendAction(tracked ? "Untrack in Live Debug" : "Track in Live Debug", _ => Graph.LiveKeyTracking.Toggle(owner, key.keyName),
                    tracked ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            }));
            return card;
        }

        /// <summary>
        /// Inline editor for simple values; complex ones (events, lists, structs) get a button that selects the key in
        /// the Inspector, so a card never grows huge.
        /// </summary>
        private static VisualElement CreateValueEditor(Key key)
        {
            var keyObject = new SerializedObject(key); // each key is its own ScriptableObject
            var valueProp = keyObject.FindProperty("value");
            if (valueProp == null)
            {
                var note = new Label("Value is assigned at runtime");
                note.AddToClassList("nd-bb-value-note");
                return note;
            }

            bool simple = valueProp.propertyType switch
            {
                SerializedPropertyType.Integer or SerializedPropertyType.Boolean or SerializedPropertyType.Float or
                SerializedPropertyType.String or SerializedPropertyType.Enum or SerializedPropertyType.ObjectReference or
                SerializedPropertyType.Color or SerializedPropertyType.Vector2 or SerializedPropertyType.Vector3 or
                SerializedPropertyType.Vector2Int or SerializedPropertyType.Vector3Int or SerializedPropertyType.LayerMask => true,
                _ => false
            };
            if (!simple || (valueProp.isArray && valueProp.propertyType != SerializedPropertyType.String))
            {
                var open = new Button(() => { Selection.activeObject = key; EditorGUIUtility.PingObject(key); })
                {
                    text = $"Edit {Graph.KeySlotScanner.FriendlyTypeName(key.GetValueType())} in Inspector…",
                    tooltip = "This value type is edited in the Inspector."
                };
                open.AddToClassList("nd-bb-value-button");
                return open;
            }

            var field = new PropertyField(valueProp, "");
            field.AddToClassList("nd-bb-value");
            field.Bind(keyObject);
            return field;
        }

        private void OnAddKeyClicked()
        {
            if (m_BTree.blackboard == null)
            {
                CreateAndAssignBlackboard();
                if (m_BTree.blackboard == null) return;
            }
            var window = EditorWindow.focusedWindow;
            Vector2 screen = window != null
                ? window.position.position + new Vector2(m_AddButton.worldBound.xMin, m_AddButton.worldBound.yMax)
                : GUIUtility.GUIToScreenPoint(Vector2.zero);
            ND_BTSearchPopup.Show(screen, "Add Key", BlackboardKeySearchProvider.BuildEntries(), data => AddKey((Type)data));
        }

        private void CreateAndAssignBlackboard()
        {
            var bb = ScriptableObject.CreateInstance<Blackboard>();
            bb.name = $"{m_BTree.name}_Blackboard";
            AssetDatabase.CreateAsset(bb, AssetDatabase.GenerateUniqueAssetPath($"{System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(m_BTree))}/{bb.name}.asset"));
            AssetDatabase.SaveAssets();
            m_treeSerializer.Update();
            m_treeSerializer.FindProperty("blackboard").objectReferenceValue = bb;
            m_treeSerializer.ApplyModifiedProperties();
            EditorUtility.SetDirty(m_BTree);
            PopulateView();
        }

        public void AddKey(Type keyType)
        {
            Undo.RecordObject(m_BTree.blackboard, "Add Key");
            var key = ScriptableObject.CreateInstance(keyType) as Key;
            key.name = keyType.Name;
            string baseName = $"New {Graph.KeySlotScanner.FriendlyTypeName(key.GetValueType())}";
            key.keyName = ObjectNames.GetUniqueName(m_BTree.blackboard.keys.Where(k => k != null).Select(k => k.keyName).ToArray(), baseName);
            AssetDatabase.AddObjectToAsset(key, m_BTree.blackboard);
            Undo.RegisterCreatedObjectUndo(key, "Add Key");
            m_BTree.blackboard.keys.Add(key);
            EditorUtility.SetDirty(m_BTree.blackboard);
            AssetDatabase.SaveAssetIfDirty(m_BTree.blackboard);
            m_Search.SetValueWithoutNotify(string.Empty);
            PopulateView();
        }

        private void RenameKey(Key key, string newName)
        {
            newName = newName?.Trim();
            if (string.IsNullOrEmpty(newName) || newName == key.keyName) { PopulateView(); return; }
            if (m_BTree.blackboard.keys.Any(k => k != null && k != key && k.keyName == newName))
            {
                EditorUtility.DisplayDialog("Duplicate key name", $"A key named '{newName}' already exists on this blackboard.", "OK");
                PopulateView();
                return;
            }
            Undo.RecordObject(key, "Rename Key");
            key.keyName = newName;
            EditorUtility.SetDirty(key);
            AssetDatabase.SaveAssetIfDirty(m_BTree.blackboard);
        }

        private void DeleteKey(Key key)
        {
            int uses = m_BTree.nodes.Where(n => n != null)
                .SelectMany(n => new[] { n }.Concat(n.decorators).Concat<Node>(n.services))
                .Where(n => n != null)
                .Sum(n => Graph.KeySlotScanner.Scan(n).Count(s => s.Current == key));
            if (uses > 0 && !EditorUtility.DisplayDialog("Delete key",
                    $"'{key.keyName}' is linked to {uses} node field(s) in '{m_BTree.name}'. Those fields will lose their key.", "Delete", "Cancel"))
                return;

            Undo.RecordObject(m_BTree.blackboard, "Delete Key");
            m_BTree.blackboard.keys.Remove(key);
            Undo.DestroyObjectImmediate(key);
            EditorUtility.SetDirty(m_BTree.blackboard);
            AssetDatabase.SaveAssetIfDirty(m_BTree.blackboard);
            PopulateView();
        }

        #endregion

        #region Drag a key onto node slots

        /// <summary>Starts a Unity drag of the Key asset (accepted by node key slots and Inspector object fields).</summary>
        private static void RegisterKeyDragSource(VisualElement handle, Key key)
        {
            Vector2 downPosition = default;
            bool pressed = false;
            handle.RegisterCallback<PointerDownEvent>(e => { if (e.button == 0) { pressed = true; downPosition = e.position; e.StopPropagation(); } });
            handle.RegisterCallback<PointerUpEvent>(e => pressed = false);
            handle.RegisterCallback<PointerLeaveEvent>(e =>
            {
                if (!pressed) return; // leaving the handle while pressed also starts the drag (fast drags)
                pressed = false;
                StartDrag(key);
            });
            handle.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!pressed || ((Vector2)e.position - downPosition).sqrMagnitude < 16f) return;
                pressed = false;
                StartDrag(key);
                e.StopPropagation();
            });
        }

        private static void StartDrag(Key key)
        {
            DragAndDrop.PrepareStartDrag();
            DragAndDrop.objectReferences = new UnityEngine.Object[] { key };
            DragAndDrop.StartDrag($"Key: {key.keyName}");
        }

        #endregion
    }
}
