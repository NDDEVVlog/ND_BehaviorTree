using System.Collections.Generic;
using UnityEngine;

namespace ND.Quests
{
    /// <summary>
    /// Quest definitions, independent of any other system: a <see cref="QuestManager"/> loads them, dialogue databases
    /// reference them (their conditions and actions can name these quests), gameplay reports progress by id.
    /// </summary>
    [CreateAssetMenu(fileName = "QuestDatabase", menuName = "ND Quests/Quest Database", order = 0)]
    public class QuestDatabase : ScriptableObject
    {
        public List<QuestDefinition> quests = new List<QuestDefinition>();

        public QuestDefinition FindQuest(string questName)
        {
            foreach (var q in quests) if (q != null && q.name == questName) return q;
            return null;
        }

        /// <summary>Adds a quest named <paramref name="questName"/>; null (logged) if the name is empty or taken.</summary>
        public QuestDefinition AddQuest(string questName, QuestState initialState = QuestState.Inactive)
        {
            if (string.IsNullOrEmpty(questName) || FindQuest(questName) != null)
            {
                Debug.LogError($"[QuestDatabase] {name}: quest name '{questName}' is empty or already used.", this);
                return null;
            }
            var quest = new QuestDefinition { name = questName, initialState = initialState };
            quests.Add(quest);
            return quest;
        }

        /// <summary>Empty or duplicate names, empty slots, goals with a counter that cannot complete.</summary>
        public List<string> Validate()
        {
            var issues = new List<string>();
            var names = new HashSet<string>();
            for (int i = 0; i < quests.Count; i++)
            {
                var q = quests[i];
                if (q == null) { issues.Add($"Quest Database '{name}': empty slot {i}."); continue; }
                if (string.IsNullOrEmpty(q.name) || !names.Add(q.name)) issues.Add($"Quest Database '{name}': empty or duplicate quest name '{q.name}'.");
                if (q.goal < 0) issues.Add($"Quest '{q.name}': negative goal {q.goal}.");
                for (int s = 0; s < q.steps.Count; s++)
                {
                    var step = q.steps[s];
                    if (step == null) { issues.Add($"Quest '{q.name}': empty step slot {s + 1}."); continue; }
                    if (step.visibleWhen.Contains(null) || step.doneWhen.Contains(null)) issues.Add($"Quest '{q.name}' step {s + 1}: empty condition slot.");
                }
            }
            return issues;
        }
    }
}
