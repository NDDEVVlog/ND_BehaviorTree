using ND.Quests;
using System.Collections.Generic;
using System.Linq;
using ND.Dialogue.Editor.Bridge;
using ND.GraphUI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND.Dialogue.Editor.Tests
{
    /// <summary>The review fixes: conditions on edges, the variable picker, the section pages, and the bridge's pure parts.</summary>
    public class DialogueEditorLayoutTests
    {
        private readonly List<Object> m_Track = new List<Object>();
        private DialogueDatabase m_Db;
        private Conversation m_Conv;
        private Actor m_Guard;

        [SetUp]
        public void SetUp()
        {
            m_Db = ScriptableObject.CreateInstance<DialogueDatabase>();
            m_Track.Add(m_Db);
            m_Guard = m_Db.AddActor("Guard");
            m_Guard.attributes.Add(new VariableDefinition { name = "trust", initialValue = VariableValue.Int(0) });
            m_Db.AddActor("Player", isPlayer: true);
            m_Db.AddVariable("player.gold", VariableValue.Int(0));
            m_Db.AddQuest("key");
            m_Conv = m_Db.AddConversation("Gate");
        }

        [TearDown]
        public void TearDown()
        {
            Undo.ClearAll();
            foreach (var o in m_Track) if (o != null) Object.DestroyImmediate(o);
            m_Track.Clear();
        }

        [Test]
        public void EdgeChip_CountsTheTargetLinesConditions_AndAPlainEdgeOffersAPlus()
        {
            var a = m_Conv.AddEntry(m_Guard.id);
            var b = m_Conv.AddEntry(m_Guard.id);
            var toA = m_Conv.Link(m_Conv.StartEntry, a);
            m_Conv.Link(m_Conv.StartEntry, b);
            a.conditions.Add(new QuestStateCondition { quest = "key", state = QuestState.Active });
            toA.conditions.Add(new VariableCondition { variable = "player.gold", op = CompareOp.GreaterOrEqual, value = "5" });

            var canvas = new DialogueGraphCanvas();
            canvas.Load(m_Db, m_Conv, "en");
            var edgeA = canvas.Edges.Single(e => ((DialogueNodeView)e.ToNode).Entry.id == a.id);
            var edgeB = canvas.Edges.Single(e => ((DialogueNodeView)e.ToNode).Entry.id == b.id);

            Assert.AreEqual("◆ 2", edgeA.Text, "the branch's own condition + the target line's entry condition");
            StringAssert.Contains("Target line", edgeA.Q<Label>(className: "dlg-edge__label").tooltip);
            Assert.AreEqual(DisplayStyle.None, edgeA.AddChip.style.display.value);
            Assert.AreEqual(string.Empty, edgeB.Text);
            Assert.AreEqual(DisplayStyle.Flex, edgeB.AddChip.style.display.value, "a condition-less edge shows '+' to add one");

            Assert.AreEqual("◆ quest key == Active", DialogueEdgeView.ChipText(new DialogueLink(), a.conditions));
        }

        [Test]
        public void TargetEntryPath_AddressesTheLinkTarget()
        {
            var a = m_Conv.AddEntry(m_Guard.id);
            var link = m_Conv.Link(m_Conv.StartEntry, a);
            var prop = new SerializedObject(m_Db).FindProperty(LinkEditorPopup.TargetEntryPath(m_Db, link));
            Assert.AreEqual(a.id, prop.FindPropertyRelative(nameof(DialogueEntry.id)).intValue);
            Assert.IsNull(LinkEditorPopup.TargetEntryPath(m_Db, new DialogueLink { conversationId = m_Conv.id, entryId = 999 }));
        }

        [Test]
        public void VariablePicker_GroupsCharactersAndWorld_WithTypeColours()
        {
            Assert.AreEqual("Characters/Guard/trust", DialogueVariableStyle.PickerPath(m_Db, "Guard.trust"));
            Assert.AreEqual("World/player/gold", DialogueVariableStyle.PickerPath(m_Db, "player.gold"));

            var entries = DialogueVariableStyle.VariableEntries(m_Db);
            CollectionAssert.AreEquivalent(new[] { "World/player/gold", "Characters/Guard/trust" }, entries.Select(e => e.Path));
            Assert.IsTrue(entries.All(e => e.Color.HasValue));
            Assert.AreEqual(DialogueVariableStyle.ForType(VariableType.Int), DialogueVariableStyle.Of(m_Db, "player.gold"));
            Assert.AreEqual(DialogueVariableStyle.UnknownColor, DialogueVariableStyle.Of(m_Db, "nope"));
            Assert.AreEqual(new[] { "key" }, DialogueVariableStyle.QuestEntries(m_Db).Select(e => e.Path).ToArray());
        }

        [Test]
        public void SchemaVariableNames_HaveNoDuplicates_WhenAWorldVariableMatchesAnAttribute()
        {
            m_Db.AddVariable("Guard.trust", VariableValue.Int(1));
            var names = ((IDialogueSchema)m_Db).VariableNames.ToList();
            Assert.AreEqual(names.Count, names.Distinct().Count());
        }

        [Test]
        public void NameButtonWrite_SetsTheFieldUndoably_AndTellsTheEditor()
        {
            var a = m_Conv.AddEntry(m_Guard.id);
            a.actions.Add(new SetVariableAction());
            string path = $"{nameof(DialogueDatabase.conversations)}.Array.data[0].{Conversation.EntriesField}.Array.data[1].{nameof(DialogueEntry.actions)}.Array.data[0].{nameof(SetVariableAction.variable)}";
            Object edited = null;
            void OnEdited(Object o) => edited = o;
            DialogueNameButton.Edited += OnEdited;
            try { DialogueNameButton.Write(m_Db, path, "Guard.trust"); }
            finally { DialogueNameButton.Edited -= OnEdited; }

            Assert.AreEqual("Guard.trust", ((SetVariableAction)m_Db.FindConversation(m_Conv.id).FindEntry(a.id).actions[0]).variable);
            Assert.AreSame(m_Db, edited);
        }

        [Test]
        public void AppendManagedReference_AddsARowToAnOrGroup()
        {
            var a = m_Conv.AddEntry(m_Guard.id);
            a.conditions.Add(new AnyCondition());
            string path = $"{nameof(DialogueDatabase.conversations)}.Array.data[0].{Conversation.EntriesField}.Array.data[1].{nameof(DialogueEntry.conditions)}.Array.data[0].{nameof(AnyCondition.conditions)}";
            DialogueRowGUI.AppendManagedReference(m_Db, path, typeof(QuestStateCondition));
            var group = (AnyCondition)m_Db.FindConversation(m_Conv.id).FindEntry(a.id).conditions[0];
            Assert.AreEqual(1, group.conditions.Count);
            Assert.IsInstanceOf<QuestStateCondition>(group.conditions[0]);
            Assert.AreEqual("Any Of (OR group)", DialogueRowGUI.TypeLabel(typeof(AnyCondition)));
            Assert.AreEqual("Set Variable", DialogueRowGUI.TypeLabel(typeof(SetVariableAction)));
        }

        [Test]
        public void LinkPriorityLabels_CoverEveryPriority()
        {
            foreach (LinkPriority p in System.Enum.GetValues(typeof(LinkPriority)))
                Assert.IsFalse(string.IsNullOrEmpty(LinkPriorityGUI.Label(p)), p.ToString());
            StringAssert.Contains("Fallback", LinkPriorityGUI.Label(LinkPriority.Low));
        }

        [Test]
        public void CharactersSection_ShowsOneCardPerCharacter_PlusAnAddCard()
        {
            var panel = new DialogueDatabasePanel();
            panel.Bind(m_Db, m_Conv.id);
            panel.ShowTab(DialogueDatabasePanel.CharactersTab);
            Assert.AreEqual(m_Db.actors.Count + 1, panel.Query(className: "dlg-charcard").ToList().Count);
            Assert.AreEqual(1, panel.Query(className: "dlg-charcard--selected").ToList().Count);
            Assert.AreEqual(1, panel.Query(className: "dlg-charcard--add").ToList().Count);
        }

        [Test]
        public void CharactersSection_ShowsSharedCharactersToo_AndCanMoveLocalOnesThere()
        {
            var cast = ScriptableObject.CreateInstance<CharacterDatabase>();
            m_Track.Add(cast);
            var draco = cast.AddCharacter("Draco");
            m_Db.characterDatabases.Add(cast);

            var panel = new DialogueDatabasePanel();
            panel.Bind(m_Db, m_Conv.id);
            panel.ShowTab(DialogueDatabasePanel.CharactersTab);
            Assert.AreEqual(m_Db.actors.Count + 2, panel.Query(className: "dlg-charcard").ToList().Count, "local + shared + the add card");
            Assert.AreEqual(1, panel.Query(className: "dlg-charcard--shared").ToList().Count);

            int guardId = m_Guard.id;
            Assert.AreEqual(2, panel.MoveLocalCharacters(cast));
            Assert.IsEmpty(m_Db.actors);
            Assert.AreSame(cast, m_Db.SourceOf(m_Db.FindActor(guardId)), "same id, now shared");
            Assert.AreSame(draco, m_Db.FindActor("Draco"));
        }

        [Test]
        public void CharactersSection_WithSeveralCharacterDatabases_BuildsWithoutTrackingErrors()
        {
            var a = ScriptableObject.CreateInstance<CharacterDatabase>();
            var b = ScriptableObject.CreateInstance<CharacterDatabase>();
            m_Track.Add(a);
            m_Track.Add(b);
            var draco = a.AddCharacter("Draco");
            draco.template = b;
            b.templateAttributes.Add(new VariableDefinition { name = "affection", initialValue = VariableValue.Int(0) });
            m_Db.characterDatabases.Add(a);
            m_Db.characterDatabases.Add(b);

            var panel = new DialogueDatabasePanel();
            panel.Bind(m_Db, m_Conv.id);
            panel.ShowTab(DialogueDatabasePanel.CharactersTab);
            foreach (var card in panel.Query(className: "dlg-charcard").ToList().Where(c => !c.ClassListContains("dlg-charcard--add")))
                using (var click = ClickEvent.GetPooled())
                {
                    click.target = card;
                    Assert.DoesNotThrow(() => card.SendEvent(click)); // selecting rebuilds with the character's own asset tracked
                }
            Assert.DoesNotThrow(() => panel.Rebuild());
            Assert.IsTrue(m_Db.Schema.TryGetVariableType("Draco.affection", out _), "the template attribute reaches the character");
        }

        [Test]
        public void EverySection_Builds_WithoutErrors()
        {
            var panel = new DialogueDatabasePanel();
            panel.Bind(m_Db, m_Conv.id);
            foreach (var tab in panel.TabTitles.ToList())
                Assert.DoesNotThrow(() => panel.ShowTab(tab), tab);
            CollectionAssert.Contains(panel.TabTitles.ToList(), DialogueDatabasePanel.LocalizationTab);
        }

        [Test]
        public void BridgeOutline_ShowsSpeakersTextConditionsAndLinks()
        {
            var a = m_Conv.AddEntry(m_Guard.id);
            a.text.Text = "Halt!";
            a.emotion = "Angry";
            a.actions.Add(new SetVariableAction { variable = "Guard.trust", value = "1" });
            var link = m_Conv.Link(m_Conv.StartEntry, a, LinkPriority.High);
            link.conditions.Add(new QuestStateCondition { quest = "key", state = QuestState.Inactive });

            string outline = DialogueBridgeCommands.Outline(m_Db, m_Conv, "en");
            StringAssert.Contains("#0 START", outline);
            StringAssert.Contains($"→ #{a.id} (High) if quest key == Inactive", outline);
            StringAssert.Contains($"#{a.id} Guard [Angry]: \"Halt!\"", outline);
            StringAssert.Contains("do: Guard.trust = 1", outline);
        }

        [Test]
        public void BridgeRefusesCommands_WhileAccessIsOff()
        {
            string response = DialogueBridgeCommands.Execute("{\"command\":\"database_list\",\"args\":{}}");
            if (DialogueAiAccess.IsActive) Assert.Inconclusive("The AI bridge is unlocked in this session.");
            StringAssert.Contains("\"ok\":false", response);
            StringAssert.Contains("Unknown command", DialogueBridgeCommands.Execute("{\"command\":\"nope\"}"));
        }

        [Test]
        public void Json_RoundTrips_NestedValues()
        {
            var parsed = (Dictionary<string, object>)DialogueJson.Parse("{\"a\": [1, 2.5, \"x\\n\\u00e9\"], \"b\": {\"c\": true, \"d\": null}}");
            var a = (List<object>)parsed["a"];
            Assert.AreEqual(1.0, a[0]);
            Assert.AreEqual(2.5, a[1]);
            Assert.AreEqual("x\né", a[2]);
            Assert.AreEqual(true, ((Dictionary<string, object>)parsed["b"])["c"]);
            Assert.AreEqual("{\"a\":[1,2.5,\"x\\né\"],\"b\":{\"c\":true,\"d\":null}}", DialogueJson.Serialize(parsed));
            Assert.Throws<System.FormatException>(() => DialogueJson.Parse("{\"a\": }"));
        }

        [Test]
        public void SearchPopupEntry_CarriesAnOptionalColour()
        {
            var plain = new GraphSearchPopup.Entry("A/B", null, 1);
            var colored = new GraphSearchPopup.Entry("A/C", null, 2, "tip", Color.red);
            Assert.IsFalse(plain.Color.HasValue);
            Assert.AreEqual(Color.red, colored.Color.Value);
        }
    }
}
