using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND_BehaviorTree.Editor.CustomGraph
{
    /// <summary>
    /// Searchable drop-down (UI Toolkit). Replaces GraphView's SearchWindow.
    /// Empty search: browse folders built from entry paths ("Action/Movement/Wander").
    ///   Click / Enter / → opens a folder; ← / Backspace / the back button goes up.
    /// Typing: flat list of every matching entry (all space-separated words must appear in the path).
    /// </summary>
    public sealed class ND_BTSearchPopup : EditorWindow
    {
        public readonly struct Entry
        {
            public readonly string Path;
            public readonly Texture2D Icon;
            public readonly object UserData;

            public Entry(string path, Texture2D icon, object userData)
            {
                Path = path;
                Icon = icon;
                UserData = userData;
            }
        }

        /// <summary>A row at one folder level: either a sub-folder or a leaf entry.</summary>
        public readonly struct LevelRow
        {
            public readonly string Label;
            public readonly bool IsFolder;
            /// <summary>Folder path for folders, full entry path for leaves.</summary>
            public readonly string Path;

            public LevelRow(string label, bool isFolder, string path)
            {
                Label = label;
                IsFolder = isFolder;
                Path = path;
            }
        }

        private struct Row
        {
            public string Label;
            public bool IsFolder;
            public string FolderPath;
            public int EntryIndex;
        }

        private List<Entry> m_AllEntries;
        private readonly List<Row> m_Rows = new List<Row>();
        private Action<object> m_OnSelect;
        private ListView m_ListView;
        private TextField m_Search;
        private Label m_Breadcrumb;
        private Button m_BackButton;
        private string m_Title;
        private string m_Folder = string.Empty;
        private bool m_Chosen;

        public static void Show(Vector2 screenPosition, string title, IEnumerable<Entry> entries, Action<object> onSelect)
        {
            var popup = CreateInstance<ND_BTSearchPopup>();
            popup.m_AllEntries = entries.ToList();
            popup.m_OnSelect = onSelect;
            popup.m_Title = title;
            popup.Build();
            popup.ShowAsDropDown(new Rect(screenPosition, Vector2.zero), new Vector2(320, 380));
        }

        /// <summary>Sub-folders (sorted) then leaves (sorted) directly inside <paramref name="folder"/> ("" = root).</summary>
        public static List<LevelRow> GetLevel(IEnumerable<string> paths, string folder)
        {
            string prefix = string.IsNullOrEmpty(folder) ? string.Empty : folder + "/";
            var folders = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            var leaves = new List<LevelRow>();

            foreach (var path in paths)
            {
                if (!path.StartsWith(prefix, StringComparison.Ordinal)) continue;
                string rest = path.Substring(prefix.Length);
                int slash = rest.IndexOf('/');
                if (slash >= 0) folders.Add(rest.Substring(0, slash));
                else leaves.Add(new LevelRow(rest, false, path));
            }

            var rows = folders.Select(f => new LevelRow(f, true, prefix + f)).ToList();
            rows.AddRange(leaves.OrderBy(l => l.Label, StringComparer.OrdinalIgnoreCase));
            return rows;
        }

        private void Build()
        {
            var root = rootVisualElement;
            root.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 1f);
            root.style.paddingLeft = root.style.paddingRight = root.style.paddingTop = root.style.paddingBottom = 4;

            var header = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 4 } };
            m_BackButton = new Button(GoUp) { text = "◀", tooltip = "Back (← / Backspace)", style = { width = 24 } };
            m_Breadcrumb = new Label { style = { unityFontStyleAndWeight = FontStyle.Bold, color = Color.white, flexGrow = 1, marginLeft = 4 } };
            header.Add(m_BackButton);
            header.Add(m_Breadcrumb);
            root.Add(header);

            m_Search = new TextField { name = "search-field" };
            m_Search.RegisterValueChangedCallback(evt => RebuildRows());
            m_Search.RegisterCallback<KeyDownEvent>(OnSearchKeyDown, TrickleDown.TrickleDown);
            root.Add(m_Search);

            m_ListView = new ListView(m_Rows, 20, MakeItem, BindItem)
            {
                selectionType = SelectionType.Single,
                style = { flexGrow = 1, marginTop = 4 }
            };
            m_ListView.itemsChosen += items => Choose(m_ListView.selectedIndex);
            root.Add(m_ListView);

            RebuildRows();
            m_Search.schedule.Execute(() => m_Search.Q("unity-text-input")?.Focus());
        }

        private VisualElement MakeItem()
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            row.Add(new Image { name = "icon", style = { width = 16, height = 16, marginRight = 4, flexShrink = 0 } });
            row.Add(new Label { name = "label", style = { flexGrow = 1 } });
            row.Add(new Label("›") { name = "arrow", style = { unityFontStyleAndWeight = FontStyle.Bold, marginRight = 4 } });
            row.RegisterCallback<ClickEvent>(evt => Choose((int)((VisualElement)evt.currentTarget).userData));
            return row;
        }

        private void BindItem(VisualElement element, int index)
        {
            var row = m_Rows[index];
            element.userData = index;
            element.Q<Image>("icon").image = row.IsFolder ? EditorGUIUtility.IconContent("Folder Icon").image : m_AllEntries[row.EntryIndex].Icon;
            element.Q<Label>("label").text = row.Label;
            element.Q<Label>("arrow").style.display = row.IsFolder ? DisplayStyle.Flex : DisplayStyle.None;
            element.tooltip = !row.IsFolder && m_AllEntries[row.EntryIndex].UserData is Type nodeType
                ? Graph.WeightGraph.DescriptionOf(nodeType)?.Summary ?? string.Empty
                : string.Empty;
        }

        private void RebuildRows()
        {
            m_Rows.Clear();
            string query = m_Search?.value ?? string.Empty;
            var tokens = query.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            bool searching = tokens.Length > 0;

            if (searching)
            {
                for (int i = 0; i < m_AllEntries.Count; i++)
                {
                    string path = m_AllEntries[i].Path;
                    if (tokens.All(t => path.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0))
                        m_Rows.Add(new Row { Label = path, EntryIndex = i });
                }
                m_Rows.Sort((a, b) => string.Compare(a.Label, b.Label, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                var indexByPath = new Dictionary<string, int>();
                for (int i = 0; i < m_AllEntries.Count; i++) indexByPath[m_AllEntries[i].Path] = i;

                foreach (var level in GetLevel(m_AllEntries.Select(e => e.Path), m_Folder))
                {
                    m_Rows.Add(level.IsFolder
                        ? new Row { Label = level.Label, IsFolder = true, FolderPath = level.Path }
                        : new Row { Label = level.Label, EntryIndex = indexByPath[level.Path] });
                }
            }

            m_Breadcrumb.text = searching ? $"{m_Title} — search" : (string.IsNullOrEmpty(m_Folder) ? m_Title : $"{m_Title} / {m_Folder}");
            m_BackButton.SetEnabled(!searching && !string.IsNullOrEmpty(m_Folder));

            m_ListView.RefreshItems();
            m_ListView.selectedIndex = m_Rows.Count > 0 ? 0 : -1;
            if (m_Rows.Count > 0) m_ListView.ScrollToItem(0);
        }

        private void GoUp()
        {
            if (string.IsNullOrEmpty(m_Folder)) return;
            int slash = m_Folder.LastIndexOf('/');
            m_Folder = slash < 0 ? string.Empty : m_Folder.Substring(0, slash);
            RebuildRows();
        }

        private void OnSearchKeyDown(KeyDownEvent evt)
        {
            bool searchEmpty = string.IsNullOrEmpty(m_Search.value);
            switch (evt.keyCode)
            {
                case KeyCode.DownArrow:
                    m_ListView.selectedIndex = Mathf.Min(m_ListView.selectedIndex + 1, m_Rows.Count - 1);
                    m_ListView.ScrollToItem(m_ListView.selectedIndex);
                    evt.StopPropagation();
                    break;
                case KeyCode.UpArrow:
                    m_ListView.selectedIndex = Mathf.Max(m_ListView.selectedIndex - 1, 0);
                    m_ListView.ScrollToItem(m_ListView.selectedIndex);
                    evt.StopPropagation();
                    break;
                case KeyCode.RightArrow:
                    if (searchEmpty && m_ListView.selectedIndex >= 0 && m_Rows[m_ListView.selectedIndex].IsFolder)
                    {
                        Choose(m_ListView.selectedIndex);
                        evt.StopPropagation();
                    }
                    break;
                case KeyCode.LeftArrow:
                case KeyCode.Backspace:
                    if (searchEmpty && !string.IsNullOrEmpty(m_Folder))
                    {
                        GoUp();
                        evt.StopPropagation();
                    }
                    break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    Choose(m_ListView.selectedIndex);
                    evt.StopPropagation();
                    break;
                case KeyCode.Escape:
                    Close();
                    evt.StopPropagation();
                    break;
            }
        }

        private void Choose(int index)
        {
            if (m_Chosen || index < 0 || index >= m_Rows.Count) return;
            var row = m_Rows[index];
            if (row.IsFolder)
            {
                m_Folder = row.FolderPath;
                RebuildRows();
                return;
            }

            m_Chosen = true;
            var data = m_AllEntries[row.EntryIndex].UserData;
            var callback = m_OnSelect;
            Close();
            callback?.Invoke(data);
        }
    }
}
