using System.Collections.Generic;
using System.Linq;
using ND_BehaviorTree.Editor.Graph;
using ND_BehaviorTree.Story.Editor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ND_BehaviorTree.Story.Tests
{
    [NodeInfo("Counting Action (story test)", "Test/StoryCountingAction", true, false)]
    public class StoryCountingAction : ActionNode
    {
        public Status result = Status.Success;
        public int processCount;
        protected override Status OnProcess()
        {
            processCount++;
            return result;
        }
    }

    public class StoryNodesTests
    {
        private BehaviorTree m_Tree;
        private BehaviorTreeGraphService m_Graph;
        private RootNode m_Root;
        private readonly List<Object> m_Track = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            m_Tree = Track(ScriptableObject.CreateInstance<BehaviorTree>());
            m_Tree.name = "StoryTestTree";
            m_Root = ScriptableObject.CreateInstance<RootNode>();
            m_Root.name = "Root";
            m_Tree.nodes.Add(m_Root);
            m_Tree.rootNode = m_Root;
            m_Graph = new BehaviorTreeGraphService(m_Tree);
            m_Graph.History.Capacity = 100;
        }

        [TearDown]
        public void TearDown()
        {
            m_Graph.History.Clear();
            foreach (var n in m_Tree.nodes) if (n != null) m_Track.Add(n);
            foreach (var o in m_Track) if (o != null) Object.DestroyImmediate(o);
            m_Track.Clear();
        }

        private T Track<T>(T o) where T : Object { m_Track.Add(o); return o; }

        private T Create<T>(float x, float y = 0f) where T : Node => (T)m_Graph.CreateNode(typeof(T), new Vector2(x, y));

        private StorySwitchNode SyncedSwitch(params string[] cases)
        {
            var sw = Create<StorySwitchNode>(0, 100);
            m_Graph.Connect(m_Root, sw);
            sw.cases = cases.ToList();
            StorySwitchCases.Sync(m_Graph, sw);
            return sw;
        }

        private static string[] Labels(StorySwitchNode sw) => sw.children.Cast<StoryCaseNode>().Select(c => c.Label).ToArray();

        private BehaviorTree Clone()
        {
            var clone = Track(m_Tree.Clone());
            m_Track.AddRange(clone.nodes);
            if (clone.blackboard != null) { m_Track.Add(clone.blackboard); m_Track.AddRange(clone.blackboard.keys); }
            return clone;
        }

        private static T Runtime<T>(BehaviorTree clone, Node original) where T : Node => (T)clone.nodes.Single(n => n.id == original.id);

        // ---------- Switch case generation ----------

        [Test]
        public void Sync_CreatesOneCasePerEntryInOrder_PlusDefault()
        {
            var sw = SyncedSwitch("Calm", "Angry", "Calm", " ");

            CollectionAssert.AreEqual(new[] { "Calm", "Angry", "Default" }, Labels(sw), "duplicates and blanks are skipped");
            Assert.IsTrue(StorySwitchCases.InSync(sw));
            Assert.IsFalse(StorySwitchCases.Sync(m_Graph, sw), "already in sync: no change");
        }

        [Test]
        public void Sync_RenamedCase_KeepsItsBranch()
        {
            var sw = SyncedSwitch("A", "B");
            var caseB = (StoryCaseNode)sw.children[1];
            var branch = Create<StoryCountingAction>(0, 400);
            m_Graph.Connect(caseB, branch);

            sw.cases = new List<string> { "A", "Bee" };
            StorySwitchCases.Sync(m_Graph, sw);

            CollectionAssert.AreEqual(new[] { "A", "Bee", "Default" }, Labels(sw));
            Assert.AreSame(caseB, sw.children[1], "the renamed case is the same node");
            Assert.AreSame(branch, caseB.child);
        }

        [Test]
        public void Sync_RemovedCase_DeletesCaseOnly_AndUndoRestoresBoth()
        {
            var sw = SyncedSwitch("A", "B");
            var caseA = (StoryCaseNode)sw.children[0];
            var branch = Create<StoryCountingAction>(0, 400);
            m_Graph.Connect(caseA, branch);

            using (new GraphUndoScope(m_Tree, "Edit Switch")) // what the properties window does around an edit
            {
                sw.cases = new List<string> { "B" };
                StorySwitchCases.Sync(m_Graph, sw);
            }

            CollectionAssert.AreEqual(new[] { "B", "Default" }, Labels(sw));
            Assert.IsFalse(m_Tree.nodes.Contains(caseA));
            Assert.IsTrue(m_Tree.nodes.Contains(branch), "the branch stays on the graph, unconnected");

            Assert.IsTrue(m_Graph.History.Undo());
            CollectionAssert.AreEqual(new[] { "A", "B" }, sw.cases, "the list edit and its cases undo together");
            CollectionAssert.AreEqual(new[] { "A", "B", "Default" }, Labels(sw));
            Assert.AreSame(branch, ((StoryCaseNode)sw.children[0]).child);
        }

        [Test]
        public void AddCase_CreatesUniquelyNamedCase_OneUndoStep()
        {
            var sw = SyncedSwitch("A", "Case 3");

            string added = StorySwitchCases.AddCase(m_Graph, sw);

            Assert.AreEqual("Case 4", added, "skips names already in the list");
            CollectionAssert.AreEqual(new[] { "A", "Case 3", "Case 4", "Default" }, Labels(sw));

            Assert.IsTrue(m_Graph.History.Undo());
            CollectionAssert.AreEqual(new[] { "A", "Case 3" }, sw.cases);
            CollectionAssert.AreEqual(new[] { "A", "Case 3", "Default" }, Labels(sw));
        }

        [Test]
        public void DuplicateOrEmptyEntries_AreReported()
        {
            var sw = SyncedSwitch("A", "B");
            sw.cases = new List<string> { "A", "B", "b", "" }; // what the list's "+" (copy of the last entry) and a blank add look like
            StorySwitchCases.Sync(m_Graph, sw);

            CollectionAssert.AreEqual(new[] { "b", "(empty)" }, StorySwitchCases.IgnoredEntries(sw), "ignoreCase: 'b' repeats 'B'");
            Assert.IsTrue(GraphValidator.Validate(m_Tree).Any(i => i.Code == StoryNodesPlugin.CaseIgnored && i.Node == sw));
        }

        [Test]
        public void Sync_ReorderedList_ReordersCases()
        {
            var sw = SyncedSwitch("A", "B", "C");
            var before = sw.children.ToList();

            sw.cases = new List<string> { "C", "A", "B" };
            StorySwitchCases.Sync(m_Graph, sw);

            CollectionAssert.AreEqual(new[] { "C", "A", "B", "Default" }, Labels(sw));
            CollectionAssert.AreEquivalent(before, sw.children, "no case was recreated");
        }

        [Test]
        public void OwnedChildren_UserConnectRejected_AndMovingCasesKeepsListOrder()
        {
            var sw = SyncedSwitch("A", "B");
            var extra = Create<StoryCaseNode>(300, 250);
            Assert.IsFalse(m_Graph.CanConnect(sw, extra, out _), "a user cannot wire nodes into a Switch");

            // Dragging case A far right must not re-sort the Switch's children by X.
            var caseA = (StoryCaseNode)sw.children[0];
            caseA.position = new Rect(2000, 250, 100, 40);
            ExecutionOrder.SortChildren(sw);
            CollectionAssert.AreEqual(new[] { "A", "B", "Default" }, Labels(sw));
        }

        [Test]
        public void DeletingSwitch_DeletesItsCases()
        {
            var sw = SyncedSwitch("A");
            var cases = sw.children.ToList();

            m_Graph.DeleteNodes(new Node[] { sw });

            Assert.IsFalse(m_Tree.nodes.Intersect(cases).Any());
        }

        [Test]
        public void Plugin_IsRegistered_WithThemeAndValidationRule()
        {
            var entry = ND_BehaviorTree.Editor.Plugins.ND_BTPluginRegistry.Entries.Single(e => e.Plugin is StoryNodesPlugin);
            Assert.IsTrue(entry.Installed, entry.Error);
            Assert.IsTrue(ND_BehaviorTree.Editor.Theme.ND_BTThemeCatalog.All().Any(e => e.Name == StoryTheme.Name));

            var sw = SyncedSwitch("A");
            Assert.IsFalse(GraphValidator.Validate(m_Tree).Any(i => i.Code == StoryNodesPlugin.CaseOutOfSync));
            sw.cases.Add("B"); // changed without the editor hooks (e.g. by a script)
            Assert.IsTrue(GraphValidator.Validate(m_Tree).Any(i => i.Code == StoryNodesPlugin.CaseOutOfSync && i.Node == sw));
        }

        // ---------- Runtime ----------

        [TestCase("B", 0, 1, 0)]
        [TestCase("b", 0, 1, 0)]   // ignoreCase
        [TestCase("Zzz", 0, 0, 1)] // no match → Default
        public void Switch_RunsTheMatchingBranch(string value, int expectA, int expectB, int expectDefault)
        {
            var key = Track(ScriptableObject.CreateInstance<StringKey>());
            key.keyName = key.name = "Mood";
            var bb = Track(ScriptableObject.CreateInstance<Blackboard>());
            bb.keys.Add(key);
            m_Tree.blackboard = bb;

            var sw = SyncedSwitch("A", "B");
            sw.valueKey = key;
            var actions = new StoryCountingAction[3];
            for (int i = 0; i < 3; i++)
            {
                actions[i] = Create<StoryCountingAction>(i * 200, 400);
                m_Graph.Connect(sw.children[i], actions[i]);
            }

            var clone = Clone();
            clone.blackboard.SetValue("Mood", value);
            clone.Update();

            Assert.AreEqual(expectA, Runtime<StoryCountingAction>(clone, actions[0]).processCount);
            Assert.AreEqual(expectB, Runtime<StoryCountingAction>(clone, actions[1]).processCount);
            Assert.AreEqual(expectDefault, Runtime<StoryCountingAction>(clone, actions[2]).processCount);
        }

        [Test]
        public void SetText_ThenSwitch_RunsTheBranchForTheTextJustWritten()
        {
            var key = Track(ScriptableObject.CreateInstance<StringKey>());
            key.keyName = key.name = "Mood";
            var bb = Track(ScriptableObject.CreateInstance<Blackboard>());
            bb.keys.Add(key);
            m_Tree.blackboard = bb;

            var sequence = Create<SequenceNode>(0, 100);
            m_Graph.Connect(m_Root, sequence);
            var block = Create<StoryBlockNode>(0, 200);
            m_Graph.Connect(sequence, block);
            var set = Create<SetTextNode>(0, 250);
            set.variableKey = key;
            set.text = "Tired";
            m_Graph.Connect(block, set, allowOwned: true);

            var sw = Create<StorySwitchNode>(300, 200);
            sw.valueKey = key;
            sw.cases = new List<string> { "Happy", "Tired" };
            m_Graph.Connect(sequence, sw);
            StorySwitchCases.Sync(m_Graph, sw);
            var actions = new StoryCountingAction[3];
            for (int i = 0; i < 3; i++)
            {
                actions[i] = Create<StoryCountingAction>(300 + i * 150, 400);
                m_Graph.Connect(sw.children[i], actions[i]);
            }

            var clone = Clone();
            clone.Update(); // Sequence: Block (Set Text) → Switch
            clone.Update();

            Assert.AreEqual("Tired", clone.blackboard.TryGetKey("Mood", out var mood) ? mood.GetValueObject() : null);
            Assert.AreEqual(0, Runtime<StoryCountingAction>(clone, actions[0]).processCount);
            Assert.AreEqual(1, Runtime<StoryCountingAction>(clone, actions[1]).processCount, "the 'Tired' case ran");
            Assert.AreEqual(0, Runtime<StoryCountingAction>(clone, actions[2]).processCount);
        }

        [Test]
        public void Block_RunsActionsInListOrder_StopsAtFailure()
        {
            var block = Create<StoryBlockNode>(0, 100);
            m_Graph.Connect(m_Root, block);
            // Created right-to-left: list order, not X, decides execution.
            var first = Create<StoryCountingAction>(600, 200);
            var second = Create<StoryCountingAction>(300, 200);
            var third = Create<StoryCountingAction>(0, 200);
            foreach (var a in new[] { first, second, third }) m_Graph.Connect(block, a, allowOwned: true);
            second.result = Node.Status.Failure;

            CollectionAssert.AreEqual(new Node[] { first, second, third }, block.children);

            var clone = Clone();
            clone.Update();

            Assert.AreEqual(1, Runtime<StoryCountingAction>(clone, first).processCount);
            Assert.AreEqual(1, Runtime<StoryCountingAction>(clone, second).processCount);
            Assert.AreEqual(0, Runtime<StoryCountingAction>(clone, third).processCount);
        }

        // ---------- Combining with the rest of the system ----------

        [Test]
        public void StoryNodes_InsideCoreComposites_WithDecorators_Run()
        {
            // Root → Selector → [Inverter(Block[fail-action])] , Block[action]
            var selector = Create<SelectorNode>(0, 100);
            m_Graph.Connect(m_Root, selector);
            var failing = Create<StoryBlockNode>(0, 200);
            var fallback = Create<StoryBlockNode>(300, 200);
            m_Graph.Connect(selector, failing);
            m_Graph.Connect(selector, fallback);
            var succeeds = Create<StoryCountingAction>(0, 300);
            var runsSecond = Create<StoryCountingAction>(300, 300);
            m_Graph.Connect(failing, succeeds, allowOwned: true);
            m_Graph.Connect(fallback, runsSecond, allowOwned: true);
            m_Graph.AddDecorator(failing, typeof(InverterNode)); // Success → Failure, so the Selector moves on

            var clone = Clone();
            clone.Update(); // tick 1: inverted Block fails, the Selector moves to its next child
            clone.Update(); // tick 2: the fallback Block runs

            Assert.AreEqual(1, Runtime<StoryCountingAction>(clone, succeeds).processCount);
            Assert.AreEqual(1, Runtime<StoryCountingAction>(clone, runsSecond).processCount, "the inverted Block failed, so the Selector ran the next one");
        }

        [Test]
        public void SwitchCase_WithDecoratorAndCoreBranch_IsSelected()
        {
            var key = Track(ScriptableObject.CreateInstance<StringKey>());
            key.keyName = key.name = "Mood";
            var bb = Track(ScriptableObject.CreateInstance<Blackboard>());
            bb.keys.Add(key);
            m_Tree.blackboard = bb;

            var sw = SyncedSwitch("A");
            sw.valueKey = key;
            var caseA = (StoryCaseNode)sw.children[0];
            m_Graph.AddDecorator(caseA, typeof(InverterNode));
            var sequence = Create<SequenceNode>(0, 300); // a core composite as the branch
            var action = Create<StoryCountingAction>(0, 400);
            m_Graph.Connect(caseA, sequence);
            m_Graph.Connect(sequence, action);
            Assert.IsTrue(StorySwitchCases.InSync(sw), "decorators do not count as cases");

            var clone = Clone();
            clone.blackboard.SetValue("Mood", "A");
            clone.Update();

            Assert.AreEqual(1, Runtime<StoryCountingAction>(clone, action).processCount, "the case is found through its decorator");
        }

        [Test]
        public void Reroute_UnderSwitch_Rejected_CasesStayInSync()
        {
            var sw = SyncedSwitch("A", "B");
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("manages its children"));

            Assert.IsNull(m_Graph.InsertReroute(sw, sw.children[0], new Vector2(0, 120)));
            Assert.IsTrue(StorySwitchCases.InSync(sw));
        }

        [Test]
        public void Reroute_BelowCase_IsAllowed_AndSkippedAtRuntime()
        {
            var sw = SyncedSwitch("A");
            var caseA = (StoryCaseNode)sw.children[0];
            var action = Create<StoryCountingAction>(0, 400);
            m_Graph.Connect(caseA, action);

            Assert.IsNotNull(m_Graph.InsertReroute(caseA, action, new Vector2(0, 300)));

            var clone = Clone();
            Assert.IsFalse(clone.nodes.Any(n => n is RerouteNode));
            Assert.AreSame(Runtime<StoryCountingAction>(clone, action), Runtime<StoryCaseNode>(clone, caseA).child);
        }

        [Test]
        public void Validator_DoesNotAskToSortOwnedChildrenByPosition()
        {
            var sw = SyncedSwitch("A", "B");
            sw.children[0].position = new Rect(2000, 250, 100, 40); // case A dragged right of B

            Assert.IsFalse(GraphValidator.Validate(m_Tree).Any(i => i.Code == GraphValidator.OrderNotByPosition && i.Node == sw));
        }

        [Test]
        public void Block_MoveChild_ChangesOrder_OneUndoStep()
        {
            var block = Create<StoryBlockNode>(0, 100);
            var a = Create<StoryCountingAction>(0, 200);
            var b = Create<StoryCountingAction>(0, 200);
            m_Graph.Connect(block, a, allowOwned: true);
            m_Graph.Connect(block, b, allowOwned: true);

            Assert.IsTrue(m_Graph.MoveChild(block, b, 0));
            CollectionAssert.AreEqual(new Node[] { b, a }, block.children);

            Assert.IsTrue(m_Graph.History.Undo());
            CollectionAssert.AreEqual(new Node[] { a, b }, block.children);
        }
    }
}
