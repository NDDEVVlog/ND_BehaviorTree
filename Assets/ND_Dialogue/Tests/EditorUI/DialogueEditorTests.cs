using ND.Quests;
using System.Collections.Generic;
using System.Linq;
using ND.GraphUI;
using ND.Localization;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ND.Dialogue.Editor.Tests
{
    /// <summary>Canvas operations write the database (with Undo) and the views follow — no window needed.</summary>
    public class DialogueEditorTests
    {
        private readonly List<Object> m_Track = new List<Object>();
        private DialogueDatabase m_Db;
        private StringTable m_Table;
        private Conversation m_Conv;
        private DialogueGraphCanvas m_Canvas;
        private Actor m_Guard, m_Player;

        [SetUp]
        public void SetUp()
        {
            m_Db = ScriptableObject.CreateInstance<DialogueDatabase>();
            m_Table = ScriptableObject.CreateInstance<StringTable>();
            m_Track.Add(m_Db);
            m_Track.Add(m_Table);
            m_Db.textTable = m_Table;
            m_Guard = m_Db.AddActor("Guard");
            m_Player = m_Db.AddActor("Player", isPlayer: true);
            m_Conv = m_Db.AddConversation("Gate");
            m_Canvas = new DialogueGraphCanvas();
            m_Canvas.Load(m_Db, m_Conv, "en");
        }

        [TearDown]
        public void TearDown()
        {
            Undo.ClearAll();
            foreach (var o in m_Track) if (o != null) Object.DestroyImmediate(o);
            m_Track.Clear();
        }

        private DialogueNodeView View(DialogueEntry entry) => m_Canvas.GetNodeView(entry.id.ToString());

        [Test]
        public void Load_DrawsOneNodePerEntry_AndOnlyLinksInsideTheConversation()
        {
            var other = m_Db.AddConversation("Other");
            var a = m_Conv.AddEntry(m_Guard.id);
            var b = m_Conv.AddEntry(m_Guard.id);
            m_Conv.Link(m_Conv.StartEntry, a);
            m_Conv.Link(a, b);
            a.links.Add(new DialogueLink { conversationId = other.id, entryId = Conversation.StartEntryId });

            m_Canvas.Reload();

            Assert.AreEqual(3, m_Canvas.Nodes.Count);
            Assert.AreEqual(2, m_Canvas.Edges.Count, "the cross-conversation link is a badge, not an edge");
            Assert.IsTrue(m_Canvas.Edges.All(e => e.IsNodeAnchored));
            Assert.IsTrue(View(a).Chips.Query<Label>().ToList().Any(chip => chip.text == "↗ Other" && chip.ClassListContains("dlg-chip--external")),
                "a link out of the conversation shows as a chip");
        }

        [Test]
        public void LinkingNodes_WritesTheLink_AndRefusesStartAndDuplicates()
        {
            var a = m_Conv.AddEntry(m_Guard.id);
            var b = m_Conv.AddEntry(m_Player.id);
            m_Canvas.Reload();

            Assert.IsTrue(m_Canvas.TryConnectNodes(View(a), View(b)));
            Assert.AreEqual(b.id, a.links.Single().entryId);
            Assert.AreEqual(m_Conv.id, a.links.Single().conversationId);
            Assert.IsNotNull(m_Canvas.FindNodeEdge(View(a), View(b)));

            Assert.IsFalse(m_Canvas.CanConnectNodes(View(a), View(b)), "no duplicate link");
            Assert.IsFalse(m_Canvas.CanConnectNodes(View(a), View(m_Conv.StartEntry)), "START is never a target");
            Assert.IsTrue(m_Canvas.CanConnectNodes(View(b), View(a)), "a loop back is allowed");
        }

        [Test]
        public void RemoveLink_DropsDataAndEdge()
        {
            var a = m_Conv.AddEntry(m_Guard.id);
            m_Conv.Link(m_Conv.StartEntry, a);
            m_Canvas.Reload();
            var edge = m_Canvas.Edges.Single();

            m_Canvas.RemoveLink(edge);

            Assert.IsEmpty(m_Conv.StartEntry.links);
            Assert.AreEqual(0, m_Canvas.Edges.Count);
        }

        [Test]
        public void CreateChild_OfSelection_UsesTheRightActor_AndLinksIt()
        {
            m_Canvas.AddToSelection((GraphNodeView)View(m_Conv.StartEntry));
            var line = m_Canvas.CreateChildOfSelection(NewEntryKind.NpcLine);
            Assert.AreEqual(m_Guard.id, line.actorId);
            Assert.AreEqual(line.id, m_Conv.StartEntry.links.Single().entryId);
            Assert.AreSame(line, m_Canvas.SingleSelectedEntry, "the new entry becomes the selection");

            var response = m_Canvas.CreateChildOfSelection(NewEntryKind.PlayerResponse);
            Assert.AreEqual(m_Player.id, response.actorId);
            Assert.AreEqual(response.id, line.links.Single().entryId);
            Assert.Greater(response.position.x, line.position.x, "children go to the right");
            Assert.AreEqual(3, m_Canvas.Nodes.Count);
            Assert.AreEqual(2, m_Canvas.Edges.Count);
        }

        [Test]
        public void CreateEntry_WithoutAPlayerActor_IsRefused()
        {
            m_Db.actors.Remove(m_Player);
            Assert.IsNull(m_Canvas.CreateEntry(NewEntryKind.PlayerResponse, Vector2.zero));
            Assert.AreEqual(1, m_Conv.entries.Count);
        }

        [Test]
        public void DeleteSelection_KeepsStart_RemovesLinks_AndUndoRestoresEverything()
        {
            var a = m_Conv.AddEntry(m_Guard.id);
            var b = m_Conv.AddEntry(m_Guard.id);
            m_Conv.Link(m_Conv.StartEntry, a);
            m_Conv.Link(a, b);
            m_Canvas.Reload();
            Undo.ClearAll();

            m_Canvas.AddToSelection((GraphNodeView)View(m_Conv.StartEntry));
            m_Canvas.AddToSelection((GraphNodeView)View(a));
            m_Canvas.DeleteSelection();

            Assert.IsNotNull(m_Conv.StartEntry, "START survives");
            Assert.IsNull(m_Conv.FindEntry(a.id));
            Assert.IsEmpty(m_Conv.StartEntry.links);
            Assert.AreEqual(2, m_Canvas.Nodes.Count);

            Undo.PerformUndo();
            m_Conv = m_Db.FindConversation(m_Conv.id); // undo restores the serialized data into new objects
            m_Conv.InvalidateCache();
            m_Canvas.Load(m_Db, m_Conv, "en");

            Assert.IsNotNull(m_Conv.FindEntry(a.id));
            Assert.AreEqual(a.id, m_Conv.StartEntry.links.Single().entryId);
            Assert.AreEqual(3, m_Canvas.Nodes.Count);
            Assert.AreEqual(2, m_Canvas.Edges.Count);
        }

        [Test]
        public void CommitNodeMove_WritesPositions()
        {
            var a = m_Conv.AddEntry(m_Guard.id);
            m_Canvas.Reload();
            View(a).SetPosition(new Vector2(120, 340));
            m_Canvas.CommitNodeMove(new List<GraphNodeView> { View(a) });
            Assert.AreEqual(new Vector2(120, 340), a.position);
        }

        [Test]
        public void TextEditing_IsPlainUntilLocalized_ThenPerLocale()
        {
            var a = m_Conv.AddEntry(m_Guard.id);
            Assert.IsTrue(DialogueTextEditing.Write(m_Db, m_Conv, a, menu: false, "en", "Halt!"));
            Assert.IsFalse(a.text.IsLocalized, "writing does not touch the table");
            Assert.AreEqual("Halt!", a.text.Text);
            Assert.AreEqual("Halt!", DialogueTextEditing.Read(a.text, "vi"), "plain text is the same in every locale");
            Assert.AreEqual(0, m_Table.Count);

            Assert.IsTrue(DialogueTextEditing.Localize(m_Db, m_Conv, a, menu: false, "en"));
            Assert.IsTrue(a.text.IsLocalized);
            Assert.IsTrue(m_Table.TryGetKey(a.text.EntryId, out string key));
            Assert.AreEqual(DialogueTextEditing.KeyFor(m_Conv, a, menu: false), key);
            Assert.AreEqual("Halt!", DialogueTextEditing.Read(a.text, "en"), "the plain text became the en translation");

            Assert.IsTrue(DialogueTextEditing.Write(m_Db, m_Conv, a, menu: false, "vi", "Đứng lại!"));
            Assert.AreEqual("Đứng lại!", DialogueTextEditing.Read(a.text, "vi"));
            DialogueTextEditing.Write(m_Db, m_Conv, a, menu: false, "vi", string.Empty);
            Assert.IsNull(DialogueTextEditing.Read(a.text, "vi"));
            Assert.AreEqual("Halt!", DialogueTextEditing.Read(a.text, "en"), "other locales untouched");
        }

        [Test]
        public void LocalizeAll_ConnectsEveryPlainText_AndSkipsEmptyOnes()
        {
            var a = m_Conv.AddEntry(m_Guard.id);
            var b = m_Conv.AddEntry(m_Player.id);
            var empty = m_Conv.AddEntry(m_Guard.id);
            a.text.Text = "Halt!";
            b.text.Text = "I would like to pass, please.";
            b.menuText.Text = "Ask to pass";

            Assert.AreEqual(3, DialogueTextEditing.LocalizeAll(m_Db, "en"));
            Assert.IsTrue(a.text.IsLocalized && b.text.IsLocalized && b.menuText.IsLocalized);
            Assert.IsFalse(empty.text.IsLocalized);
            Assert.AreEqual(0, DialogueTextEditing.LocalizeAll(m_Db, "en"), "already connected texts are left alone");
        }

        [Test]
        public void Localize_WithoutTable_IsRefused()
        {
            m_Db.textTable = null;
            var a = m_Conv.AddEntry(m_Guard.id);
            a.text.Text = "Halt!";
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("no Text Table"));
            Assert.IsFalse(DialogueTextEditing.Localize(m_Db, m_Conv, a, false, "en"));
            Assert.IsFalse(a.text.IsLocalized);
        }

        [Test]
        public void NodePreview_FollowsThePreviewLocale()
        {
            var a = m_Conv.AddEntry(m_Guard.id);
            DialogueTextEditing.Write(m_Db, m_Conv, a, false, "en", "Halt!");
            DialogueTextEditing.Localize(m_Db, m_Conv, a, false, "en");
            DialogueTextEditing.Write(m_Db, m_Conv, a, false, "vi", "Đứng lại!");
            m_Canvas.Reload();
            var text = View(a).Q<Label>("text");
            Assert.AreEqual("Halt!", text.text);
            m_Canvas.SetPreviewLocale("vi");
            Assert.AreEqual("Đứng lại!", text.text);
            m_Canvas.SetPreviewLocale("ja");
            StringAssert.Contains("no ja text", text.text);
        }

        [Test]
        public void EdgeLabel_ShowsPriorityAndLinkConditions()
        {
            m_Db.AddVariable("gold", VariableValue.Int(0));
            var a = m_Conv.AddEntry(m_Guard.id);
            var link = m_Conv.Link(m_Conv.StartEntry, a, LinkPriority.High);
            link.conditions.Add(new VariableCondition { variable = "gold", op = CompareOp.GreaterOrEqual, value = "5" });
            link.conditions.Add(new QuestStateCondition { quest = "key", state = QuestState.Active });
            m_Canvas.Reload();

            var edge = m_Canvas.Edges.Single();
            Assert.AreEqual("◆ 2", edge.Text, "two conditions: a compact count chip");
            Assert.AreEqual("▲ High\n◆ gold >= 5\n◆ quest key == Active", DialogueEdgeView.Describe(link), "the full text is the chip's tooltip");
            Assert.IsNotNull(DialogueEdgeView.PriorityColor(LinkPriority.High), "High is drawn in its own colour");
            Assert.IsNull(DialogueEdgeView.PriorityColor(LinkPriority.Normal));

            link.conditions.RemoveAt(1);
            Assert.AreEqual("◆ gold >= 5", DialogueEdgeView.ChipText(link), "one short condition is shown as is");
            for (int i = 0; i < 4; i++) link.conditions.Add(new VariableCondition { variable = "gold" });
            Assert.That(DialogueEdgeView.Describe(link), Does.EndWith("… +2 more"));
            Assert.AreEqual(string.Empty, DialogueEdgeView.ChipText(new DialogueLink()), "no conditions: no chip");
        }

        [Test]
        public void NodeChips_SummariseConditionsActionsAndEvents_WithDetailsInTooltips()
        {
            var a = m_Conv.AddEntry(m_Guard.id);
            a.conditions.Add(new VariableCondition { variable = "gold", value = "1" });
            a.actions.Add(new SetVariableAction { variable = "gold", value = "2" });
            a.actions.Add(new SetQuestStateAction { quest = "key" });
            a.actions.Add(new RaiseEventAction { eventName = "open_gate" });
            m_Canvas.Reload();

            var chips = View(a).Chips.Query<Label>().ToList();
            CollectionAssert.AreEqual(new[] { "◆ 1", "✎ 2", "⚡ open_gate" }, chips.Select(c => c.text));
            StringAssert.Contains("gold == 1", chips[0].tooltip);
            StringAssert.Contains("quest key → Active", chips[1].tooltip);

            var plain = m_Conv.AddEntry(m_Guard.id);
            m_Canvas.Reload();
            Assert.AreEqual(DisplayStyle.None, View(plain).Chips.style.display.value, "no chips: the row is hidden");
        }

        [Test]
        public void SpeakerInitial_HasAStableColourPerCharacter()
        {
            Assert.AreEqual(DialogueNodeView.ActorColor("Guard"), DialogueNodeView.ActorColor("Guard"));
            Assert.AreNotEqual(DialogueNodeView.ActorColor("Guard"), DialogueNodeView.ActorColor("Merchant"));
            var a = m_Conv.AddEntry(m_Guard.id);
            m_Canvas.Reload();
            Assert.AreEqual("G", View(a).Q<Label>("avatar").text);
        }

        [Test]
        public void LinkPath_AddressesTheLink_ForTheSerializedEditor()
        {
            var a = m_Conv.AddEntry(m_Guard.id);
            var link = m_Conv.Link(m_Conv.StartEntry, a);
            string path = LinkEditorPopup.LinkPath(m_Db, m_Conv, m_Conv.StartEntry, link);
            var prop = new SerializedObject(m_Db).FindProperty(path);
            Assert.IsNotNull(prop);
            Assert.AreEqual(a.id, prop.FindPropertyRelative(nameof(DialogueLink.entryId)).intValue);
            Assert.IsNull(LinkEditorPopup.LinkPath(m_Db, m_Conv, m_Conv.StartEntry, new DialogueLink()), "a link not in the data has no path");
        }

        [Test]
        public void RefreshNode_AfterASerializedEdit_ShowsAndHoldsTheCurrentData()
        {
            var a = m_Conv.AddEntry(m_Guard.id);
            m_Conv.Link(m_Conv.StartEntry, a);
            m_Canvas.Reload();

            // Edit the link through SerializedObject, exactly like the link popup does.
            var so = new SerializedObject(m_Db);
            var list = so.FindProperty(LinkEditorPopup.LinkPath(m_Db, m_Conv, m_Conv.StartEntry, m_Conv.StartEntry.links[0]))
                .FindPropertyRelative(nameof(DialogueLink.conditions));
            list.InsertArrayElementAtIndex(0);
            list.GetArrayElementAtIndex(0).managedReferenceValue = new VariableCondition { variable = "gold", value = "1" };
            so.ApplyModifiedProperties();

            m_Canvas.RefreshNode(m_Conv.StartEntry);

            var current = m_Db.FindConversation(m_Conv.id);
            Assert.AreSame(current.StartEntry, m_Canvas.GetNodeView("0").Entry, "the view holds the database's current object");
            Assert.AreEqual("◆ gold == 1", m_Canvas.Edges.Single().Text);
        }

        [Test]
        public void SelectionChanged_IsRaised_ForClicksBoxSelectAndClear()
        {
            var a = m_Conv.AddEntry(m_Guard.id);
            m_Canvas.Reload();
            int raised = 0;
            m_Canvas.SelectionChanged += () => raised++;
            GraphCanvas baseCanvas = m_Canvas;
            baseCanvas.AddToSelection(View(a));
            baseCanvas.ClearSelection();
            Assert.AreEqual(2, raised);
        }

        private DialogueEdgeView EdgeOf(DialogueEntry from, DialogueEntry to) =>
            m_Canvas.Edges.Single(e => e.FromNode == View(from) && e.ToNode == View(to));

        [Test]
        public void SelectingAnEntry_LightsItsLinks_AndFadesTheRest()
        {
            var a = m_Conv.AddEntry(m_Guard.id);
            var b = m_Conv.AddEntry(m_Player.id);
            var c = m_Conv.AddEntry(m_Guard.id);
            m_Conv.Link(m_Conv.StartEntry, a);
            m_Conv.Link(a, b);
            m_Conv.Link(b, c);
            m_Canvas.Reload();
            Assert.IsTrue(m_Canvas.Edges.All(e => e.CurrentFocus == DialogueEdgeView.Focus.None), "nothing selected: normal");

            m_Canvas.AddToSelection((GraphNodeView)View(b));
            Assert.AreEqual(DialogueEdgeView.Focus.Incoming, EdgeOf(a, b).CurrentFocus);
            Assert.AreEqual(DialogueEdgeView.Focus.Outgoing, EdgeOf(b, c).CurrentFocus);
            Assert.AreEqual(DialogueEdgeView.Focus.Faded, EdgeOf(m_Conv.StartEntry, a).CurrentFocus);

            m_Canvas.ClearSelection();
            Assert.AreEqual(DialogueEdgeView.Focus.None, EdgeOf(a, b).CurrentFocus);
        }

        [Test]
        public void BendPoints_AreKeptOnTheLink_AndEditsAreUndoable()
        {
            var a = m_Conv.AddEntry(m_Guard.id);
            var link = m_Conv.Link(m_Conv.StartEntry, a);
            link.bends.Add(new Vector2(50, 200));
            link.bends.Add(new Vector2(150, 220));
            m_Canvas.Reload();
            var edge = EdgeOf(m_Conv.StartEntry, a);
            CollectionAssert.AreEqual(link.bends, edge.Bends, "drawn from the data");

            edge.RemoveBend(0); // what double-click / Alt+click on a dot does
            var stored = m_Db.FindConversation(m_Conv.id).StartEntry.links.Single().bends;
            CollectionAssert.AreEqual(new[] { new Vector2(150, 220) }, stored);
            Undo.PerformUndo();
            Assert.AreEqual(2, m_Db.FindConversation(m_Conv.id).StartEntry.links.Single().bends.Count, "undo brings the point back");
        }

        [Test]
        public void MergeSharedLinks_PutsAHubBetween_AndThePlayerStillGetsTheSameChoices()
        {
            var hi = m_Conv.AddEntry(m_Guard.id);
            hi.text.Text = "Hi.";
            var halt = m_Conv.AddEntry(m_Guard.id);
            halt.text.Text = "Halt!";
            halt.conditions.Add(new VariableCondition { variable = "angry", value = "true" });
            var yes = m_Conv.AddEntry(m_Player.id);
            yes.text.Text = "Yes";
            var no = m_Conv.AddEntry(m_Player.id);
            no.text.Text = "No";
            var bribe = m_Conv.AddEntry(m_Player.id);
            bribe.text.Text = "Bribe";
            m_Db.AddVariable("angry", VariableValue.Bool(false));
            m_Conv.Link(m_Conv.StartEntry, halt, LinkPriority.High);
            m_Conv.Link(m_Conv.StartEntry, hi);
            foreach (var from in new[] { hi, halt })
            {
                m_Conv.Link(from, yes);
                m_Conv.Link(from, no);
            }
            m_Conv.Link(halt, bribe).conditions.Add(new VariableCondition { variable = "angry", value = "true" }); // not shared: stays
            m_Canvas.Reload();

            m_Canvas.AddToSelection((GraphNodeView)View(hi));
            m_Canvas.AddToSelection((GraphNodeView)View(halt));
            CollectionAssert.AreEqual(new[] { yes.id, no.id }, m_Canvas.SharedLinkTargets(new List<DialogueEntry> { hi, halt }));
            var hub = m_Canvas.MergeSharedLinksIntoHub();

            Assert.IsNotNull(hub);
            Assert.IsTrue(hub.IsHub);
            var conv = m_Db.FindConversation(m_Conv.id);
            CollectionAssert.AreEqual(new[] { hub.id }, conv.FindEntry(hi.id).links.Select(l => l.entryId));
            CollectionAssert.AreEqual(new[] { hub.id, bribe.id }, conv.FindEntry(halt.id).links.Select(l => l.entryId), "the conditional link stays, after the hub");
            CollectionAssert.AreEqual(new[] { yes.id, no.id }, conv.FindEntry(hub.id).links.Select(l => l.entryId));
            CollectionAssert.IsEmpty(m_Db.Validate());

            var runner = new DialogueRunner(m_Db, new DialogueWorld(m_Db));
            runner.Start(m_Conv.id);
            Assert.AreEqual("Hi.", runner.CurrentLine.Text);
            runner.Continue();
            CollectionAssert.AreEqual(new[] { "Yes", "No" }, runner.CurrentChoices.Select(c => c.Text), "the hub is not a line: its targets are the choices");
        }
    }
}
