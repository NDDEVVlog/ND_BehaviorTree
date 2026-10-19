using System.Collections.Generic;
using System.Linq;
using ND_BehaviorTree.Editor.Graph;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND_BehaviorTree.Editor.CustomGraph
{
    /// <summary>
    /// Live Debug panel (Play mode): lists every BehaviorTreeRunner in the loaded scenes with its status and lets you
    /// choose which one the graph follows. Also shows where the watched agent is running, buttons to send its events
    /// and a live table of its runtime blackboard (<see cref="ND_BTLiveBlackboardView"/>).
    /// Refreshes twice per second; rows are rebuilt only when the set of runners changes, so clicks are never lost.
    /// </summary>
    public class ND_BTDebugPanel : VisualElement
    {
        private const long RefreshMs = 500;

        private readonly ND_BehaviorTreeEditorWindow m_Window;
        private readonly Label m_Hint;
        private readonly ScrollView m_RunnerList;
        private readonly Label m_ActivePath;
        private readonly VisualElement m_EventsSection;
        private readonly VisualElement m_EventButtons;
        private readonly ND_BTLiveBlackboardView m_Blackboard;

        private readonly List<BehaviorTreeRunner> m_ListedRunners = new List<BehaviorTreeRunner>();
        private readonly Dictionary<BehaviorTreeRunner, (VisualElement row, VisualElement dot, Label status)> m_Rows =
            new Dictionary<BehaviorTreeRunner, (VisualElement, VisualElement, Label)>();
        private BehaviorTreeRunner m_EventsBuiltFor;

        public ND_BTDebugPanel(ND_BehaviorTreeEditorWindow window)
        {
            m_Window = window;
            style.position = Position.Absolute;
            style.right = 8;
            style.top = 28;
            style.width = 300;
            style.maxHeight = Length.Percent(92);
            style.backgroundColor = new Color(0.14f, 0.14f, 0.14f, 0.96f);
            style.paddingLeft = style.paddingRight = style.paddingTop = style.paddingBottom = 6;
            style.borderTopLeftRadius = style.borderTopRightRadius = style.borderBottomLeftRadius = style.borderBottomRightRadius = 8;
            style.borderTopWidth = style.borderBottomWidth = style.borderLeftWidth = style.borderRightWidth = 1;
            style.borderTopColor = style.borderBottomColor = style.borderLeftColor = style.borderRightColor = new Color(0.08f, 0.08f, 0.08f, 1f);

            Add(new Label("Live Debug") { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 13, color = Color.white, marginBottom = 4 } });
            m_Hint = new Label { style = { whiteSpace = WhiteSpace.Normal, color = new Color(0.75f, 0.75f, 0.75f), marginBottom = 4 } };
            Add(m_Hint);

            Add(SectionTitle("Agents"));
            m_RunnerList = new ScrollView { style = { maxHeight = 160, flexShrink = 0 } };
            Add(m_RunnerList);

            Add(SectionTitle("Running now"));
            m_ActivePath = new Label("—") { style = { whiteSpace = WhiteSpace.Normal, color = new Color(1f, 0.8f, 0.2f), marginBottom = 4 } };
            Add(m_ActivePath);

            m_EventsSection = new VisualElement();
            m_EventsSection.Add(SectionTitle("Send event"));
            m_EventButtons = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } };
            m_EventsSection.Add(m_EventButtons);
            Add(m_EventsSection);

            m_Blackboard = new ND_BTLiveBlackboardView();
            Add(m_Blackboard);

            schedule.Execute(Refresh).Every(RefreshMs);
            Refresh();
        }

        private static Label SectionTitle(string text) =>
            new Label(text) { style = { unityFontStyleAndWeight = FontStyle.Bold, color = new Color(0.85f, 0.85f, 0.85f), marginTop = 6, marginBottom = 2 } };

        private static readonly Unity.Profiling.ProfilerMarker s_Marker = new Unity.Profiling.ProfilerMarker("ND_BehaviorTree.DebugPanel");

        private void Refresh()
        {
            if (resolvedStyle.display == DisplayStyle.None || panel == null) return; // closed: no scene scans
            using (s_Marker.Auto()) RefreshInternal();
        }

        private void RefreshInternal()
        {

            if (!Application.isPlaying)
            {
                m_Hint.text = "Enter Play Mode, then pick an agent to watch. The graph shows running nodes (yellow), recent results (green/red) and the active path.";
                m_Hint.style.display = DisplayStyle.Flex;
                ClearRunnerRows();
                m_ActivePath.text = "—";
                m_EventsSection.style.display = DisplayStyle.None;
                m_EventsBuiltFor = null;
                m_Blackboard.Bind(null);
                return;
            }

            var runners = Object.FindObjectsByType<BehaviorTreeRunner>(FindObjectsSortMode.None)
                .Where(r => r != null)
                .OrderBy(r => r.gameObject.name)
                .ToList();

            m_Hint.text = runners.Count == 0 ? "No BehaviorTreeRunner in the loaded scenes." : "Click Watch to follow an agent (turns on Lock).";
            if (!runners.SequenceEqual(m_ListedRunners)) RebuildRunnerRows(runners);

            var watched = m_Window.WatchedRunner;
            foreach (var kvp in m_Rows)
            {
                var runner = kvp.Key;
                var (row, dot, status) = kvp.Value;
                var tree = runner != null ? runner.RuntimeTree : null;
                string statusText = tree == null ? "not started" : !tree.IsInitialized ? "initializing"
                    : tree.LastStatus == Node.Status.Running ? "Running" : tree.LastStatus == Node.Status.Success ? "Success" : "Failure";
                if (status.text != statusText) status.text = statusText;
                dot.style.backgroundColor = tree == null ? Color.gray
                    : tree.LastStatus == Node.Status.Running ? new Color(1f, 0.8f, 0.2f)
                    : tree.LastStatus == Node.Status.Success ? new Color(0.35f, 0.9f, 0.35f)
                    : new Color(0.95f, 0.35f, 0.35f);
                row.style.backgroundColor = runner == watched ? new Color(0.25f, 0.4f, 0.65f, 0.6f) : StyleKeyword.Null;
            }

            string activePath = watched != null && watched.RuntimeTree != null
                ? LiveDebugUtility.DescribeActivePath(watched.RuntimeTree, Time.frameCount) ?? "(idle)"
                : "—";
            if (m_ActivePath.text != activePath) m_ActivePath.text = activePath;
            RefreshEvents(watched);
            m_Blackboard.Bind(watched != null && watched.RuntimeTree != null ? watched.RuntimeTree.blackboard : null);
        }

        private void ClearRunnerRows()
        {
            m_RunnerList.Clear();
            m_Rows.Clear();
            m_ListedRunners.Clear();
        }

        private void RebuildRunnerRows(List<BehaviorTreeRunner> runners)
        {
            ClearRunnerRows();
            m_ListedRunners.AddRange(runners);
            foreach (var runner in runners)
            {
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, paddingTop = 2, paddingBottom = 2, paddingLeft = 2, borderTopLeftRadius = 4, borderTopRightRadius = 4, borderBottomLeftRadius = 4, borderBottomRightRadius = 4 } };
                var dot = new VisualElement { style = { width = 8, height = 8, marginRight = 6, borderTopLeftRadius = 4, borderTopRightRadius = 4, borderBottomLeftRadius = 4, borderBottomRightRadius = 4 } };
                var names = new VisualElement { style = { flexGrow = 1, flexShrink = 1 } };
                names.Add(new Label(runner.gameObject.name) { style = { color = Color.white } });
                var status = new Label { style = { fontSize = 10, color = new Color(0.7f, 0.7f, 0.7f) } };
                var treeName = runner.treeAsset != null ? runner.treeAsset.name : "(no tree)";
                names.Add(new Label(treeName) { style = { fontSize = 10, color = new Color(0.6f, 0.6f, 0.6f) } });
                names.Add(status);

                var captured = runner;
                var watch = new Button(() => m_Window.WatchRunner(captured)) { text = "Watch", tooltip = "Follow this agent in the graph." };
                var ping = new Button(() => { Selection.activeGameObject = captured.gameObject; EditorGUIUtility.PingObject(captured.gameObject); }) { text = "Ping", tooltip = "Select the GameObject." };
                watch.SetEnabled(runner.treeAsset != null);

                row.Add(dot);
                row.Add(names);
                row.Add(watch);
                row.Add(ping);
                m_RunnerList.Add(row);
                m_Rows[runner] = (row, dot, status);
            }
        }

        private void RefreshEvents(BehaviorTreeRunner watched)
        {
            var events = watched != null && watched.treeAsset != null
                ? watched.treeAsset.nodes.OfType<EventNode>().Select(e => e.eventName).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToList()
                : new List<string>();
            m_EventsSection.style.display = events.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            if (watched == m_EventsBuiltFor && m_EventButtons.childCount == events.Count) return;

            m_EventsBuiltFor = watched;
            m_EventButtons.Clear();
            foreach (var eventName in events)
            {
                var captured = eventName;
                m_EventButtons.Add(new Button(() => watched.SendEvent(captured)) { text = captured, tooltip = $"SendEvent(\"{captured}\") on {watched.gameObject.name}" });
            }
        }
    }
}
