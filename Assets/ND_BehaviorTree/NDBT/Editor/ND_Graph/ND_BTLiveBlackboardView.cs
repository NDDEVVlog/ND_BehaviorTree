using System.Collections.Generic;
using ND_BehaviorTree.Editor.Graph;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND_BehaviorTree.Editor.CustomGraph
{
    /// <summary>
    /// Live Debug section: the watched agent's RUNTIME blackboard as a table (key, type, value), refreshed 10 times
    /// per second. Tracked keys (★, or click a row → Track) move to a "Tracked" box at the top with larger values.
    /// A value's text is rebuilt only when it changed (common types are compared without boxing), and a changed row
    /// flashes briefly so you can see what the tree just wrote.
    /// </summary>
    public class ND_BTLiveBlackboardView : VisualElement
    {
        private const long RefreshMs = 100;
        private const double FlashSeconds = 0.6;

        private sealed class Row
        {
            public Key key;
            public VisualElement root;
            public Label value;
            public Button star;
            public bool hasValue;
            public float f; public int i; public bool b; public Vector3 v; public Vector2 v2; public object o;
            public double changedAt = -1;
        }

        private readonly Label m_Title;
        private readonly TextField m_Filter;
        private readonly VisualElement m_TrackedBox;
        private readonly VisualElement m_TrackedRows;
        private readonly Label m_AllTitle;
        private readonly ScrollView m_Rows;
        private readonly Label m_Empty;
        private readonly List<Row> m_RowList = new List<Row>();
        private Blackboard m_Bound;
        private Blackboard m_Asset; // tracking is stored per blackboard asset
        private int m_BoundKeyCount = -1;

        public ND_BTLiveBlackboardView()
        {
            ND_BTStyles.Apply(this, ND_BTStyles.LiveBlackboard);
            AddToClassList("nd-livebb");

            var header = new VisualElement();
            header.AddToClassList("nd-livebb__header");
            m_Title = new Label("Blackboard");
            m_Title.AddToClassList("nd-livebb__title");
            header.Add(m_Title);
            m_Filter = new TextField { tooltip = "Filter keys by name" };
            m_Filter.AddToClassList("nd-livebb__filter");
            m_Filter.RegisterValueChangedCallback(_ => ApplyFilter());
            header.Add(m_Filter);
            Add(header);

            m_Empty = new Label("Watch an agent to see its blackboard.");
            m_Empty.AddToClassList("nd-livebb__empty");
            Add(m_Empty);

            m_TrackedBox = new VisualElement();
            m_TrackedBox.AddToClassList("nd-livebb__tracked");
            var trackedTitle = new Label("★ Tracked");
            trackedTitle.AddToClassList("nd-livebb__section");
            m_TrackedBox.Add(trackedTitle);
            m_TrackedRows = new VisualElement();
            m_TrackedBox.Add(m_TrackedRows);
            Add(m_TrackedBox);

            m_AllTitle = new Label("All keys  ·  click a row or ☆ to track");
            m_AllTitle.AddToClassList("nd-livebb__section");
            Add(m_AllTitle);

            m_Rows = new ScrollView();
            m_Rows.AddToClassList("nd-livebb__rows");
            Add(m_Rows);

            schedule.Execute(RefreshValues).Every(RefreshMs);
            RegisterCallback<AttachToPanelEvent>(_ => { LiveKeyTracking.Changed -= PlaceRows; LiveKeyTracking.Changed += PlaceRows; });
            RegisterCallback<DetachFromPanelEvent>(_ => LiveKeyTracking.Changed -= PlaceRows);
        }

        /// <summary>
        /// Shows <paramref name="runtime"/> (the agent's runtime clone); <paramref name="asset"/> is the blackboard asset
        /// it was cloned from (tracking is stored per asset). Null shows nothing.
        /// </summary>
        public void Bind(Blackboard runtime, Blackboard asset)
        {
            int keyCount = runtime != null ? runtime.keys.Count : -1;
            if (runtime == m_Bound && asset == m_Asset && keyCount == m_BoundKeyCount) return;
            m_Bound = runtime;
            m_Asset = asset;
            m_BoundKeyCount = keyCount;
            RebuildRows();
        }

        private void RebuildRows()
        {
            m_Rows.Clear();
            m_TrackedRows.Clear();
            m_RowList.Clear();
            bool bound = m_Bound != null;
            m_Empty.style.display = bound ? DisplayStyle.None : DisplayStyle.Flex;
            m_AllTitle.style.display = bound ? DisplayStyle.Flex : DisplayStyle.None;
            m_Title.text = bound ? $"Blackboard · {m_Bound.keys.Count} keys" : "Blackboard";
            if (!bound)
            {
                m_TrackedBox.style.display = DisplayStyle.None;
                return;
            }

            foreach (var key in m_Bound.keys)
            {
                if (key == null) continue;
                var row = new Row { key = key, root = new VisualElement() };
                row.root.AddToClassList("nd-livebb__row");

                row.star = new Button { tooltip = "Track: pin this key to the top" };
                row.star.AddToClassList("nd-livebb__star");
                var captured = row;
                row.star.clicked += () => LiveKeyTracking.Toggle(m_Asset, captured.key.keyName);

                var names = new VisualElement();
                names.AddToClassList("nd-livebb__names");
                var nameLabel = new Label(key.keyName);
                nameLabel.AddToClassList("nd-livebb__name");
                var typeLabel = new Label(KeySlotScanner.FriendlyTypeName(key.GetValueType()));
                typeLabel.AddToClassList("nd-livebb__type");
                names.Add(nameLabel);
                names.Add(typeLabel);

                row.value = new Label("—");
                row.value.AddToClassList("nd-livebb__value");
                row.root.Add(row.star);
                row.root.Add(names);
                row.root.Add(row.value);
                row.root.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button != 0 && evt.button != 1) return;
                    if (evt.target is Button) return; // the star toggles directly
                    ShowRowMenu(captured);
                    evt.StopPropagation();
                });
                m_RowList.Add(row);
            }
            PlaceRows();
            RefreshValues();
        }

        /// <summary>Puts each row in the Tracked box or the main list (rows are moved, never rebuilt).</summary>
        private void PlaceRows()
        {
            if (m_Bound == null) return;
            int tracked = 0;
            foreach (var row in m_RowList)
            {
                bool isTracked = LiveKeyTracking.IsTracked(m_Asset, row.key.keyName);
                var target = isTracked ? m_TrackedRows : m_Rows.contentContainer;
                if (row.root.parent != target) target.Add(row.root);
                row.root.EnableInClassList("nd-livebb__row--tracked", isTracked);
                row.star.text = isTracked ? "★" : "☆";
                row.star.tooltip = isTracked ? "Untrack" : "Track: pin this key to the top";
                if (isTracked) tracked++;
            }
            m_TrackedBox.style.display = tracked > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            ApplyFilter();
        }

        private void ShowRowMenu(Row row)
        {
            var menu = new GenericMenu();
            bool tracked = LiveKeyTracking.IsTracked(m_Asset, row.key.keyName);
            menu.AddItem(new GUIContent(tracked ? "Untrack Variable" : "Track Variable"), tracked, () => LiveKeyTracking.Toggle(m_Asset, row.key.keyName));
            menu.AddItem(new GUIContent("Copy Value"), false, () => EditorGUIUtility.systemCopyBuffer = row.value.text);
            menu.AddItem(new GUIContent("Copy Key Name"), false, () => EditorGUIUtility.systemCopyBuffer = row.key.keyName);
            menu.ShowAsContext();
        }

        private void ApplyFilter()
        {
            string filter = m_Filter.value;
            foreach (var row in m_RowList)
            {
                // Tracked keys always stay visible: that is the point of tracking them.
                bool show = string.IsNullOrEmpty(filter) || row.root.parent == m_TrackedRows
                            || row.key.keyName.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) >= 0;
                row.root.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void RefreshValues()
        {
            if (m_Bound == null || panel == null || resolvedStyle.display == DisplayStyle.None) return;
            double now = EditorApplication.timeSinceStartup;
            foreach (var row in m_RowList)
            {
                if (row.key == null) continue;
                if (UpdateValue(row)) row.changedAt = now;
                bool flashing = row.changedAt >= 0 && now - row.changedAt < FlashSeconds;
                row.root.EnableInClassList("nd-livebb__row--changed", flashing);
            }
        }

        /// <summary>Writes the value label if the value changed. Returns true on a change after the first read.</summary>
        private static bool UpdateValue(Row row)
        {
            bool first = !row.hasValue;
            string text;
            switch (row.key)
            {
                case Key<float> k:
                {
                    float x = k.GetValue();
                    if (!first && x == row.f) return false;
                    row.f = x; text = x.ToString("0.###");
                    break;
                }
                case Key<int> k:
                {
                    int x = k.GetValue();
                    if (!first && x == row.i) return false;
                    row.i = x; text = x.ToString();
                    break;
                }
                case Key<bool> k:
                {
                    bool x = k.GetValue();
                    if (!first && x == row.b) return false;
                    row.b = x; text = x ? "true" : "false";
                    row.value.EnableInClassList("nd-livebb__value--true", x);
                    row.value.EnableInClassList("nd-livebb__value--false", !x);
                    break;
                }
                case Key<Vector3> k:
                {
                    Vector3 x = k.GetValue();
                    if (!first && x == row.v) return false;
                    row.v = x; text = $"({x.x:0.0}, {x.y:0.0}, {x.z:0.0})";
                    break;
                }
                case Key<Vector2> k:
                {
                    Vector2 x = k.GetValue();
                    if (!first && x == row.v2) return false;
                    row.v2 = x; text = $"({x.x:0.0}, {x.y:0.0})";
                    break;
                }
                default:
                {
                    object x = row.key.GetValueObject();
                    if (!first && (ReferenceEquals(x, row.o) || (x != null && !(x is Object) && x.Equals(row.o)))) return false;
                    row.o = x; text = Format(x);
                    break;
                }
            }
            row.hasValue = true;
            row.value.text = text;
            row.value.tooltip = text;
            return !first;
        }

        private static string Format(object value)
        {
            if (value is Object unityObject) return unityObject != null ? $"{unityObject.name} ({unityObject.GetType().Name})" : "None";
            return value == null ? "null" : value.ToString();
        }
    }
}
