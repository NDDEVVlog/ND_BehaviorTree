using System;
using System.Collections.Generic;
using System.Linq;
using ND.GraphUI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND.Dialogue.Editor
{
    public enum NewEntryKind { NpcLine, PlayerResponse, Passthrough }

    /// <summary>
    /// The graph of one conversation. Entries are port-less nodes; links are node-anchored edges. Every data change
    /// is recorded with Undo on the database; after Undo/Redo the owner calls <see cref="Reload"/>.
    /// </summary>
    public class DialogueGraphCanvas : GraphCanvas<DialogueNodeView, DialogueEdgeView>
    {
        private static readonly Vector2 ChildOffset = new Vector2(280f, 0f);

        public DialogueDatabase Database { get; private set; }
        public Conversation Conversation { get; private set; }
        public string PreviewLocale { get; private set; }
        public EdgeTheme EdgeTheme { get; } = new EdgeTheme { edgeColor = new Color(0.75f, 0.78f, 0.85f, 1f), edgeThickness = 2.5f };

        /// <summary>Builds plugin args for menus and hooks (set by the window; null = minimal args).</summary>
        public Func<DialogueEntry, Vector2, Plugins.DialoguePluginArgs> PluginArgs { get; set; }

        private Plugins.DialoguePluginArgs ArgsFor(DialogueEntry entry, Vector2 position) =>
            PluginArgs?.Invoke(entry, position) ?? new Plugins.DialoguePluginArgs { Canvas = this, Database = Database, Conversation = Conversation, Entry = entry, Position = position, Locale = PreviewLocale };

        /// <summary>The selection changed (inspector shows the single selected entry).</summary>
        public event Action SelectionChanged;
        /// <summary>(conversation id, entry id) "Test from here" was picked on a node.</summary>
        public event Action<int, int> TestRequested;
        /// <summary>The database (or its text table) was changed from the canvas.</summary>
        public event Action DataChanged;

        public DialogueGraphCanvas()
        {
            var sheet = DialogueEditorStyles.Graph;
            if (sheet != null) styleSheets.Add(sheet);
        }

        #region Loading

        public void Load(DialogueDatabase database, Conversation conversation, string previewLocale)
        {
            Database = database;
            Conversation = conversation;
            PreviewLocale = previewLocale;
            Reload();
        }

        /// <summary>Redraws from data, keeping the selection (by entry id) and pan/zoom.</summary>
        public void Reload()
        {
            var selected = new HashSet<string>(SelectedNodes.Select(v => v.Id));
            ClearViews();
            if (Database == null || Conversation == null)
            {
                SelectionChanged?.Invoke();
                return;
            }
            // Re-fetch by id: SerializedObject edits (inspector fields, link popup) may have replaced the data objects.
            Conversation = Database.FindConversation(Conversation.id) ?? Conversation;
            Conversation.InvalidateCache();
            foreach (var entry in Conversation.entries)
                if (entry != null) RegisterNodeView(new DialogueNodeView(Database, Conversation, entry, PreviewLocale));
            foreach (var entry in Conversation.entries)
            {
                if (entry == null) continue;
                foreach (var link in entry.links)
                    if (link.conversationId == Conversation.id) DrawLink(entry, link);
            }
            foreach (var id in selected)
            {
                var view = GetNodeView(id);
                if (view != null) AddToSelection(view);
            }
            SelectionChanged?.Invoke();
        }

        public void SetPreviewLocale(string locale)
        {
            PreviewLocale = locale;
            foreach (var view in Nodes) view.Refresh(locale);
        }

        /// <summary>
        /// Redraws one entry (and its outgoing links) after an edit, re-fetching it by id. Does not touch the selection,
        /// so an inspector field being typed in keeps its focus.
        /// </summary>
        public void RefreshNode(DialogueEntry entry)
        {
            if (Database == null || Conversation == null || entry == null) return;
            Conversation = Database.FindConversation(Conversation.id) ?? Conversation;
            var fresh = Conversation.FindEntry(entry.id);
            var view = GetNodeView(entry.id.ToString());
            if (fresh == null || view == null) return;
            view.Rebind(fresh);
            view.Refresh(PreviewLocale);
            foreach (var edge in Edges.Where(e => e.FromNode == view).ToList()) RemoveEdgeView(edge);
            foreach (var link in fresh.links)
                if (link.conversationId == Conversation.id) DrawLink(fresh, link);
            // Edges into this entry show its entry conditions too.
            foreach (var edge in Edges.Where(e => e.ToNode == view)) edge.RefreshLabel();
        }

        /// <summary>Redraws every entry and edge from data, keeping selection and focus (after edits made elsewhere).</summary>
        public void RefreshAll()
        {
            if (Database == null || Conversation == null) return;
            Conversation = Database.FindConversation(Conversation.id) ?? Conversation;
            foreach (var entry in Conversation.entries.Where(e => e != null).ToList()) RefreshNode(entry);
        }

        private DialogueEdgeView DrawLink(DialogueEntry from, DialogueLink link)
        {
            var fromView = GetNodeView(from.id.ToString());
            var toView = GetNodeView(link.entryId.ToString());
            if (fromView == null || toView == null) return null; // dangling link: Validate reports it
            var edge = ConnectNodeViews(fromView, toView);
            if (edge == null) return null;
            edge.Link = link;
            edge.RefreshLabel();
            return edge;
        }

        protected override DialogueEdgeView CreateEdge(GraphPortView output)
        {
            Debug.LogError("[Dialogue Editor] Dialogue nodes have no ports.");
            return null;
        }

        protected override DialogueEdgeView CreateNodeEdge(GraphNodeView from)
        {
            var edge = new DialogueEdgeView(from, EdgeTheme);
            edge.LabelClicked += e => OpenLinkEditor(e);
            return edge;
        }

        /// <summary>Opens the link editor (priority + conditions) for an edge, at the mouse.</summary>
        public LinkEditorPopup OpenLinkEditor(DialogueEdgeView edge)
        {
            if (edge?.Link == null || !(edge.FromNode is DialogueNodeView from) || !(edge.ToNode is DialogueNodeView to)) return null;
            Vector2 screen = HostWindow != null ? HostWindow.position.position + MousePanelPosition : GUIUtility.GUIToScreenPoint(MousePanelPosition);
            string title = $"#{from.Entry.id} {Describe(from.Entry)}  →  #{to.Entry.id} {Describe(to.Entry)}";
            return LinkEditorPopup.Open(screen, Database, Conversation, from.Entry, edge.Link, title, () =>
            {
                EditorUtility.SetDirty(Database);
                RefreshNode(to.Entry);   // its entry conditions may have changed (chips, incoming edges)
                RefreshNode(from.Entry); // redraws this node's edges with their new labels
                DataChanged?.Invoke();
            });
        }

        /// <summary>No ports in a dialogue graph: links are made node to node (<see cref="TryConnectNodes"/>).</summary>
        public override bool TryConnect(GraphPortView output, GraphPortView input) => false;

        #endregion

        #region Selection (raises SelectionChanged)

        public override void AddToSelection(GraphNodeView view) { base.AddToSelection(view); SelectionChanged?.Invoke(); }
        public override void ToggleSelection(GraphNodeView view) { base.ToggleSelection(view); SelectionChanged?.Invoke(); }
        public override void ClearSelection() { base.ClearSelection(); SelectionChanged?.Invoke(); }

        public DialogueEntry SingleSelectedEntry => SelectedNodes.Count == 1 ? SelectedNodes[0].Entry : null;

        #endregion

        #region Data operations

        private void Record(string label)
        {
            Undo.RecordObject(Database, label);
        }

        private void Changed()
        {
            EditorUtility.SetDirty(Database);
            DataChanged?.Invoke();
        }

        public override bool CanConnectNodes(GraphNodeView from, GraphNodeView to)
        {
            if (!base.CanConnectNodes(from, to)) return false;
            if (!(from is DialogueNodeView source) || !(to is DialogueNodeView target)) return false;
            if (target.IsStart) return false;
            return !source.Entry.links.Any(l => l.conversationId == Conversation.id && l.entryId == target.Entry.id);
        }

        public override bool TryConnectNodes(GraphNodeView from, GraphNodeView to)
        {
            if (!CanConnectNodes(from, to))
            {
                ShowNotification("These entries cannot be linked (START cannot be a target, and a link may exist only once).");
                return false;
            }
            var source = (DialogueNodeView)from;
            var target = (DialogueNodeView)to;
            Record("Link Dialogue Entries");
            var link = Conversation.Link(source.Entry, target.Entry);
            Changed();
            DrawLink(source.Entry, link);
            source.Refresh(PreviewLocale);
            return true;
        }

        public void RemoveLink(DialogueEdgeView edge)
        {
            if (edge?.Link == null || !(edge.FromNode is DialogueNodeView source)) return;
            Record("Remove Dialogue Link");
            source.Entry.links.Remove(edge.Link);
            Changed();
            RemoveEdgeView(edge);
        }

        public void SetLinkPriority(DialogueEdgeView edge, LinkPriority priority)
        {
            if (edge?.Link == null) return;
            Record("Change Link Priority");
            edge.Link.priority = priority;
            Changed();
            edge.RefreshLabel();
        }

        /// <summary>Alt+click on an edge removes that link; double-click opens its editor.</summary>
        protected override bool OnEdgeClicked(GraphEdgeView edge, Vector2 contentPosition, bool cut)
        {
            if (!(edge is DialogueEdgeView dialogueEdge)) return false;
            if (cut) RemoveLink(dialogueEdge);
            else OpenLinkEditor(dialogueEdge); // double-click: edit its conditions
            return true;
        }

        public override void CommitNodeMove(IReadOnlyList<GraphNodeView> movedViews)
        {
            Record("Move Dialogue Entries");
            foreach (var view in movedViews)
                if (view is DialogueNodeView node) node.Entry.position = node.ViewPosition;
            Changed();
        }

        /// <summary>The actor a new entry of this kind speaks with: the first player / non-player actor. Null (logged) if there is none.</summary>
        public Actor DefaultActorFor(NewEntryKind kind)
        {
            bool wantPlayer = kind == NewEntryKind.PlayerResponse;
            var actor = Database.AllActors.FirstOrDefault(a => a != null && a.isPlayer == wantPlayer)
                        ?? (kind == NewEntryKind.Passthrough ? Database.AllActors.FirstOrDefault(a => a != null) : null);
            if (actor == null)
                ShowNotification(wantPlayer ? "Add an actor with 'Is Player' to the database first." : "Add a non-player actor to the database first.");
            return actor;
        }

        /// <summary>Creates an entry at <paramref name="position"/>, optionally linked from <paramref name="linkFrom"/> (one undo step).</summary>
        public DialogueEntry CreateEntry(NewEntryKind kind, Vector2 position, DialogueEntry linkFrom = null, Type entryType = null)
        {
            if (Conversation == null) return null;
            var actor = DefaultActorFor(kind);
            if (actor == null) return null;

            Record("Create Dialogue Entry");
            var entry = Conversation.AddEntry(entryType ?? typeof(DialogueEntry), actor.id);
            if (entry == null) return null; // not a usable entry type (logged)
            entry.position = position;
            entry.isPassthrough = kind == NewEntryKind.Passthrough;
            if (linkFrom != null) Conversation.Link(linkFrom, entry);
            Plugins.DialoguePluginRegistry.RaiseEntryCreated(ArgsFor(entry, position), entry); // same undo step
            Changed();

            var view = new DialogueNodeView(Database, Conversation, entry, PreviewLocale);
            RegisterNodeView(view);
            if (linkFrom != null)
            {
                DrawLink(linkFrom, linkFrom.links[linkFrom.links.Count - 1]);
                GetNodeView(linkFrom.id.ToString())?.Refresh(PreviewLocale);
            }
            base.ClearSelection();
            AddToSelection((GraphNodeView)view); // the GraphNodeView overload raises SelectionChanged
            return entry;
        }

        /// <summary>Concrete <see cref="DialogueEntry"/> subclasses with a parameterless constructor (for the "New …" menus).</summary>
        public static List<Type> EntryTypes() =>
            TypeCache.GetTypesDerivedFrom<DialogueEntry>()
                .Where(t => !t.IsAbstract && !t.IsGenericType && t.IsSerializable && t.GetConstructor(Type.EmptyTypes) != null)
                .OrderBy(t => t.Name).ToList();

        /// <summary>Creates a linked child of the single selected entry, to its right.</summary>
        public DialogueEntry CreateChildOfSelection(NewEntryKind kind)
        {
            var parent = SingleSelectedEntry;
            if (parent == null) return CreateEntry(kind, MouseContentPosition);
            var below = parent.links.Count(l => l.conversationId == Conversation.id);
            return CreateEntry(kind, parent.position + ChildOffset + new Vector2(0f, 120f * below), parent);
        }

        /// <summary>Deletes the selected entries (never START) and every link to them, in all conversations.</summary>
        public void DeleteSelection()
        {
            var doomed = SelectedNodes.Where(v => !v.IsStart).Select(v => v.Entry.id).ToList();
            if (doomed.Count == 0)
            {
                if (SelectedNodes.Count > 0) ShowNotification("START cannot be deleted.");
                return;
            }
            Record("Delete Dialogue Entries");
            foreach (var id in doomed) Database.RemoveEntry(Conversation.id, id);
            Changed();
            base.ClearSelection();
            Reload();
        }

        #endregion

        #region Input

        protected override bool HandleShortcut(KeyDownEvent evt)
        {
            bool ctrl = evt.actionKey;
            switch (evt.keyCode)
            {
                case KeyCode.Delete:
                case KeyCode.Backspace when !ctrl:
                    DeleteSelection();
                    return true;
                case KeyCode.Space when !ctrl:
                case KeyCode.Tab when !ctrl:
                    CreateChildOfSelection(NewEntryKind.NpcLine);
                    return true;
                case KeyCode.R when !ctrl:
                    CreateChildOfSelection(NewEntryKind.PlayerResponse);
                    return true;
                default:
                    return base.HandleShortcut(evt);
            }
        }

        protected override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            if (Conversation == null) return;
            Vector2 local = ContentContainer.WorldToLocal(evt.mousePosition);
            var node = (evt.target as VisualElement)?.GetFirstAncestorOfType<DialogueNodeView>() ?? evt.target as DialogueNodeView;

            if (node == null)
            {
                evt.menu.AppendAction("New NPC Line", _ => CreateEntry(NewEntryKind.NpcLine, local));
                evt.menu.AppendAction("New Player Response", _ => CreateEntry(NewEntryKind.PlayerResponse, local));
                evt.menu.AppendAction("New Passthrough (routing)", _ => CreateEntry(NewEntryKind.Passthrough, local));
                foreach (var type in EntryTypes())
                {
                    var t = type;
                    string label = UnityEditor.ObjectNames.NicifyVariableName(t.Name);
                    evt.menu.AppendAction($"New {label}/NPC Line", _ => CreateEntry(NewEntryKind.NpcLine, local, null, t));
                    evt.menu.AppendAction($"New {label}/Player Response", _ => CreateEntry(NewEntryKind.PlayerResponse, local, null, t));
                }
                evt.menu.AppendSeparator();
                evt.menu.AppendAction("Frame All   (F)", _ => FrameDefault());
                Plugins.DialoguePluginRegistry.AppendTo(evt.menu, Plugins.DialoguePluginRegistry.CanvasMenuItems, () => ArgsFor(null, local));
                return;
            }

            if (!IsSelected(node))
            {
                base.ClearSelection();
                AddToSelection((GraphNodeView)node);
            }
            var entry = node.Entry;
            evt.menu.AppendAction("Add Child/NPC Line   (Space)", _ => CreateChildOfSelection(NewEntryKind.NpcLine));
            evt.menu.AppendAction("Add Child/Player Response   (R)", _ => CreateChildOfSelection(NewEntryKind.PlayerResponse));
            evt.menu.AppendAction("Add Child/Passthrough", _ => CreateChildOfSelection(NewEntryKind.Passthrough));
            int testId = entry.id;
            evt.menu.AppendAction("Test from here   (Test Dialogue)", _ => TestRequested?.Invoke(Conversation.id, testId), _ => TestRequested != null ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            evt.menu.AppendAction($"Copy entry id   (#{testId})", _ => EditorGUIUtility.systemCopyBuffer = testId.ToString());

            foreach (var target in Nodes.OrderBy(v => v.Entry.id))
            {
                if (target == node) continue;
                var t = target;
                var status = CanConnectNodes(node, t) ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled;
                evt.menu.AppendAction($"Link To…/#{t.Entry.id}  {Describe(t.Entry)}", _ => TryConnectNodes(node, t), status);
            }

            var outgoing = Edges.Where(e => e.FromNode == node).ToList();
            foreach (var edge in outgoing)
            {
                var e = edge;
                string to = e.ToNode is DialogueNodeView tv ? $"#{tv.Entry.id}" : "?";
                foreach (LinkPriority p in Enum.GetValues(typeof(LinkPriority)))
                {
                    var priority = p;
                    evt.menu.AppendAction($"Link Priority/→ {to}/{priority}", _ => SetLinkPriority(e, priority),
                        e.Link.priority == priority ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
                }
                evt.menu.AppendAction($"Link Conditions/→ {to}…", _ => OpenLinkEditor(e));
                evt.menu.AppendAction($"Unlink/→ {to}", _ => RemoveLink(e));
            }

            Plugins.DialoguePluginRegistry.AppendTo(evt.menu, Plugins.DialoguePluginRegistry.EntryMenuItems, () => ArgsFor(entry, local));
            evt.menu.AppendSeparator();
            evt.menu.AppendAction("Delete   (Del)", _ => DeleteSelection(),
                node.IsStart ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);
            evt.StopPropagation();
        }

        /// <summary>Short label for menus: title, else the preview text.</summary>
        public string Describe(DialogueEntry entry)
        {
            if (entry.id == Conversation.StartEntryId) return "START";
            if (!string.IsNullOrEmpty(entry.title)) return entry.title;
            string text = DialogueTextEditing.Read(entry.text, PreviewLocale);
            if (string.IsNullOrEmpty(text)) return entry.isPassthrough ? "(routing)" : "(no text)";
            return text.Length > 40 ? text.Substring(0, 39).Replace('/', '∕') + "…" : text.Replace('/', '∕');
        }

        #endregion
    }
}
