using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ND.Quests;
using UnityEngine;

namespace ND.Dialogue
{
    /// <summary>
    /// Captures dialogue state into a <see cref="DialogueSaveData"/> and applies it back. It works through
    /// <see cref="IDialogueContext"/> and the database's schema, so any world (the database's own variables, a
    /// behavior-tree blackboard…) is saved the same way. Applying goes through the normal setters: change events fire.
    /// </summary>
    public static class DialogueSaveSystem
    {
        private const string LogTag = "[DialogueSave]";

        public static DialogueSaveData Capture(DialogueDatabase database, IDialogueContext context, DialogueMemory memory, DialogueRunner runner = null)
        {
            if (database == null || context == null)
            {
                Debug.LogError($"{LogTag} {nameof(Capture)} rejected: database={(database != null)}, context={(context != null)}.");
                return null;
            }
            var data = new DialogueSaveData { database = database.name, savedAt = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) };
            var schema = database.Schema;
            var skippedObjects = new List<string>();
            foreach (var name in schema.VariableNames.Distinct())
            {
                if (!context.TryGetVariable(name, out var value)) continue; // the context logged why
                if (value.type == VariableType.Object) { skippedObjects.Add(name); continue; }
                data.variables.Add(new DialogueSaveData.Variable { name = name, type = value.type, value = ToText(value) });
            }
            if (skippedObjects.Count > 0)
                Debug.LogWarning($"{LogTag} Object variables are not saved (asset references): {string.Join(", ", skippedObjects)}.");
            var quests = (context as IDialogueQuestHost)?.Quests;
            if (quests != null)
            {
                // The quest runtime: every quest it knows (also from Quest Databases), with progress and tracking.
                var saved = quests.Capture();
                foreach (var q in saved.quests) data.quests.Add(new DialogueSaveData.Quest { name = q.name, state = q.state, progress = q.progress });
                data.trackedQuests.AddRange(saved.tracked);
            }
            else
            {
                foreach (var name in schema.QuestNames.Distinct())
                    if (context.TryGetQuestState(name, out var state)) data.quests.Add(new DialogueSaveData.Quest { name = name, state = state });
            }
            if (memory != null)
            {
                foreach (var (c, e) in memory.Seen) data.seen.Add(new DialogueSaveData.Seen { conversation = c, entry = e });
                foreach (var (c, e, cycle, bag) in memory.Routes) data.routes.Add(new DialogueSaveData.Route { conversation = c, entry = e, cycle = cycle, bag = bag });
            }
            if (runner != null) data.position = runner.CapturePosition();
            return data;
        }

        /// <summary>
        /// Writes the saved values into <paramref name="context"/> and <paramref name="memory"/> (cleared first). Names the
        /// database no longer declares, or whose type changed, are skipped with a warning. Returns how many were skipped.
        /// The position is not applied here (see <see cref="DialogueRunner.Resume"/>).
        /// </summary>
        public static int Apply(DialogueDatabase database, IDialogueContext context, DialogueMemory memory, DialogueSaveData data)
        {
            if (database == null || context == null || data == null)
            {
                Debug.LogError($"{LogTag} {nameof(Apply)} rejected: database={(database != null)}, context={(context != null)}, data={(data != null)}.");
                return -1;
            }
            if (data.version > DialogueSaveData.CurrentVersion)
                Debug.LogWarning($"{LogTag} The save is version {data.version}, newer than this build ({DialogueSaveData.CurrentVersion}); loading what is understood.");
            if (!string.IsNullOrEmpty(data.database) && data.database != database.name)
                Debug.LogWarning($"{LogTag} The save was made with '{data.database}', loading it into '{database.name}'.");

            int skipped = 0;
            var schema = database.Schema;
            foreach (var v in data.variables)
            {
                if (!schema.TryGetVariableType(v.name, out var type)) { Skip($"variable '{v.name}' is no longer declared"); continue; }
                if (type != v.type) { Skip($"variable '{v.name}' is {type} now, the save has {v.type}"); continue; }
                var value = v.type == VariableType.String ? VariableValue.String(v.value ?? string.Empty) : default; // strings exactly (TryParse trims)
                if (v.type != VariableType.String && !VariableValue.TryParse(v.type, v.value, out value)) { Skip($"variable '{v.name}': cannot read '{v.value}' as {v.type}"); continue; }
                if (!context.SetVariable(v.name, value)) skipped++; // the context logged why
            }
            var quests = (context as IDialogueQuestHost)?.Quests;
            if (quests != null)
            {
                // Through the runtime: progress and tracking too, and no "quest started" while loading.
                var saved = new QuestSaveData { tracked = new List<string>(data.trackedQuests) };
                foreach (var q in data.quests)
                {
                    if (!quests.Has(q.name)) { Skip($"quest '{q.name}' is no longer declared"); continue; }
                    saved.quests.Add(new QuestSaveData.Quest { name = q.name, state = q.state, progress = q.progress });
                }
                saved.tracked.RemoveAll(n => !quests.Has(n));
                quests.Apply(saved);
            }
            else
            {
                foreach (var q in data.quests)
                {
                    if (!schema.HasQuest(q.name)) { Skip($"quest '{q.name}' is no longer declared"); continue; }
                    if (!context.SetQuestState(q.name, q.state)) skipped++;
                }
            }
            if (memory != null)
            {
                memory.Clear();
                foreach (var s in data.seen)
                {
                    if (database.FindConversation(s.conversation)?.FindEntry(s.entry) == null) { Skip($"seen entry {s.conversation}#{s.entry} no longer exists"); continue; }
                    memory.MarkSeen(s.conversation, s.entry);
                }
                foreach (var r in data.routes ?? new List<DialogueSaveData.Route>()) // saves made before routes have none
                {
                    if (database.FindConversation(r.conversation)?.FindEntry(r.entry) == null) { Skip($"route state of {r.conversation}#{r.entry}: the entry no longer exists"); continue; }
                    memory.RestoreRoute(r.conversation, r.entry, r.cycle, r.bag);
                }
            }
            return skipped;

            void Skip(string reason)
            {
                skipped++;
                Debug.LogWarning($"{LogTag} {nameof(Apply)}: {reason}; skipped.");
            }
        }

        public static string ToJson(DialogueSaveData data, bool pretty = false) => JsonUtility.ToJson(data, pretty);

        /// <summary>The save in <paramref name="json"/>, or null (logged) when it is not one.</summary>
        public static DialogueSaveData FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.LogError($"{LogTag} {nameof(FromJson)} rejected: empty text.");
                return null;
            }
            try
            {
                var data = JsonUtility.FromJson<DialogueSaveData>(json);
                if (data == null) Debug.LogError($"{LogTag} {nameof(FromJson)}: the text is not a dialogue save.");
                return data;
            }
            catch (ArgumentException e)
            {
                Debug.LogError($"{LogTag} {nameof(FromJson)}: invalid JSON ({e.Message}).");
                return null;
            }
        }

        /// <summary>A value as text <see cref="VariableValue.TryParse"/> reads back exactly (invariant culture, round-trip floats).</summary>
        public static string ToText(VariableValue value)
        {
            var ci = CultureInfo.InvariantCulture;
            switch (value.type)
            {
                case VariableType.Float: return value.floatValue.ToString("R", ci);
                case VariableType.Vector2: return string.Format(ci, "{0:R},{1:R}", value.vectorValue.x, value.vectorValue.y);
                case VariableType.Vector3: return string.Format(ci, "{0:R},{1:R},{2:R}", value.vectorValue.x, value.vectorValue.y, value.vectorValue.z);
                case VariableType.Color: return string.Format(ci, "{0:R},{1:R},{2:R},{3:R}", value.colorValue.r, value.colorValue.g, value.colorValue.b, value.colorValue.a);
                default: return value.ToString();
            }
        }
    }
}
