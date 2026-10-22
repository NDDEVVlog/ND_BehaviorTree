using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ND.Dialogue.Editor.Plugins;
using ND.Localization.Editor;
using UnityEditor;
using UnityEngine;

namespace ND.Dialogue.Editor.Bridge
{
    /// <summary>
    /// Commands of the dialogue AI bridge (snake_case; the ndbt CLI calls them as <c>dlg-&lt;command&gt;</c>).
    /// Read commands need Read-only or higher; write commands need Read + Write and are refused in Play mode. Every
    /// write is one Undo step on the database and redraws open Dialogue Editor windows.
    /// </summary>
    public static class DialogueBridgeCommands
    {
        private sealed class Command
        {
            public bool Write;
            public string Summary;
            public Func<Dictionary<string, object>, object> Run;
        }

        public sealed class BridgeException : Exception
        {
            public BridgeException(string message) : base(message) { }
        }

        private static readonly string[] MenuPrefixes = { "ND Dialogue/", "Window/ND Dialogue/" };

        private static readonly Dictionary<string, Command> s_Commands = new Dictionary<string, Command>
        {
            ["status"] = new Command { Summary = "Bridge, access level, Unity and play-mode state.", Run = Status },
            ["help"] = new Command { Summary = "List of commands.", Run = Help },
            ["database_list"] = new Command { Summary = "Dialogue databases. Args: folder?", Run = DatabaseList },
            ["database_get"] = new Command { Summary = "Characters, variables, quests, events and conversations of a database. Args: database", Run = DatabaseGet },
            ["conversation_get"] = new Command { Summary = "A conversation as outline (default) or json. Args: database, conversation, format? (outline|json), locale?", Run = ConversationGet },
            ["validate"] = new Command { Summary = "Problems of a database (incl. plugin rules). Args: database", Run = ValidateDb },
            ["test_start"] = new Command { Summary = "Play a conversation in the test chat (live in an open Dialogue Editor, else headless). Shows lines, actions, changes, events. Args: database, conversation, entry? (id, default START), reset? (fresh world), locale?, values?", Run = TestStart },
            ["test_continue"] = new Command { Summary = "Next line in the test chat. Args: database, values?", Run = TestContinue },
            ["test_choose"] = new Command { Summary = "Pick a choice (1 = first) in the test chat. Args: database, choice, values?", Run = TestChoose },
            ["test_state"] = new Command { Summary = "Where the test chat is: state, entry, choices, all variable / quest values, the transcript so far. Args: database", Run = TestState },
            ["test_set"] = new Command { Summary = "What if: set a variable (name) or a quest (\"quest name\") of the test world, then keep playing to see where it goes (applies from the next step). Args: database, name, value (text in the variable's type, or a quest state)", Run = TestSet },
            ["test_reset"] = new Command { Summary = "New test world (initial values, nothing seen, empty chat). Args: database", Run = TestReset },
            ["test_run"] = new Command { Summary = "Play a whole path: continue through lines, take the given choices in order (1 = first), stop at the end, at a choice not given, or after maxSteps. Args: database, conversation, entry?, choices? ([1,2,…]), reset? (default true), maxSteps? (200)", Run = TestRun },

            ["conversation_create"] = new Command { Write = true, Summary = "Add a conversation. Args: database, title", Run = ConversationCreate },
            ["entry_add"] = new Command { Write = true, Summary = "Add a line. Args: database, conversation, actor, text?, emotion?, title?, passthrough?, hub? (merges links: links into it count as links to its targets), from? (entry id to link from), lines? (more lines: [\"text\" or {\"text\", \"emotion\"}]), locale?", Run = EntryAdd },
            ["entry_set"] = new Command { Write = true, Summary = "Change a line. Args: database, conversation, entry, text?, menuText?, actor?, emotion?, title?, passthrough?, hub?, sequence? (Name(args)@seconds; …), lines? (replaces the more lines), locale?", Run = EntrySet },
            ["entry_remove"] = new Command { Write = true, Summary = "Delete a line (and links to it). Args: database, conversation, entry", Run = EntryRemove },
            ["link_add"] = new Command { Write = true, Summary = "Link two lines. Args: database, conversation, from, to, toConversation?, priority? (Low|BelowNormal|Normal|AboveNormal|High)", Run = LinkAdd },
            ["link_remove"] = new Command { Write = true, Summary = "Remove a link. Args: database, conversation, from, to, toConversation?", Run = LinkRemove },
            ["condition_add"] = new Command { Write = true, Summary = "Add a condition to a line (every way in) or to its link to 'to'. Args: database, conversation, entry, to?, variable + op? (= != < <= > >=) + value | quest + state + not?", Run = ConditionAdd },
            ["action_add"] = new Command { Write = true, Summary = "Add an action to a line. Args: database, conversation, entry, and variable + value + add? | quest + state | event + argument?", Run = ActionAdd },
            ["actor_add"] = new Command { Write = true, Summary = "Add a character. Args: database, name, player?, displayName?", Run = ActorAdd },
            ["event_add"] = new Command { Write = true, Summary = "Declare an event. Args: database, name, argumentHint?", Run = EventAdd },
            ["save"] = new Command { Write = true, Summary = "Write the database (and its text table) to disk. Args: database", Run = SaveDb },
            ["menu_run"] = new Command { Write = true, Summary = "Run an editor menu item under 'ND Dialogue/' or 'Window/ND Dialogue/'. Args: path", Run = MenuRun },
        };

        /// <summary>Entry point from the server (main thread). Never throws.</summary>
        public static string Execute(string body)
        {
            try
            {
                var request = DialogueJson.Parse(body) as Dictionary<string, object> ?? throw new BridgeException("Body must be a JSON object {\"command\", \"args\"}.");
                string name = Str(request, "command").Trim().ToLowerInvariant().Replace('-', '_');
                if (name.StartsWith("dlg_", StringComparison.Ordinal)) name = name.Substring(4);
                var args = request.TryGetValue("args", out var a) ? a as Dictionary<string, object> ?? new Dictionary<string, object>() : new Dictionary<string, object>();

                if (!s_Commands.TryGetValue(name, out var command)) throw new BridgeException($"Unknown command '{name}'. Use 'help'.");
                if (!DialogueAiAccess.IsActive) throw new BridgeException("AI access is off or locked (Project Settings → ND Dialogue → AI Bridge).");
                if (command.Write)
                {
                    if (!DialogueAiAccess.CanWrite) throw new BridgeException($"'{name}' needs the Read + Write access level.");
                    if (EditorApplication.isPlayingOrWillChangePlaymode) throw new BridgeException($"'{name}' edits assets: leave Play mode first.");
                }
                var result = command.Run(args);
                return DialogueJson.Serialize(new Dictionary<string, object> { ["ok"] = true, ["result"] = result });
            }
            catch (BridgeException e) { return DialogueBridgeServer.Error(e.Message); }
            catch (FormatException e) { return DialogueBridgeServer.Error("Bad JSON: " + e.Message); }
            catch (Exception e)
            {
                Debug.LogException(e);
                return DialogueBridgeServer.Error($"{e.GetType().Name}: {e.Message}");
            }
        }

        #region Read

        private static object Status(Dictionary<string, object> args) => new Dictionary<string, object>
        {
            ["bridge"] = "ND Dialogue",
            ["access"] = DialogueAiAccess.Level.ToString(),
            ["unity"] = Application.unityVersion,
            ["playMode"] = EditorApplication.isPlaying,
            ["locales"] = LocaleCodes.Project.ToList(),
        };

        private static object Help(Dictionary<string, object> args) =>
            s_Commands.Select(kv => $"{kv.Key}{(kv.Value.Write ? " [write]" : string.Empty)}: {kv.Value.Summary}").ToList();

        private static object DatabaseList(Dictionary<string, object> args)
        {
            string folder = OptStr(args, "folder");
            var folders = string.IsNullOrEmpty(folder) ? null : new[] { folder };
            return AssetDatabase.FindAssets("t:DialogueDatabase", folders)
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(path => (path, db: AssetDatabase.LoadAssetAtPath<DialogueDatabase>(path)))
                .Where(x => x.db != null)
                .Select(x => (object)new Dictionary<string, object>
                {
                    ["path"] = x.path,
                    ["name"] = x.db.name,
                    ["conversations"] = x.db.conversations.Count(c => c != null),
                    ["characters"] = x.db.AllActors.Count(c => c != null),
                })
                .ToList();
        }

        private static object DatabaseGet(Dictionary<string, object> args)
        {
            var db = Database(args);
            var schema = db.Schema;
            return new Dictionary<string, object>
            {
                ["path"] = AssetDatabase.GetAssetPath(db),
                ["textTable"] = db.textTable != null ? AssetDatabase.GetAssetPath(db.textTable) : null,
                ["world"] = db.schemaSource != null ? AssetDatabase.GetAssetPath(db.schemaSource) : "(the database)",
                ["characters"] = db.AllActors.Where(a => a != null).Select(a => (object)new Dictionary<string, object>
                {
                    ["id"] = a.id,
                    ["name"] = a.name,
                    ["displayName"] = db.DisplayNameOf(a),
                    ["shared"] = db.SourceOf(a) != null ? db.SourceOf(a).name : null,
                    ["player"] = a.isPlayer,
                    ["kind"] = a.GetType().Name,
                    ["emotions"] = a.emotions.Where(e => e != null).Select(e => e.id).ToList(),
                    ["attributes"] = a.AttributeDefinitions.Where(x => x != null).Select(x => $"{x.name}: {x.initialValue.type}").ToList(),
                }).ToList(),
                ["variables"] = schema.VariableNames.Distinct().OrderBy(n => n)
                    .Select(n => schema.TryGetVariableType(n, out var t) ? $"{n}: {t}" : n).ToList(),
                ["quests"] = schema.QuestNames.Distinct().OrderBy(n => n).ToList(),
                ["events"] = db.events.Where(e => e != null).Select(e => string.IsNullOrEmpty(e.argumentHint) ? e.name : $"{e.name}({e.argumentHint})").ToList(),
                ["conversations"] = db.conversations.Where(c => c != null)
                    .Select(c => (object)new Dictionary<string, object> { ["id"] = c.id, ["title"] = c.title, ["lines"] = c.entries.Count - 1 }).ToList(),
            };
        }

        private static object ConversationGet(Dictionary<string, object> args)
        {
            var db = Database(args);
            var conversation = Conversation(db, args);
            string locale = Locale(args);
            if (OptStr(args, "format") == "json") return ConversationJson(db, conversation, locale);
            return Outline(db, conversation, locale);
        }

        /// <summary>Compact, readable view: one block per line with its speaker, text, conditions, actions and links.</summary>
        public static string Outline(DialogueDatabase db, Conversation conversation, string locale)
        {
            var sb = new StringBuilder();
            sb.Append($"Conversation \"{conversation.title}\" (#{conversation.id}) — {conversation.entries.Count - 1} lines\n");
            foreach (var entry in conversation.entries.Where(e => e != null).OrderBy(e => e.id))
            {
                if (entry.id == global::ND.Dialogue.Conversation.StartEntryId) sb.Append("#0 START");
                else
                {
                    var actor = db.FindActor(entry.actorId);
                    sb.Append($"#{entry.id} {(actor != null ? actor.name : "?")}");
                    if (!string.IsNullOrEmpty(entry.emotion)) sb.Append($" [{entry.emotion}]");
                    if (entry.IsHub) sb.Append(" (hub)");
                    else if (entry.isPassthrough) sb.Append(" (routing)");
                    else sb.Append($": \"{DialogueTextEditing.Read(entry.text, locale) ?? string.Empty}\"");
                    for (int i = 1; i < entry.LineCount; i++)
                        sb.Append($"\n    + [{entry.LineEmotion(i)}] \"{DialogueTextEditing.Read(entry.LineText(i), locale) ?? string.Empty}\"");
                    if (!string.IsNullOrEmpty(entry.title)) sb.Append($"  «{entry.title}»");
                }
                sb.Append('\n');
                if (entry.conditions.Count > 0) sb.Append("    if: ").Append(string.Join(" AND ", entry.conditions.Select(c => c?.Describe() ?? "(empty)"))).Append('\n');
                if (entry.actions.Count > 0) sb.Append("    do: ").Append(string.Join("; ", entry.actions.Select(a => a?.Describe() ?? "(empty)"))).Append('\n');
                foreach (var link in entry.links)
                {
                    string where = link.conversationId == conversation.id ? string.Empty : $"{db.FindConversation(link.conversationId)?.title ?? "?"} ";
                    sb.Append($"    → {where}#{link.entryId}");
                    if (link.priority != LinkPriority.Normal) sb.Append($" ({link.priority})");
                    if (link.conditions.Count > 0) sb.Append(" if ").Append(string.Join(" AND ", link.conditions.Select(c => c?.Describe() ?? "(empty)")));
                    sb.Append('\n');
                }
            }
            return sb.ToString();
        }

        private static object ConversationJson(DialogueDatabase db, Conversation conversation, string locale) => new Dictionary<string, object>
        {
            ["id"] = conversation.id,
            ["title"] = conversation.title,
            ["entries"] = conversation.entries.Where(e => e != null).Select(e => (object)new Dictionary<string, object>
            {
                ["id"] = e.id,
                ["actor"] = db.FindActor(e.actorId)?.name,
                ["emotion"] = e.emotion,
                ["title"] = e.title,
                ["passthrough"] = e.isPassthrough,
                ["hub"] = e.IsHub,
                ["sequence"] = e.sequence,
                ["text"] = DialogueTextEditing.Read(e.text, locale),
                ["localized"] = e.text.IsLocalized,
                ["conditions"] = e.conditions.Select(c => c?.Describe()).ToList(),
                ["actions"] = e.actions.Select(a => a?.Describe()).ToList(),
                ["links"] = e.links.Select(l => (object)new Dictionary<string, object>
                {
                    ["conversation"] = l.conversationId,
                    ["entry"] = l.entryId,
                    ["priority"] = l.priority.ToString(),
                    ["conditions"] = l.conditions.Select(c => c?.Describe()).ToList(),
                }).ToList(),
            }).ToList(),
        };

        private static object ValidateDb(Dictionary<string, object> args) => DialoguePluginRegistry.Validate(Database(args));

        #endregion

        #region Test chat

        /// <summary>Headless sessions per database (when no Dialogue Editor shows it).</summary>
        private static readonly Dictionary<DialogueDatabase, DialogueTestSession> s_TestSessions = new Dictionary<DialogueDatabase, DialogueTestSession>();

        /// <summary>The open Dialogue Editor's test chat for <paramref name="db"/> (switched to the Test section), or null.</summary>
        private static DialogueTestPanel LivePanel(DialogueDatabase db)
        {
            var window = Resources.FindObjectsOfTypeAll<DialogueEditorWindow>().FirstOrDefault(w => w.Database == db && w.TestPanel != null);
            if (window == null) return null;
            if (window.CurrentTab != DialogueDatabasePanel.TestTab) window.ShowTab(DialogueDatabasePanel.TestTab);
            return window.TestPanel;
        }

        private static DialogueTestSession Session(DialogueDatabase db, string locale)
        {
            var panel = LivePanel(db);
            if (panel != null) return panel.Session;
            if (!s_TestSessions.TryGetValue(db, out var session) || session == null)
                s_TestSessions[db] = session = new DialogueTestSession(db, locale);
            if (locale != null) session.Locale = locale;
            return session;
        }

        /// <summary>Runs one test step on the live chat (so the user sees it) or the headless session, and reports it.</summary>
        private static Dictionary<string, object> TestStep(DialogueDatabase db, Dictionary<string, object> args, Func<DialogueTestPanel, bool> live, Func<DialogueTestSession, List<DialogueTestSession.Item>> headless)
        {
            var panel = LivePanel(db);
            var session = panel != null ? panel.Session : Session(db, OptStr(args, "locale") ?? Locale(args));
            if (session == null) throw new BridgeException("The Dialogue Editor has no test session for this database.");
            int before = session.Transcript.Count;
            bool ok = panel != null ? live(panel) : headless(session) != null;
            session = panel != null ? panel.Session : session; // a reset replaces the panel's session
            if (!ok) throw new BridgeException(session.Error ?? $"Not possible now (state: {session.Runner?.State.ToString() ?? "not started"}).");
            var shown = session.Transcript.Skip(Math.Min(before, session.Transcript.Count)).ToList();
            return Report(session, shown, panel != null, OptBool(args, "values"));
        }

        private static Dictionary<string, object> Report(DialogueTestSession session, List<DialogueTestSession.Item> shown, bool live, bool values)
        {
            var runner = session.Runner;
            var result = new Dictionary<string, object>
            {
                ["shown"] = DialogueTestSession.Format(shown),
                ["state"] = runner?.State.ToString() ?? "NotStarted",
                ["conversation"] = runner?.CurrentConversation?.title,
                ["entry"] = runner != null && runner.IsRunning ? (object)runner.CurrentEntry?.id : null,
                ["choices"] = session.ChoiceLabels(),
                ["where"] = live ? "Dialogue Editor › Test Dialogue (live)" : "headless (no Dialogue Editor open on this database)",
            };
            if (values) result["values"] = session.Values().ToDictionary(kv => kv.Key, kv => (object)kv.Value);
            return result;
        }

        private static object TestStart(Dictionary<string, object> args)
        {
            var db = Database(args);
            var conversation = Conversation(db, args);
            int entry = args.ContainsKey("entry") ? Int(args, "entry") : global::ND.Dialogue.Conversation.StartEntryId;
            if (OptBool(args, "reset")) TestReset(args);
            return TestStep(db, args, p => p.StartAt(conversation.id, entry), s => s.Start(conversation, entry));
        }

        private static object TestContinue(Dictionary<string, object> args) =>
            TestStep(Database(args), args, p => p.Continue(), s => s.Continue());

        private static object TestChoose(Dictionary<string, object> args)
        {
            int index = Int(args, "choice") - 1;
            return TestStep(Database(args), args, p => p.Choose(index), s => s.Choose(index));
        }

        private static object TestSet(Dictionary<string, object> args)
        {
            string name = Str(args, "name");
            string value = Str(args, "value");
            return TestStep(Database(args), args, p => p.SetValue(name, value), s => s.SetValue(name, value));
        }

        private static object TestState(Dictionary<string, object> args)
        {
            var db = Database(args);
            var panel = LivePanel(db);
            var session = panel != null ? panel.Session : Session(db, Locale(args));
            var report = Report(session, new List<DialogueTestSession.Item>(), panel != null, true);
            report.Remove("shown");
            report["transcript"] = DialogueTestSession.Format(session.Transcript);
            return report;
        }

        private static object TestReset(Dictionary<string, object> args)
        {
            var db = Database(args);
            var panel = LivePanel(db);
            if (panel != null) panel.ResetWorld();
            else Session(db, Locale(args)).ResetWorld();
            return new Dictionary<string, object> { ["reset"] = true };
        }

        private static object TestRun(Dictionary<string, object> args)
        {
            var db = Database(args);
            var conversation = Conversation(db, args);
            int entry = args.ContainsKey("entry") ? Int(args, "entry") : global::ND.Dialogue.Conversation.StartEntryId;
            var choices = args.TryGetValue("choices", out var c) && c is List<object> list ? list.Select(x => Convert.ToInt32(x, CultureInfo.InvariantCulture)).ToList() : new List<int>();
            int maxSteps = args.ContainsKey("maxSteps") ? Int(args, "maxSteps") : 200;
            if (!args.ContainsKey("reset") || OptBool(args, "reset")) TestReset(args);

            var panel = LivePanel(db);
            var session = panel != null ? panel.Session : Session(db, Locale(args));
            int before = session.Transcript.Count;
            bool ok = panel != null ? panel.StartAt(conversation.id, entry) : session.Start(conversation, entry) != null;
            if (!ok) throw new BridgeException(session.Error ?? "Could not start.");
            int used = 0, steps = 0;
            string stoppedBecause = "the conversation ended";
            while (session.Runner != null && session.Runner.IsRunning)
            {
                if (++steps > maxSteps) { stoppedBecause = $"maxSteps ({maxSteps}) reached"; break; }
                if (session.Runner.State == DialogueRunnerState.ShowingLine)
                {
                    if (panel != null) panel.Continue(); else session.Continue();
                    continue;
                }
                if (used >= choices.Count) { stoppedBecause = "a choice with no answer in 'choices'"; break; }
                int pick = choices[used++] - 1;
                bool picked = panel != null ? panel.Choose(pick) : session.Choose(pick) != null;
                if (!picked) { stoppedBecause = $"choice {pick + 1} does not exist ({session.Runner.CurrentChoices.Count} offered)"; break; }
            }
            var report = Report(session, session.Transcript.Skip(before).ToList(), panel != null, true);
            report["stopped"] = stoppedBecause;
            report["choicesUsed"] = used;
            return report;
        }

        #endregion

        #region Write

        private static object ConversationCreate(Dictionary<string, object> args)
        {
            var db = Database(args);
            string title = Str(args, "title").Trim();
            if (db.FindConversation(title) != null) throw new BridgeException($"A conversation named '{title}' already exists.");
            Conversation created = null;
            Edit(db, "Add Conversation", () => created = db.AddConversation(title));
            return new Dictionary<string, object> { ["id"] = created.id, ["title"] = created.title };
        }

        private static object EntryAdd(Dictionary<string, object> args)
        {
            var db = Database(args);
            var conversation = Conversation(db, args);
            var actor = Actor(db, Str(args, "actor"));
            DialogueEntry from = args.ContainsKey("from") ? Entry(conversation, Int(args, "from")) : null;
            string locale = Locale(args);
            DialogueEntry created = null;
            Edit(db, "Add Dialogue Line", () =>
            {
                created = conversation.AddEntry(actor.id, OptStr(args, "title"));
                created.emotion = OptStr(args, "emotion");
                created.isHub = OptBool(args, "hub");
                created.isPassthrough = OptBool(args, "passthrough") || created.isHub;
                created.position = from != null ? from.position + new Vector2(280f, 40f * from.links.Count) : new Vector2(280f * conversation.entries.Count, 0f);
                if (args.ContainsKey("text")) DialogueTextEditing.Write(db, conversation, created, false, locale, Str(args, "text"));
                if (from != null) conversation.Link(from, created);
                if (args.ContainsKey("lines")) SetMoreLines(created, args["lines"]);
            });
            return new Dictionary<string, object> { ["id"] = created.id };
        }

        private static object EntrySet(Dictionary<string, object> args)
        {
            var db = Database(args);
            var conversation = Conversation(db, args);
            var entry = Entry(conversation, Int(args, "entry"));
            if (entry.id == global::ND.Dialogue.Conversation.StartEntryId) throw new BridgeException("START has no text, speaker or emotion.");
            var actor = args.ContainsKey("actor") ? Actor(db, Str(args, "actor")) : null;
            string locale = Locale(args);
            Edit(db, "Edit Dialogue Line", () =>
            {
                if (actor != null) entry.actorId = actor.id;
                if (args.ContainsKey("emotion")) entry.emotion = OptStr(args, "emotion");
                if (args.ContainsKey("title")) entry.title = OptStr(args, "title");
                if (args.ContainsKey("passthrough")) entry.isPassthrough = OptBool(args, "passthrough");
                if (args.ContainsKey("hub")) { entry.isHub = OptBool(args, "hub"); if (entry.isHub) entry.isPassthrough = true; }
                if (args.ContainsKey("text")) DialogueTextEditing.Write(db, conversation, entry, false, locale, Str(args, "text"));
                if (args.ContainsKey("menuText")) DialogueTextEditing.Write(db, conversation, entry, true, locale, Str(args, "menuText"));
                if (args.ContainsKey("sequence")) entry.sequence = OptStr(args, "sequence");
                if (args.ContainsKey("lines")) SetMoreLines(entry, args["lines"]);
            });
            return new Dictionary<string, object> { ["id"] = entry.id };
        }

        private static object EntryRemove(Dictionary<string, object> args)
        {
            var db = Database(args);
            var conversation = Conversation(db, args);
            int id = Int(args, "entry");
            Entry(conversation, id);
            if (id == global::ND.Dialogue.Conversation.StartEntryId) throw new BridgeException("START cannot be removed.");
            bool removed = false;
            Edit(db, "Delete Dialogue Line", () => removed = db.RemoveEntry(conversation.id, id));
            return new Dictionary<string, object> { ["removed"] = removed };
        }

        private static object LinkAdd(Dictionary<string, object> args)
        {
            var db = Database(args);
            var conversation = Conversation(db, args);
            var from = Entry(conversation, Int(args, "from"));
            var targetConversation = args.ContainsKey("toConversation") ? ConversationBy(db, args["toConversation"]) : conversation;
            var to = Entry(targetConversation, Int(args, "to"));
            if (to.id == global::ND.Dialogue.Conversation.StartEntryId) throw new BridgeException("START cannot be a link target.");
            if (from.links.Any(l => l.conversationId == targetConversation.id && l.entryId == to.id)) throw new BridgeException("That link already exists.");
            var priority = ParsePriority(OptStr(args, "priority"));
            Edit(db, "Link Dialogue Lines", () => from.links.Add(new DialogueLink { conversationId = targetConversation.id, entryId = to.id, priority = priority }));
            return new Dictionary<string, object> { ["from"] = from.id, ["to"] = to.id, ["priority"] = priority.ToString() };
        }

        private static object LinkRemove(Dictionary<string, object> args)
        {
            var db = Database(args);
            var conversation = Conversation(db, args);
            var from = Entry(conversation, Int(args, "from"));
            int toConversation = args.ContainsKey("toConversation") ? ConversationBy(db, args["toConversation"]).id : conversation.id;
            int to = Int(args, "to");
            int removed = 0;
            Edit(db, "Remove Dialogue Link", () => removed = from.links.RemoveAll(l => l.conversationId == toConversation && l.entryId == to));
            if (removed == 0) throw new BridgeException($"#{from.id} has no link to #{to}.");
            return new Dictionary<string, object> { ["removed"] = removed };
        }

        private static object ConditionAdd(Dictionary<string, object> args)
        {
            var db = Database(args);
            var conversation = Conversation(db, args);
            var entry = Entry(conversation, Int(args, "entry"));
            DialogueCondition condition;
            if (args.ContainsKey("variable"))
            {
                string variable = Str(args, "variable");
                if (!db.Schema.TryGetVariableType(variable, out _)) throw new BridgeException($"Unknown variable '{variable}'. See database_get.");
                condition = new VariableCondition { variable = variable, op = ParseOp(OptStr(args, "op") ?? "="), value = Str(args, "value") };
            }
            else if (args.ContainsKey("quest"))
            {
                string quest = Str(args, "quest");
                if (!db.Schema.HasQuest(quest)) throw new BridgeException($"Unknown quest '{quest}'. See database_get.");
                condition = new QuestStateCondition { quest = quest, state = ParseEnum<QuestState>(Str(args, "state")), op = OptBool(args, "not") ? CompareOp.NotEqual : CompareOp.Equal };
            }
            else throw new BridgeException("Give 'variable' (+ op, value) or 'quest' (+ state).");

            if (args.ContainsKey("to"))
            {
                int to = Int(args, "to");
                var link = entry.links.FirstOrDefault(l => l.conversationId == conversation.id && l.entryId == to) ?? throw new BridgeException($"#{entry.id} has no link to #{to}.");
                Edit(db, "Add Link Condition", () => link.conditions.Add(condition));
                return new Dictionary<string, object> { ["on"] = $"link #{entry.id} → #{to}", ["condition"] = condition.Describe() };
            }
            Edit(db, "Add Entry Condition", () => entry.conditions.Add(condition));
            return new Dictionary<string, object> { ["on"] = $"entry #{entry.id}", ["condition"] = condition.Describe() };
        }

        private static object ActionAdd(Dictionary<string, object> args)
        {
            var db = Database(args);
            var conversation = Conversation(db, args);
            var entry = Entry(conversation, Int(args, "entry"));
            DialogueAction action;
            if (args.ContainsKey("variable"))
            {
                string variable = Str(args, "variable");
                if (!db.Schema.TryGetVariableType(variable, out _)) throw new BridgeException($"Unknown variable '{variable}'. See database_get.");
                action = new SetVariableAction { variable = variable, value = Str(args, "value"), mode = OptBool(args, "add") ? SetVariableAction.Mode.Add : SetVariableAction.Mode.Set };
            }
            else if (args.ContainsKey("quest"))
            {
                string quest = Str(args, "quest");
                if (!db.Schema.HasQuest(quest)) throw new BridgeException($"Unknown quest '{quest}'. See database_get.");
                action = new SetQuestStateAction { quest = quest, state = ParseEnum<QuestState>(Str(args, "state")) };
            }
            else if (args.ContainsKey("event")) action = new RaiseEventAction { eventName = Str(args, "event"), argument = OptStr(args, "argument") };
            else throw new BridgeException("Give 'variable' (+ value, add?), 'quest' (+ state) or 'event' (+ argument?).");
            Edit(db, "Add Dialogue Action", () => entry.actions.Add(action));
            return new Dictionary<string, object> { ["entry"] = entry.id, ["action"] = action.Describe() };
        }

        private static object ActorAdd(Dictionary<string, object> args)
        {
            var db = Database(args);
            string name = Str(args, "name").Trim();
            if (name.IndexOf(global::ND.Dialogue.Actor.AttributeSeparator) >= 0) throw new BridgeException("A character name cannot contain '.'.");
            if (db.FindActor(name) != null) throw new BridgeException($"A character named '{name}' already exists.");
            Actor created = null;
            Edit(db, "Add Character", () =>
            {
                created = db.AddActor(name, OptBool(args, "player"));
                if (args.ContainsKey("displayName")) created.displayName.Text = Str(args, "displayName");
                created.emotions.Add(new ActorEmotion { id = created.defaultEmotion });
            });
            return new Dictionary<string, object> { ["id"] = created.id, ["name"] = created.name };
        }

        private static object EventAdd(Dictionary<string, object> args)
        {
            var db = Database(args);
            string name = Str(args, "name").Trim();
            if (db.FindEvent(name) != null) throw new BridgeException($"Event '{name}' is already declared.");
            Edit(db, "Declare Dialogue Event", () => db.events.Add(new EventDefinition { name = name, argumentHint = OptStr(args, "argumentHint") }));
            return new Dictionary<string, object> { ["event"] = name };
        }

        private static object SaveDb(Dictionary<string, object> args)
        {
            var db = Database(args);
            AssetDatabase.SaveAssetIfDirty(db);
            if (db.textTable != null) AssetDatabase.SaveAssetIfDirty(db.textTable);
            return new Dictionary<string, object> { ["saved"] = AssetDatabase.GetAssetPath(db) };
        }

        private static object MenuRun(Dictionary<string, object> args)
        {
            string path = Str(args, "path").Trim();
            if (!MenuPrefixes.Any(p => path.StartsWith(p, StringComparison.Ordinal)))
                throw new BridgeException($"Only menu items under {string.Join(" or ", MenuPrefixes.Select(p => $"'{p}'"))} can be run (got '{path}').");
            if (!EditorApplication.ExecuteMenuItem(path)) throw new BridgeException($"Menu item '{path}' does not exist or is disabled.");
            return new Dictionary<string, object> { ["ran"] = path };
        }

        /// <summary>Replaces the entry's further lines: each item is "text" or {"text": "...", "emotion": "..."}.</summary>
        private static void SetMoreLines(DialogueEntry entry, object value)
        {
            if (!(value is List<object> items)) throw new BridgeException("'lines' must be an array of \"text\" or {\"text\", \"emotion\"}.");
            entry.moreLines.Clear();
            foreach (var item in items)
            {
                var page = new DialoguePage();
                if (item is Dictionary<string, object> obj)
                {
                    page.emotion = OptStr(obj, "emotion");
                    page.text.Text = OptStr(obj, "text") ?? string.Empty;
                }
                else page.text.Text = item?.ToString() ?? string.Empty;
                entry.moreLines.Add(page);
            }
        }

        /// <summary>One undo step, then every open editor showing <paramref name="db"/> redraws.</summary>
        private static void Edit(DialogueDatabase db, string label, Action change)
        {
            Undo.RecordObject(db, $"{label} (AI bridge)");
            if (db.textTable != null) Undo.RecordObject(db.textTable, $"{label} (AI bridge)");
            change();
            foreach (var c in db.conversations) c?.InvalidateCache();
            EditorUtility.SetDirty(db);
            if (db.textTable != null) EditorUtility.SetDirty(db.textTable);
            foreach (var window in Resources.FindObjectsOfTypeAll<DialogueEditorWindow>())
                if (window.Database == db) window.NotifyDatabaseChanged();
        }

        #endregion

        #region Arguments

        private static DialogueDatabase Database(Dictionary<string, object> args)
        {
            string key = Str(args, "database").Trim();
            var byPath = AssetDatabase.LoadAssetAtPath<DialogueDatabase>(key);
            if (byPath != null) return byPath;
            var matches = AssetDatabase.FindAssets("t:DialogueDatabase").Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<DialogueDatabase>).Where(d => d != null && d.name == key).ToList();
            if (matches.Count == 1) return matches[0];
            if (matches.Count > 1) throw new BridgeException($"Several databases are named '{key}': use the asset path.");
            throw new BridgeException($"No Dialogue Database '{key}' (name or Assets/… path). Use database_list.");
        }

        private static Conversation Conversation(DialogueDatabase db, Dictionary<string, object> args) =>
            args.TryGetValue("conversation", out var value) ? ConversationBy(db, value) : throw new BridgeException("Missing argument 'conversation' (id or title).");

        private static Conversation ConversationBy(DialogueDatabase db, object value)
        {
            var found = value is double d ? db.FindConversation((int)d)
                : int.TryParse(value?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int id) ? db.FindConversation(id) ?? db.FindConversation(value.ToString())
                : db.FindConversation(value?.ToString());
            return found ?? throw new BridgeException($"No conversation '{value}' in '{db.name}'.");
        }

        private static DialogueEntry Entry(Conversation conversation, int id) =>
            conversation.FindEntry(id) ?? throw new BridgeException($"'{conversation.title}' has no entry #{id}.");

        private static Actor Actor(DialogueDatabase db, string key)
        {
            var actor = int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id) ? db.FindActor(id) : db.FindActor(key);
            return actor ?? throw new BridgeException($"No character '{key}' in '{db.name}'. Use database_get.");
        }

        private static string Locale(Dictionary<string, object> args) => OptStr(args, "locale") ?? LocaleCodes.Project.FirstOrDefault() ?? "en";

        private static LinkPriority ParsePriority(string text) => string.IsNullOrEmpty(text) ? LinkPriority.Normal : ParseEnum<LinkPriority>(text);

        private static T ParseEnum<T>(string text) where T : struct =>
            Enum.TryParse(text, true, out T value) ? value : throw new BridgeException($"'{text}' is not one of {string.Join(", ", Enum.GetNames(typeof(T)))}.");

        private static CompareOp ParseOp(string op)
        {
            switch (op.Trim())
            {
                case "=": case "==": return CompareOp.Equal;
                case "!=": case "≠": return CompareOp.NotEqual;
                case "<": return CompareOp.Less;
                case "<=": case "≤": return CompareOp.LessOrEqual;
                case ">": return CompareOp.Greater;
                case ">=": case "≥": return CompareOp.GreaterOrEqual;
                default: return ParseEnum<CompareOp>(op);
            }
        }

        private static string Str(Dictionary<string, object> args, string name)
        {
            if (!args.TryGetValue(name, out var value) || value == null) throw new BridgeException($"Missing argument '{name}'.");
            return value is double d ? d.ToString(CultureInfo.InvariantCulture) : value is bool b ? (b ? "true" : "false") : value.ToString();
        }

        private static string OptStr(Dictionary<string, object> args, string name) => args.ContainsKey(name) && args[name] != null ? Str(args, name) : null;

        private static bool OptBool(Dictionary<string, object> args, string name) =>
            args.TryGetValue(name, out var value) && (value is bool b ? b : bool.TryParse(value?.ToString(), out bool parsed) && parsed);

        private static int Int(Dictionary<string, object> args, string name)
        {
            if (!args.TryGetValue(name, out var value) || value == null) throw new BridgeException($"Missing argument '{name}'.");
            if (value is double d) return (int)d;
            if (int.TryParse(value.ToString().TrimStart('#'), NumberStyles.Integer, CultureInfo.InvariantCulture, out int i)) return i;
            throw new BridgeException($"'{name}' must be a number (got '{value}').");
        }

        #endregion
    }
}
