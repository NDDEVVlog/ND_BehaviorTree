using System;
using System.Collections.Generic;
using System.Linq;
using ND.Localization;
using ND.Quests;
using UnityEngine;

namespace ND.Dialogue
{
    [Serializable]
    public class VariableDefinition
    {
        public string name;
        public VariableValue initialValue = VariableValue.Bool(false);
        [TextArea] public string description;
    }

    /// <summary>A gameplay event dialogue can fire (RaiseEventAction) and scenes can react to (DialogueEventListener).</summary>
    [Serializable]
    public class EventDefinition
    {
        public string name;
        [Tooltip("What the argument means for this event (shown to designers).")]
        public string argumentHint;
        [TextArea] public string description;
    }

    /// <summary>
    /// The variables and quests conditions/actions may name. <see cref="DialogueDatabase"/> declares its own; a world
    /// state backend (e.g. a behavior-tree blackboard) supplies another so validation checks the real keys.
    /// </summary>
    public interface IDialogueSchema
    {
        bool TryGetVariableType(string variableName, out VariableType type);
        bool HasQuest(string questName);
        /// <summary>Every variable a condition/action may name (editor pickers).</summary>
        IEnumerable<string> VariableNames { get; }
        IEnumerable<string> QuestNames { get; }
        /// <summary>Declared gameplay events (RaiseEventAction). Empty = event names are not checked.</summary>
        IEnumerable<string> EventNames { get; }
    }

    /// <summary>
    /// Plug-in world state for a <see cref="DialogueDatabase"/>: what variables/quests exist (editor pickers and
    /// validation) and the runtime context that reads/writes them. Example: a behavior-tree blackboard
    /// (ND.Dialogue.NDBT's BlackboardSchemaSource). Leave a database's source empty to use its own lists.
    /// </summary>
    public abstract class DialogueSchemaSource : ScriptableObject
    {
        /// <summary>A schema with no variables or quests: for a source that is not configured yet (it warns in OnValidate).</summary>
        public static readonly IDialogueSchema Empty = new EmptySchema();

        public abstract IDialogueSchema Schema { get; }
        /// <summary>The context a runner should use at runtime for <paramref name="database"/> (its actors' attributes included).</summary>
        public abstract IDialogueContext CreateContext(DialogueDatabase database, QuestRuntime quests = null);

        private sealed class EmptySchema : IDialogueSchema
        {
            public bool TryGetVariableType(string variableName, out VariableType type) { type = default; return false; }
            public bool HasQuest(string questName) => false;
            public IEnumerable<string> VariableNames { get { yield break; } }
            public IEnumerable<string> QuestNames { get { yield break; } }
            public IEnumerable<string> EventNames { get { yield break; } }
        }
    }

    /// <summary>
    /// Everything a dialogue needs, in one asset: actors, variables, quests and conversations (a graph of entries
    /// joined by links). Text lives in localization tables; entries hold references to it.
    /// The variable/quest lists are the world state for <see cref="DialogueWorld"/>; with a
    /// <see cref="schemaSource"/> they stay empty and the source supplies the schema and the runtime context.
    /// </summary>
    [CreateAssetMenu(fileName = "DialogueDatabase", menuName = "ND Dialogue/Dialogue Database", order = 0)]
    public class DialogueDatabase : ScriptableObject, IDialogueSchema
    {
        [Tooltip("Table new entries get their text keys in (editor and importers).")]
        public StringTable textTable;

        [Tooltip("Where variables and quests live (e.g. a blackboard). Empty = the Variables and Quests lists below.")]
        public DialogueSchemaSource schemaSource;

        /// <summary>
        /// The schema conditions/actions are checked against: variables and quests from the source (else this
        /// database's own lists); events always from this database.
        /// </summary>
        public IDialogueSchema Schema => schemaSource != null ? new SourceSchema(schemaSource.Schema, this) : this;

        /// <summary>The runtime context for this database: the source's, else a new <see cref="DialogueWorld"/>.</summary>
        /// <param name="quests">The quests to use (a scene's QuestManager); null = the context keeps its own.</param>
        public IDialogueContext CreateContext(QuestRuntime quests = null) => schemaSource != null ? schemaSource.CreateContext(this, quests) : new DialogueWorld(this, quests);

        [Tooltip("Defaults for runners created without options.")]
        public DialogueRunnerOptions runnerOptions = new DialogueRunnerOptions();

        [Tooltip("Characters kept in this database only. Polymorphic: subclasses of Actor keep their own fields. Shared characters live in Character Databases.")]
        [SerializeReference] public List<Actor> actors = new List<Actor>();
        [Tooltip("Character Databases whose characters this database uses (same ids everywhere).")]
        public List<CharacterDatabase> characterDatabases = new List<CharacterDatabase>();
        [Tooltip("How shared characters look in this database (other portraits per emotion, another display name).")]
        public List<ActorLook> looks = new List<ActorLook>();
        public List<VariableDefinition> variables = new List<VariableDefinition>();
        [Tooltip("Quest Databases whose quests this database uses (conditions, actions, the quest log).")]
        public List<QuestDatabase> questDatabases = new List<QuestDatabase>();
        [Tooltip("Quests kept in this database only (older content; new quests belong in a Quest Database).")]
        public List<QuestDefinition> quests = new List<QuestDefinition>();
        public List<EventDefinition> events = new List<EventDefinition>();
        public List<Conversation> conversations = new List<Conversation>();

        /// <summary>Variables/quests of a schema source + this database's events.</summary>
        private sealed class SourceSchema : IDialogueSchema
        {
            private readonly IDialogueSchema m_Source;
            private readonly DialogueDatabase m_Database;
            public SourceSchema(IDialogueSchema source, DialogueDatabase database) { m_Source = source; m_Database = database; }
            public bool TryGetVariableType(string variableName, out VariableType type) =>
                m_Database.TryGetAttributeType(variableName, out type) || m_Source.TryGetVariableType(variableName, out type);
            public bool HasQuest(string questName) => m_Source.HasQuest(questName);
            public IEnumerable<string> VariableNames => m_Source.VariableNames.Concat(m_Database.AttributeVariableNames).Distinct();
            public IEnumerable<string> QuestNames => m_Source.QuestNames;
            public IEnumerable<string> EventNames => ((IDialogueSchema)m_Database).EventNames;
        }

        [SerializeField] private int m_NextActorId = 1;
        [SerializeField] private int m_NextConversationId = 1;

        public Actor FindActor(int actorId)
        {
            foreach (var a in AllActors) if (a != null && a.id == actorId) return a;
            return null;
        }

        public Actor FindActor(string actorName)
        {
            foreach (var a in AllActors) if (a != null && a.name == actorName) return a;
            return null;
        }

        /// <summary>Every character this database can use: its own <see cref="actors"/>, then each Character Database's.</summary>
        public IEnumerable<Actor> AllActors
        {
            get
            {
                foreach (var a in actors) yield return a;
                var visited = new HashSet<CharacterDatabase>();
                foreach (var db in characterDatabases)
                {
                    if (db == null || !visited.Add(db)) continue;
                    foreach (var c in db.characters) yield return c;
                }
            }
        }

        /// <summary>The Character Database a character comes from, or null when it is one of this database's own actors.</summary>
        public CharacterDatabase SourceOf(Actor actor)
        {
            if (actor == null || actors.Contains(actor)) return null;
            foreach (var db in characterDatabases)
                if (db != null && db.characters.Contains(actor)) return db;
            return null;
        }

        public ActorLook FindLook(int actorId)
        {
            foreach (var l in looks) if (l != null && l.actorId == actorId) return l;
            return null;
        }

        /// <summary>
        /// The emotion as it looks in THIS database: this database's look of the character first (<see cref="looks"/>),
        /// else the character's own (<see cref="Actor.FindEmotion"/>, which logs when it does not exist).
        /// </summary>
        public ActorEmotion FindEmotion(Actor actor, string emotionId)
        {
            if (actor == null) return null;
            string wanted = string.IsNullOrEmpty(emotionId) ? actor.defaultEmotion : emotionId;
            var overridden = FindLook(actor.id)?.FindEmotion(wanted);
            return overridden ?? actor.FindEmotion(emotionId);
        }

        /// <summary>The character's name as shown in this database (its look's display name, else its own).</summary>
        public string DisplayNameOf(Actor actor)
        {
            if (actor == null) return string.Empty;
            var look = FindLook(actor.id);
            return look != null && look.displayName != null && look.displayName.HasContent ? look.displayName.GetText() : actor.ResolveDisplayName();
        }

        public Conversation FindConversation(int conversationId)
        {
            foreach (var c in conversations) if (c != null && c.id == conversationId) return c;
            return null;
        }

        public Conversation FindConversation(string conversationTitle)
        {
            foreach (var c in conversations) if (c != null && c.title == conversationTitle) return c;
            return null;
        }

        public VariableDefinition FindVariable(string variableName)
        {
            foreach (var v in variables) if (v != null && v.name == variableName) return v;
            return null;
        }

        /// <summary>This database's own quests, then those of its Quest Databases (a name defined twice: the first wins; Validate reports it).</summary>
        public IEnumerable<QuestDefinition> AllQuests
        {
            get
            {
                foreach (var q in quests) if (q != null) yield return q;
                foreach (var database in questDatabases)
                {
                    if (database == null) continue;
                    foreach (var q in database.quests) if (q != null) yield return q;
                }
            }
        }

        public QuestDefinition FindQuest(string questName)
        {
            foreach (var q in AllQuests) if (q.name == questName) return q;
            return null;
        }

        /// <summary>Own variables, plus every actor attribute (as <c>Actor.attribute</c>).</summary>
        public bool TryGetVariableType(string variableName, out VariableType type)
        {
            if (TryGetAttributeType(variableName, out type)) return true;
            var definition = FindVariable(variableName);
            type = definition != null ? definition.initialValue.type : default;
            return definition != null;
        }

        public bool HasQuest(string questName) => FindQuest(questName) != null;

        IEnumerable<string> IDialogueSchema.VariableNames
        {
            get
            {
                var seen = new HashSet<string>();
                foreach (var v in variables) if (v != null && !string.IsNullOrEmpty(v.name) && seen.Add(v.name)) yield return v.name;
                foreach (var n in AttributeVariableNames) if (seen.Add(n)) yield return n;
            }
        }

        #region Actor attributes

        /// <summary>
        /// Splits <c>Actor.attribute</c> into the actor and the attribute name (the actor is matched by name up to the
        /// first '.'). False when no actor has that name or the actor declares no such attribute.
        /// An actor attribute wins over a world variable of the same full name (Validate reports such a clash).
        /// </summary>
        public bool TryResolveAttribute(string variableName, out Actor actor, out VariableDefinition attribute)
        {
            actor = null;
            attribute = null;
            if (string.IsNullOrEmpty(variableName)) return false;
            int dot = variableName.IndexOf(Actor.AttributeSeparator);
            if (dot <= 0 || dot == variableName.Length - 1) return false;
            actor = FindActor(variableName.Substring(0, dot));
            attribute = actor?.FindAttribute(variableName.Substring(dot + 1));
            return attribute != null;
        }

        public bool TryGetAttributeType(string variableName, out VariableType type)
        {
            bool found = TryResolveAttribute(variableName, out _, out var attribute);
            type = found ? attribute.initialValue.type : default;
            return found;
        }

        /// <summary>Every actor attribute as a variable name (<c>Guard.trust</c>…).</summary>
        public IEnumerable<string> AttributeVariableNames
        {
            get
            {
                foreach (var actor in AllActors)
                {
                    if (actor == null || string.IsNullOrEmpty(actor.name)) continue;
                    foreach (var attribute in actor.AttributeDefinitions)
                        if (attribute != null && !string.IsNullOrEmpty(attribute.name)) yield return actor.AttributeVariable(attribute.name);
                }
            }
        }

        #endregion

        IEnumerable<string> IDialogueSchema.QuestNames
        {
            get { foreach (var q in AllQuests) if (!string.IsNullOrEmpty(q.name)) yield return q.name; }
        }

        IEnumerable<string> IDialogueSchema.EventNames
        {
            get { foreach (var e in events) if (e != null && !string.IsNullOrEmpty(e.name)) yield return e.name; }
        }

        public EventDefinition FindEvent(string eventName)
        {
            foreach (var e in events) if (e != null && e.name == eventName) return e;
            return null;
        }

        public Actor AddActor(string actorName, bool isPlayer = false) => AddActor(new Actor { name = actorName, isPlayer = isPlayer });

        /// <summary>Adds a character of a subclass (<c>new Merchant { name = "Ann" }</c>) and gives it the next id.</summary>
        public T AddActor<T>(T actor) where T : Actor
        {
            if (actor == null || string.IsNullOrEmpty(actor.name) || FindActor(actor.name) != null)
            {
                Debug.LogError($"[Dialogue] {name}: actor '{actor?.name}' is unnamed or already exists.", this);
                return null;
            }
            if (actor.name.IndexOf(Actor.AttributeSeparator) >= 0)
            {
                Debug.LogError($"[Dialogue] {name}: actor name '{actor.name}' contains '{Actor.AttributeSeparator}', which separates attributes.", this);
                return null;
            }
            actor.id = m_NextActorId++;
            actors.Add(actor);
            return actor;
        }

        /// <summary>New conversation with its START entry.</summary>
        public Conversation AddConversation(string conversationTitle)
        {
            if (FindConversation(conversationTitle) != null)
            {
                Debug.LogError($"[Dialogue] {name}: conversation '{conversationTitle}' already exists.", this);
                return null;
            }
            var conversation = new Conversation { id = m_NextConversationId++, title = conversationTitle };
            conversation.EnsureStartEntry();
            conversations.Add(conversation);
            return conversation;
        }

        public VariableDefinition AddVariable(string variableName, VariableValue initialValue)
        {
            if (FindVariable(variableName) != null)
            {
                Debug.LogError($"[Dialogue] {name}: variable '{variableName}' already exists.", this);
                return null;
            }
            var variable = new VariableDefinition { name = variableName, initialValue = initialValue };
            variables.Add(variable);
            return variable;
        }

        public QuestDefinition AddQuest(string questName, QuestState initialState = QuestState.Inactive)
        {
            if (FindQuest(questName) != null)
            {
                Debug.LogError($"[Dialogue] {name}: quest '{questName}' already exists.", this);
                return null;
            }
            var quest = new QuestDefinition { name = questName, initialState = initialState };
            quests.Add(quest);
            return quest;
        }

        /// <summary>
        /// Removes an entry and every link in the database that pointed at it (links are addressed by id, so a link
        /// from another conversation would otherwise dangle). START cannot be removed.
        /// </summary>
        public bool RemoveEntry(int conversationId, int entryId)
        {
            var conversation = FindConversation(conversationId);
            if (conversation == null || !conversation.RemoveEntry(entryId)) return false;
            foreach (var other in conversations)
            {
                if (other == null || other == conversation) continue;
                foreach (var e in other.entries)
                    e?.links.RemoveAll(l => l.conversationId == conversationId && l.entryId == entryId);
            }
            return true;
        }

        /// <summary>Removes a conversation and every link from other conversations that pointed into it.</summary>
        public bool RemoveConversation(int conversationId)
        {
            var conversation = FindConversation(conversationId);
            if (conversation == null) return false;
            conversations.Remove(conversation);
            foreach (var other in conversations)
            {
                if (other == null) continue;
                foreach (var e in other.entries)
                    e?.links.RemoveAll(l => l.conversationId == conversationId);
            }
            return true;
        }

        /// <summary>
        /// Structural problems that would break a conversation at runtime: duplicate ids/names, missing START,
        /// links to entries that do not exist, unknown actors, conditions/actions naming unknown variables or quests.
        /// </summary>
        /// <param name="schema">Variables/quests to check names against; null = <see cref="Schema"/>.</param>
        public List<string> Validate(IDialogueSchema schema = null)
        {
            schema = schema ?? Schema;
            var issues = new List<string>();
            var actorIds = new HashSet<int>();
            var actorNames = new HashSet<string>();
            foreach (var a in AllActors)
            {
                if (a == null) { issues.Add("Null actor in list."); continue; }
                if (!actorIds.Add(a.id)) issues.Add($"Duplicate actor id {a.id} ('{a.name}').");
                if (string.IsNullOrEmpty(a.name) || !actorNames.Add(a.name)) issues.Add($"Actor {a.id}: empty or duplicate name '{a.name}'.");
                if (a.name != null && a.name.IndexOf(Actor.AttributeSeparator) >= 0)
                    issues.Add($"Actor '{a.name}': the name contains '{Actor.AttributeSeparator}', which separates attributes ('{a.name}.x' is ambiguous).");
                var attributeNames = new HashSet<string>();
                foreach (var attribute in a.AttributeDefinitions)
                {
                    if (attribute == null || string.IsNullOrEmpty(attribute.name) || !attributeNames.Add(attribute.name))
                        issues.Add($"Actor '{a.name}': empty or duplicate attribute name '{attribute?.name}'.");
                    else if (FindVariable(a.AttributeVariable(attribute.name)) != null)
                        issues.Add($"Actor '{a.name}': attribute '{attribute.name}' hides the variable '{a.AttributeVariable(attribute.name)}'.");
                }
                foreach (var own in a.attributes)
                    if (own != null && a.IsTemplateAttribute(own.name))
                        issues.Add($"Actor '{a.name}': its attribute '{own.name}' is already given by the template '{a.template.name}' (the template's wins; remove it).");
                foreach (var value in a.templateValues)
                {
                    if (value == null) continue;
                    var declared = a.template != null ? a.template.FindTemplateAttribute(value.name) : null;
                    if (declared == null)
                        issues.Add($"Actor '{a.name}': it sets '{value.name}', which its template no longer declares (the value is ignored).");
                    else if (declared.initialValue.type != value.initialValue.type)
                        issues.Add($"Actor '{a.name}': its value of '{value.name}' is {value.initialValue.type}, the template now says {declared.initialValue.type} (the template's value is used).");
                }
            }
            for (int i = 0; i < characterDatabases.Count; i++)
            {
                if (characterDatabases[i] == null) { issues.Add($"Character Databases: empty slot {i}."); continue; }
                issues.AddRange(characterDatabases[i].Validate());
            }
            foreach (var look in looks)
            {
                if (look == null) continue;
                var actor = FindActor(look.actorId);
                if (actor == null) { issues.Add($"Look for unknown character id {look.actorId}."); continue; }
                foreach (var emotion in look.emotions)
                    if (emotion != null && actor.emotions.TrueForAll(e => e == null || e.id != emotion.id))
                        issues.Add($"Look of '{actor.name}': emotion '{emotion.id}' does not exist on the character (looks only change existing emotions).");
            }
            CheckNames(variables, v => v.name, "variable", issues);
            CheckNames(AllQuests.ToList(), q => q.name, "quest", issues);
            for (int i = 0; i < questDatabases.Count; i++)
            {
                if (questDatabases[i] == null) { issues.Add($"Quest Databases: empty slot {i}."); continue; }
                issues.AddRange(questDatabases[i].Validate());
            }
            foreach (var q in AllQuests)
            {
                if (q == null || string.IsNullOrEmpty(q.name)) continue;
                if (schemaSource != null && !schema.HasQuest(q.name))
                    issues.Add($"Quest journal '{q.name}': the world ('{schemaSource.name}') has no such quest.");
                for (int i = 0; i < q.steps.Count; i++)
                {
                    var step = q.steps[i];
                    if (step == null) { issues.Add($"Quest '{q.name}': empty step slot {i + 1}."); continue; }
                    string where = $"Quest '{q.name}' step {i + 1}";
                    foreach (var c in step.visibleWhen.Concat(step.doneWhen))
                    {
                        if (c == null) issues.Add($"{where}: empty condition slot.");
                        else if (c is DialogueCondition dialogueCondition) dialogueCondition.Validate(schema, where, issues);
                    }
                }
            }
            CheckNames(events, e => e.name, "event", issues);

            var conversationIds = new HashSet<int>();
            foreach (var c in conversations)
            {
                if (c == null) { issues.Add("Null conversation in list."); continue; }
                if (!conversationIds.Add(c.id)) issues.Add($"Duplicate conversation id {c.id} ('{c.title}').");
                if (c.StartEntry == null) issues.Add($"Conversation '{c.title}' has no START entry (id {Conversation.StartEntryId}).");
                if (c.isBark && c.StartEntry != null)
                    foreach (var link in c.StartEntry.links)
                    {
                        var first = link.conversationId == c.id ? c.FindEntry(link.entryId) : null;
                        var speaker = first != null ? FindActor(first.actorId) : null;
                        if (first != null && !first.isPassthrough && speaker != null && speaker.isPlayer)
                            issues.Add($"Bark conversation '{c.title}': START links to #{first.id}, a player line — barks only say NPC lines (it is never said).");
                    }

                var entryIds = new HashSet<int>();
                foreach (var e in c.entries)
                {
                    if (e == null) { issues.Add($"Conversation '{c.title}': null entry."); continue; }
                    string where = $"Conversation '{c.title}' entry {e.id}";
                    if (!entryIds.Add(e.id)) issues.Add($"{where}: duplicate entry id.");
                    if (e.id != Conversation.StartEntryId && FindActor(e.actorId) == null) issues.Add($"{where}: unknown actor id {e.actorId}.");
                    if (e.isHub && !e.isPassthrough) issues.Add($"{where}: Hub is on but Passthrough is off (it works as a hub only when it shows no line).");
                    if (e.IsHub && e.actions.Count > 0) issues.Add($"{where}: a hub is never entered, so its actions never run (move them to the lines it leads to).");
                    if (e.IsHub && e.links.Count == 0) issues.Add($"{where}: hub without links (links into it lead nowhere).");
                    if (!string.IsNullOrWhiteSpace(e.sequence))
                    {
                        var sequenceErrors = new List<string>();
                        DialogueSequence.Parse(e.sequence, sequenceErrors);
                        foreach (var error in sequenceErrors) issues.Add($"{where}: sequence {error}");
                    }
                    for (int line = 0; line < e.LineCount; line++) CheckMarkup(e.LineText(line)?.Text, e.LineCount > 1 ? $"{where} line {line + 1}" : where, issues);
                    CheckMarkup(e.menuText?.Text, $"{where} menu text", issues);
                    foreach (var link in e.links)
                    {
                        var target = FindConversation(link.conversationId);
                        if (target == null) issues.Add($"{where}: link to unknown conversation {link.conversationId}.");
                        else if (target.FindEntry(link.entryId) == null) issues.Add($"{where}: link to missing entry {link.entryId} in '{target.title}'.");
                        string linkWhere = $"{where} link → {link.entryId}";
                        foreach (var condition in link.conditions ?? new List<DialogueCondition>())
                        {
                            if (condition == null) issues.Add($"{linkWhere}: empty condition slot.");
                            else condition.Validate(schema, linkWhere, issues);
                        }
                    }
                    foreach (var condition in e.conditions)
                    {
                        if (condition == null) issues.Add($"{where}: empty condition slot.");
                        else condition.Validate(schema, where, issues);
                    }
                    foreach (var action in e.actions)
                    {
                        if (action == null) issues.Add($"{where}: empty action slot.");
                        else action.Validate(schema, where, issues);
                    }
                }
            }
            return issues;
        }

        /// <summary>Reports the line markup errors of a plain text (pause without seconds, unclosed style, fix without err…).</summary>
        private static void CheckMarkup(string text, string where, List<string> issues)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('<') < 0) return;
            var errors = new List<string>();
            LineMarkup.Parse(text, errors);
            foreach (var error in errors) issues.Add($"{where}: markup {error}");
        }

        private static void CheckNames<T>(List<T> items, Func<T, string> nameOf, string kind, List<string> issues) where T : class
        {
            var seen = new HashSet<string>();
            foreach (var item in items)
            {
                if (item == null) { issues.Add($"Null {kind} in list."); continue; }
                string n = nameOf(item);
                if (string.IsNullOrEmpty(n) || !seen.Add(n)) issues.Add($"Empty or duplicate {kind} name '{n}'.");
            }
        }

        private void OnValidate()
        {
            foreach (var c in conversations) c?.InvalidateCache();
        }
    }
}
