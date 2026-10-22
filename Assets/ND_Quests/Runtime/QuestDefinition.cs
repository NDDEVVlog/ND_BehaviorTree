using System;
using System.Collections.Generic;
using ND.Localization;
using UnityEngine;

namespace ND.Quests
{
    public enum QuestState { Inactive, Active, Completed, Failed, Abandoned }

    /// <summary>
    /// A quest: its id, journal texts, optional goal (a counter, "Kill 20 goblins") and steps. Definitions live in a
    /// <see cref="QuestDatabase"/> (or, for older content, inside a dialogue database); the state is in a
    /// <see cref="QuestRuntime"/>.
    /// </summary>
    [Serializable]
    public class QuestDefinition
    {
        [Tooltip("Quest id: what dialogue, gameplay and saves name it by. Keep it stable.")]
        public string name;
        [Tooltip("Name shown to players in the quest log and tracker. Empty = the quest id.")]
        public LocalizableString title = new LocalizableString();
        public QuestState initialState = QuestState.Inactive;
        [Tooltip("Note for designers (not shown in game).")]
        [TextArea] public string description;

        [Tooltip("Shown in the quest log under the title.")]
        public LocalizableString summary = new LocalizableString();
        [Tooltip("Quest log group, e.g. Main or Side.")]
        public string group = "Main";
        [Tooltip("Show it in the on-screen tracker as soon as it becomes Active.")]
        public bool trackWhenActive = true;

        [Header("Goal (optional counter)")]
        [Tooltip("Progress needed (e.g. 20 for 'Kill 20 goblins'). 0 = no counter: the quest is completed by dialogue or code.")]
        [Min(0)] public int goal;
        [Tooltip("How a reported value changes the progress. Empty = Increment (add it).")]
        [SerializeReference] public IQuestProgression progression;
        [Tooltip("Complete the quest by itself when the progress reaches the goal.")]
        public bool completeAtGoal = true;

        [Tooltip("The steps listed under the quest (each shows and ticks itself by conditions).")]
        public List<QuestStep> steps = new List<QuestStep>();

        public bool HasGoal => goal > 0;
    }

    /// <summary>
    /// One line of a quest's journal ("Find the old key"): shown while <see cref="visibleWhen"/> holds and ticked once
    /// <see cref="doneWhen"/> holds. Conditions read the world (quest states, progress, and whatever the host system
    /// provides — e.g. dialogue variables), so steps need no state of their own.
    /// </summary>
    [Serializable]
    public class QuestStep
    {
        public LocalizableString text = new LocalizableString();
        [Tooltip("All must hold for the step to be listed. Empty = always listed.")]
        [SerializeReference] public List<IQuestCondition> visibleWhen = new List<IQuestCondition>();
        [Tooltip("All must hold for the step to be ticked. Empty = ticked when the quest is Completed.")]
        [SerializeReference] public List<IQuestCondition> doneWhen = new List<IQuestCondition>();
    }
}
