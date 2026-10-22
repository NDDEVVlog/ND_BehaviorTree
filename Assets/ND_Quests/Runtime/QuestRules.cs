using System;
using UnityEngine;

namespace ND.Quests
{
    /// <summary>What a step condition is evaluated against: the quests, plus the host's own world (e.g. a dialogue context).</summary>
    public readonly struct QuestConditionContext
    {
        public readonly QuestRuntime Quests;
        /// <summary>The host system's state (set with <see cref="QuestRuntime.ConditionWorld"/>), or null.</summary>
        public readonly object World;

        public QuestConditionContext(QuestRuntime quests, object world) { Quests = quests; World = world; }
    }

    /// <summary>
    /// A condition for quest steps. Quests come with their own (quest state, progress); other systems add theirs by
    /// implementing it (dialogue conditions do, reading <see cref="QuestConditionContext.World"/>).
    /// </summary>
    public interface IQuestCondition
    {
        bool IsMet(QuestConditionContext context);
        string Describe();
    }

    /// <summary>Holds while a quest is (or is not) in a state.</summary>
    [Serializable]
    public class QuestIsCondition : IQuestCondition
    {
        public string quest;
        public QuestState state = QuestState.Active;
        [Tooltip("Holds when the quest is NOT in that state.")]
        public bool not;

        public bool IsMet(QuestConditionContext context) =>
            context.Quests != null && context.Quests.TryGetState(quest, out var current) && (current == state) != not;

        public string Describe() => $"quest {quest} {(not ? "!=" : "==")} {state}";
    }

    /// <summary>Holds once a quest's progress reached a value.</summary>
    [Serializable]
    public class QuestProgressCondition : IQuestCondition
    {
        public string quest;
        [Min(0)] public int atLeast = 1;

        public bool IsMet(QuestConditionContext context) =>
            context.Quests != null && context.Quests.TryGetProgress(quest, out int progress) && progress >= atLeast;

        public string Describe() => $"quest {quest} progress >= {atLeast}";
    }

    /// <summary>How a reported value changes a quest's progress (the strategy of a <see cref="QuestDefinition"/>).</summary>
    public interface IQuestProgression
    {
        /// <summary>The new progress for <paramref name="reported"/>; false = the report does not count (no change).</summary>
        bool TryUpdate(int current, int reported, out int updated);
    }

    /// <summary>Adds the reported value (a delta): "Kill X", "Collect X". Values of 0 or less are refused.</summary>
    [Serializable]
    public class IncrementProgression : IQuestProgression
    {
        public bool TryUpdate(int current, int reported, out int updated)
        {
            updated = current + reported;
            return reported > 0;
        }
    }

    /// <summary>Keeps the highest reported value (an absolute value): "Reach wave X", "Score X".</summary>
    [Serializable]
    public class MaxValueProgression : IQuestProgression
    {
        public bool TryUpdate(int current, int reported, out int updated)
        {
            updated = Math.Max(current, reported);
            return updated != current;
        }
    }
}
