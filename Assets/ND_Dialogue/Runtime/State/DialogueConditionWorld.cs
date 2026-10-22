using ND.Quests;

namespace ND.Dialogue
{
    /// <summary>
    /// What dialogue conditions in quest steps read (<see cref="QuestRuntime.ConditionWorld"/>): a dialogue context
    /// plus what a runner has seen, so steps can use Entry Seen ("talked to the guard") as well as variables and quests.
    /// </summary>
    public sealed class DialogueConditionWorld : IDialogueContext, IDialogueMemory, IDialogueQuestHost
    {
        private readonly DialogueMemory m_Memory;

        public IDialogueContext Inner { get; }

        public DialogueConditionWorld(IDialogueContext inner, DialogueMemory memory)
        {
            Inner = inner;
            m_Memory = memory;
        }

        public bool TryGetVariable(string variableName, out VariableValue value) => Inner.TryGetVariable(variableName, out value);
        public bool SetVariable(string variableName, VariableValue value) => Inner.SetVariable(variableName, value);
        public bool TryGetQuestState(string questName, out QuestState state) => Inner.TryGetQuestState(questName, out state);
        public bool SetQuestState(string questName, QuestState state) => Inner.SetQuestState(questName, state);
        public void RaiseEvent(string eventName, string argument) => Inner.RaiseEvent(eventName, argument);
        public bool WasSeen(int conversationId, int entryId) => m_Memory != null && m_Memory.WasSeen(conversationId, entryId);
        /// <summary>No conversation is current for a quest step: Entry Seen conditions must name their conversation.</summary>
        public int CurrentConversationId => 0;
        public QuestRuntime Quests => (Inner as IDialogueQuestHost)?.Quests;
    }
}
