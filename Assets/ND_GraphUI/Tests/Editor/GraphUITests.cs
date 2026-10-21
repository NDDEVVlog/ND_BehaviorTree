using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ND.GraphUI.Tests
{
    /// <summary>
    /// A second consumer of the graph UI with no behavior-tree types: left-to-right flow, nodes with several named
    /// outputs (like dialogue choices), and plain in-memory data. If this works, the API is not tree-shaped.
    /// </summary>
    internal class FlowNodeView : GraphNodeView
    {
        private readonly string m_Id;
        private readonly PortTheme m_Theme;

        public override string Id => m_Id;
        public override PortTheme PortTheme => m_Theme;
        public GraphPortView In { get; }

        public FlowNodeView(string id, PortTheme theme, params string[] outputs)
        {
            m_Id = id;
            m_Theme = theme;
            style.flexDirection = FlexDirection.Row;
            In = AddPort(new GraphPortView(this, PortDirection.Input, PortCapacity.Multi, PortOrientation.Horizontal));
            Add(In);
            foreach (var name in outputs)
                Add(AddPort(new GraphPortView(this, PortDirection.Output, PortCapacity.Single, PortOrientation.Horizontal, name)));
        }

        public GraphPortView Out(string name) => FindPort(PortDirection.Output, name);
    }

    internal class FlowCanvas : GraphCanvas<FlowNodeView, GraphEdgeView>
    {
        public readonly HashSet<(string from, string port, string to)> Links = new HashSet<(string, string, string)>();
        public readonly Dictionary<string, Vector2> Positions = new Dictionary<string, Vector2>();
        public readonly HashSet<string> RefuseTargets = new HashSet<string>();
        public readonly EdgeTheme EdgeTheme = new EdgeTheme();

        public FlowNodeView Add(FlowNodeView view)
        {
            RegisterNodeView(view);
            return view;
        }

        public void Remove(FlowNodeView view) => UnregisterNodeView(view);

        protected override GraphEdgeView CreateEdge(GraphPortView output) => new GraphEdgeView(output, EdgeTheme);

        public override bool CanConnect(GraphPortView output, GraphPortView input) =>
            base.CanConnect(output, input) && !RefuseTargets.Contains(input.ParentNodeView.Id);

        public override bool TryConnect(GraphPortView output, GraphPortView input)
        {
            if (!CanConnect(output, input)) return false;
            // Single-capacity output: a new link replaces the old one.
            if (output.Capacity == PortCapacity.Single)
                foreach (var old in new List<GraphEdgeView>(output.Connections))
                {
                    Links.Remove((output.ParentNodeView.Id, output.PortName, old.InputPort.ParentNodeView.Id));
                    RemoveEdgeView(old);
                }
            if (!Links.Add((output.ParentNodeView.Id, output.PortName, input.ParentNodeView.Id))) return false;
            ConnectViews(output, input, behindNodes: true);
            return true;
        }

        public override void CommitNodeMove(IReadOnlyList<GraphNodeView> movedViews)
        {
            foreach (var view in movedViews) Positions[view.Id] = view.ViewPosition;
        }
    }

    /// <summary>Port-less node: linked node-to-node (like dialogue entries addressed by id).</summary>
    internal class LinkNodeView : GraphNodeView
    {
        private readonly string m_Id;
        public override string Id => m_Id;
        public override PortTheme PortTheme { get; } = new PortTheme();
        protected override bool AllowsNodeLinks => true;

        public LinkNodeView(string id, Rect rect)
        {
            m_Id = id;
            SetPosition(rect.position);
            style.width = rect.width;
            style.height = rect.height;
        }
    }

    internal class LinkCanvas : GraphCanvas<LinkNodeView, GraphEdgeView>
    {
        public readonly HashSet<(string from, string to)> Links = new HashSet<(string, string)>();
        public bool AllowSelfLinks;

        public LinkNodeView Add(LinkNodeView view) { RegisterNodeView(view); return view; }
        public void Remove(LinkNodeView view) => UnregisterNodeView(view);

        protected override GraphEdgeView CreateEdge(GraphPortView output) => new GraphEdgeView(output, new EdgeTheme());
        protected override GraphEdgeView CreateNodeEdge(GraphNodeView from) => new GraphEdgeView(from, new EdgeTheme());

        public override bool CanConnectNodes(GraphNodeView from, GraphNodeView to) =>
            base.CanConnectNodes(from, to) && !Links.Contains((from.Id, to.Id));

        public override bool TryConnectNodes(GraphNodeView from, GraphNodeView to)
        {
            if (!CanConnectNodes(from, to) || !Links.Add((from.Id, to.Id))) return false;
            ConnectNodeViews(from, to);
            return true;
        }

        public void Unlink(LinkNodeView from, LinkNodeView to)
        {
            Links.Remove((from.Id, to.Id));
            var edge = FindNodeEdge(from, to);
            if (edge != null) RemoveEdgeView(edge);
        }

        public override bool TryConnect(GraphPortView output, GraphPortView input) => false;
        public override void CommitNodeMove(IReadOnlyList<GraphNodeView> movedViews) { }
    }

    public class GraphUINodeLinkTests
    {
        private LinkCanvas m_Canvas;
        private LinkNodeView m_A, m_B, m_C;

        [SetUp]
        public void SetUp()
        {
            m_Canvas = new LinkCanvas();
            m_A = m_Canvas.Add(new LinkNodeView("a", new Rect(0, 0, 100, 50)));
            m_B = m_Canvas.Add(new LinkNodeView("b", new Rect(300, 0, 100, 50)));
            m_C = m_Canvas.Add(new LinkNodeView("c", new Rect(0, 300, 100, 50)));
        }

        [Test]
        public void ExitPoint_CutsTheLineAtTheNodeBorder()
        {
            var rect = new Rect(0, 0, 100, 50);
            Assert.AreEqual(new Vector2(100, 25), GraphEdgeView.ExitPoint(rect, rect.center, Vector2.right));
            Assert.AreEqual(new Vector2(50, 0), GraphEdgeView.ExitPoint(rect, rect.center, Vector2.down)); // UI: -y is up
            // Diagonal towards a node far below-right: leaves through the bottom edge of a wide node.
            var exit = GraphEdgeView.ExitPoint(rect, rect.center, new Vector2(1, 1).normalized);
            Assert.AreEqual(50f, exit.y, 0.001f);
            Assert.AreEqual(75f, exit.x, 0.001f);
        }

        [Test]
        public void NodeLink_DrawsANodeAnchoredEdge_WithoutPorts()
        {
            Assert.IsTrue(m_Canvas.TryConnectNodes(m_A, m_B));
            var edge = m_Canvas.FindNodeEdge(m_A, m_B);
            Assert.IsNotNull(edge);
            Assert.IsTrue(edge.IsNodeAnchored);
            Assert.IsTrue(edge.IsComplete);
            Assert.AreSame(m_A, edge.FromNode);
            Assert.AreSame(m_B, edge.ToNode);
            Assert.IsNull(edge.OutputPort);
            Assert.AreEqual(0, m_A.Ports.Count);
        }

        [Test]
        public void OppositeLinks_GetSeparateLanes_AndTheSurvivorReturnsToCentre()
        {
            m_Canvas.TryConnectNodes(m_A, m_B);
            m_Canvas.TryConnectNodes(m_B, m_A);
            var ab = m_Canvas.FindNodeEdge(m_A, m_B);
            var ba = m_Canvas.FindNodeEdge(m_B, m_A);
            Assert.AreNotEqual(0f, ab.LaneOffset);
            Assert.AreEqual(ab.LaneOffset, ba.LaneOffset, "same offset to each edge's own left = opposite sides");

            m_Canvas.TryConnectNodes(m_A, m_C);
            Assert.AreEqual(0f, m_Canvas.FindNodeEdge(m_A, m_C).LaneOffset, "a one-way link stays centred");

            m_Canvas.Remove(m_B);
            Assert.IsNull(m_Canvas.FindNodeEdge(m_A, m_B));
            Assert.AreEqual(1, m_Canvas.Edges.Count, "both edges touching B are gone, A→C remains");
        }

        [Test]
        public void RemovingOneOfAPair_ResetsTheOtherLane()
        {
            m_Canvas.TryConnectNodes(m_A, m_B);
            m_Canvas.TryConnectNodes(m_B, m_A);
            m_Canvas.Unlink(m_B, m_A);
            Assert.AreEqual(0f, m_Canvas.FindNodeEdge(m_A, m_B).LaneOffset, "alone again: back to the centre line");
        }

        [Test]
        public void LinkCandidates_MarkOtherNodesTheCanvasAccepts()
        {
            m_Canvas.TryConnectNodes(m_A, m_B); // already linked: not a candidate again
            m_Canvas.SetNodeLinkCandidates(m_A);
            Assert.IsFalse(m_A.ClassListContains("nd-link-candidate"), "never the dragged node");
            Assert.IsFalse(m_B.ClassListContains("nd-link-candidate"));
            Assert.IsTrue(m_C.ClassListContains("nd-link-candidate"));
            m_Canvas.SetNodeLinkCandidates(null);
            Assert.IsFalse(m_C.ClassListContains("nd-link-candidate"));
        }

        [Test]
        public void LinkHandle_ExistsOnlyWhenTheNodeAllowsLinks()
        {
            m_A.EnsureLinkHandle();
            Assert.IsNotNull(m_A.LinkHandle);
            Assert.AreEqual(DisplayStyle.None, m_A.LinkHandle.style.display.value, "hidden until hovered");

            var flow = new FlowNodeView("f", new PortTheme());
            flow.EnsureLinkHandle();
            Assert.IsNull(flow.LinkHandle);
        }

        [Test]
        public void CanvasWithoutNodeLinks_RefusesWithAnError()
        {
            var flowCanvas = new FlowCanvas();
            var node = flowCanvas.Add(new FlowNodeView("f", new PortTheme()));
            LogAssert.Expect(LogType.Error, new Regex("does not support node links"));
            Assert.IsNull(flowCanvas.CreateNodeEdgeView(node));
        }
    }

    public class GraphUITests
    {
        private PortTheme m_Theme;
        private FlowCanvas m_Canvas;
        private FlowNodeView m_Greeting, m_Accept, m_Refuse;

        [SetUp]
        public void SetUp()
        {
            m_Theme = new PortTheme { portSize = 10f, shape = PortShape.Circle };
            m_Canvas = new FlowCanvas();
            m_Greeting = m_Canvas.Add(new FlowNodeView("greeting", m_Theme, "accept", "refuse"));
            m_Accept = m_Canvas.Add(new FlowNodeView("accept", m_Theme, "next"));
            m_Refuse = m_Canvas.Add(new FlowNodeView("refuse", m_Theme));
        }

        [Test]
        public void HorizontalPorts_FaceLeftAndRight_AndOverhangTheSides()
        {
            var output = m_Greeting.Out("accept");
            Assert.AreEqual(Vector2.right, output.Outward);
            Assert.AreEqual(Vector2.left, m_Accept.In.Outward);

            // A multi input is long along the node side, i.e. tall for a horizontal port.
            Assert.Greater(m_Accept.In.style.height.value.value, m_Accept.In.style.width.value.value);
            Assert.Less(m_Accept.In.style.marginLeft.value.value, 0f, "input overhangs the left side");
            Assert.Less(output.style.marginRight.value.value, 0f, "output overhangs the right side");
        }

        [Test]
        public void VerticalPorts_KeepTreeLayout()
        {
            var tree = new FlowCanvas();
            var node = tree.Add(new FlowNodeView("n", m_Theme));
            var down = new GraphPortView(node, PortDirection.Output, PortCapacity.Multi);
            Assert.AreEqual(PortOrientation.Vertical, down.Orientation);
            Assert.AreEqual(Vector2.up, down.Outward, "UI space: +y points down the screen");
            Assert.Greater(down.style.width.value.value, down.style.height.value.value);
        }

        [Test]
        public void NamedOutputs_AreFoundByName()
        {
            Assert.AreEqual("accept", m_Greeting.Out("accept").PortName);
            Assert.AreEqual("refuse", m_Greeting.Out("refuse").PortName);
            Assert.IsNull(m_Greeting.Out("missing"));
            Assert.AreEqual(3, m_Greeting.Ports.Count);
        }

        [Test]
        public void Connect_DrawsEdge_BetweenTheRightPorts()
        {
            Assert.IsTrue(m_Canvas.TryConnect(m_Greeting.Out("accept"), m_Accept.In));
            Assert.IsTrue(m_Canvas.TryConnect(m_Greeting.Out("refuse"), m_Refuse.In));

            Assert.AreEqual(2, m_Canvas.Edges.Count);
            var edge = m_Canvas.FindEdge(m_Greeting.Out("refuse"), m_Refuse.In);
            Assert.IsNotNull(edge);
            Assert.IsTrue(m_Greeting.Out("refuse").IsConnected);
            Assert.IsTrue(m_Refuse.In.IsConnected);
            Assert.IsFalse(m_Accept.In.Connections.Contains(edge));
            CollectionAssert.Contains(m_Canvas.Links, ("greeting", "refuse", "refuse"));
        }

        [Test]
        public void Connect_SingleOutput_ReplacesItsPreviousEdge_InDataAndViews()
        {
            var output = m_Greeting.Out("accept");
            m_Canvas.TryConnect(output, m_Accept.In);
            m_Canvas.TryConnect(output, m_Refuse.In);

            Assert.AreEqual(1, output.Connections.Count);
            Assert.IsFalse(m_Accept.In.IsConnected, "the old target's input is released");
            Assert.AreEqual(1, m_Canvas.Edges.Count);
            CollectionAssert.AreEquivalent(new[] { ("greeting", "accept", "refuse") }, m_Canvas.Links);
        }

        [Test]
        public void ConnectionCandidates_FollowCanConnect_AndClear()
        {
            m_Canvas.RefuseTargets.Add("refuse");
            m_Canvas.SetConnectionCandidates(m_Greeting.Out("accept"));

            Assert.IsTrue(IsCandidate(m_Accept.In), "another node's input can receive it");
            Assert.IsFalse(IsCandidate(m_Refuse.In), "CanConnect refused this target");
            Assert.IsFalse(IsCandidate(m_Greeting.In), "never the dragged node itself");
            Assert.IsFalse(IsCandidate(m_Accept.Out("next")), "never a port of the same direction");

            m_Canvas.SetConnectionCandidates(null);
            Assert.IsFalse(IsCandidate(m_Accept.In));
        }

        // Candidate ports are drawn 1.2x; the scale is the observable effect of SetCandidate (no hover in tests).
        private static bool IsCandidate(GraphPortView port) => port.style.scale.value.value.x > 1.1f;

        [Test]
        public void RemovingANode_RemovesItsEdges_AndReleasesTheOtherEnd()
        {
            m_Canvas.TryConnect(m_Greeting.Out("accept"), m_Accept.In);
            m_Canvas.TryConnect(m_Accept.Out("next"), m_Refuse.In);
            m_Canvas.AddToSelection(m_Accept);

            m_Canvas.Remove(m_Accept);

            Assert.AreEqual(0, m_Canvas.Edges.Count);
            Assert.IsFalse(m_Greeting.Out("accept").IsConnected);
            Assert.IsFalse(m_Refuse.In.IsConnected);
            Assert.IsNull(m_Canvas.GetNodeView("accept"));
            Assert.AreEqual(0, m_Canvas.SelectionCount);
            Assert.IsFalse(m_Canvas.ContentContainer.Contains(m_Accept));
        }

        [Test]
        public void Selection_ThroughBaseApi_MatchesTypedApi()
        {
            GraphCanvas canvas = m_Canvas;
            canvas.AddToSelection(m_Greeting);
            canvas.ToggleSelection(m_Accept);
            Assert.IsTrue(canvas.IsSelected(m_Greeting));
            CollectionAssert.AreEquivalent(new[] { m_Greeting, m_Accept }, m_Canvas.SelectedNodes);
            Assert.IsTrue(m_Accept.ClassListContains("selected"));

            canvas.ToggleSelection(m_Accept);
            Assert.IsFalse(m_Accept.ClassListContains("selected"));
            canvas.ClearSelection();
            Assert.AreEqual(0, canvas.SelectionCount);
            Assert.IsFalse(m_Greeting.ClassListContains("selected"));
        }

        [Test]
        public void Register_SameIdTwice_KeepsTheFirstView()
        {
            var duplicate = new FlowNodeView("greeting", m_Theme);
            m_Canvas.Add(duplicate);
            Assert.AreSame(m_Greeting, m_Canvas.GetNodeView("greeting"));
            Assert.IsFalse(m_Canvas.ContentContainer.Contains(duplicate));
        }

        [Test]
        public void CommitNodeMove_ReportsViewPositions()
        {
            m_Greeting.SetPosition(new Vector2(40, 60));
            m_Canvas.CommitNodeMove(new List<GraphNodeView> { m_Greeting });
            Assert.AreEqual(new Vector2(40, 60), m_Canvas.Positions["greeting"]);
        }

        [Test]
        public void OrientedPorts_AcceptEitherDragDirection()
        {
            Assert.IsTrue(GraphConnectionManager.TryGetOrientedPorts(m_Accept.In, m_Greeting.Out("accept"), out var output, out var input));
            Assert.AreSame(m_Greeting.Out("accept"), output);
            Assert.AreSame(m_Accept.In, input);
            Assert.IsFalse(GraphConnectionManager.TryGetOrientedPorts(m_Accept.In, m_Refuse.In, out _, out _));
            Assert.IsFalse(GraphConnectionManager.TryGetOrientedPorts(m_Accept.In, null, out _, out _));
        }

        [Test]
        public void PortPainter_Create_RejectsNonPainters()
        {
            LogAssert.Expect(LogType.Error, new Regex("not a concrete PortPainter"));
            Assert.IsNull(PortPainter.Create(typeof(string), "NotAPainter"));
        }

        [Test]
        public void SearchPopup_Level_ListsFoldersThenLeaves()
        {
            var rows = GraphSearchPopup.GetLevel(new[] { "Line/Say", "Line/Ask", "Choice", "Line/Sub/Deep" }, "Line");
            CollectionAssert.AreEqual(new[] { "Sub", "Ask", "Say" }, rows.ConvertAll(r => r.Label));
            Assert.IsTrue(rows[0].IsFolder);
        }
    }
}
