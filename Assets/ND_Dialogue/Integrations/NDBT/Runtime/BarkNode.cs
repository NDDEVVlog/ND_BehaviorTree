using ND_BehaviorTree;
using UnityEngine;

namespace ND.Dialogue.NDBT
{
    /// <summary>
    /// Behavior tree action: the agent barks (one line in a bubble, see <see cref="DialogueBarker"/>). Succeeds when a
    /// line was said, fails when the barker is cooling down, the player is in a conversation, or no line is valid.
    /// <para>The barker is a <see cref="DialogueBarker"/> on the agent (the runner's GameObject): the one whose
    /// conversation is <see cref="conversation"/> (a character may have several, one per kind of bark), else the first.
    /// A tree asset cannot reference scene objects, so the node finds it there once and logs when it is missing.</para>
    /// </summary>
    [NodeDescription("The agent says one line of a bark conversation in its bubble.", "Needs a Dialogue Barker on the agent (the one with this conversation, else the first). Empty Conversation = that barker's own. Fails when nothing was said (cooldown, talking, no valid line).")]
    [NodeInfo("Bark", "Action/Dialogue/Bark", true, false)]
    public class BarkNode : ActionNode
    {
        [Tooltip("Bark conversation title. Empty = the first barker's own conversation.")]
        public string conversation;

        [System.NonSerialized] private DialogueBarker m_Barker;

        protected override Status OnProcess()
        {
            if (m_Barker == null)
            {
                var agent = GetOwnerTreeGameObject();
                var barkers = agent != null ? agent.GetComponents<DialogueBarker>() : new DialogueBarker[0];
                foreach (var barker in barkers)
                    if (string.IsNullOrEmpty(conversation) || barker.Conversation == conversation) { m_Barker = barker; break; }
                if (m_Barker == null && barkers.Length > 0) m_Barker = barkers[0]; // it barks the asked conversation with its own rules
                if (m_Barker == null)
                {
                    Debug.LogError($"[BarkNode] '{(agent != null ? agent.name : "no agent")}' has no Dialogue Barker: add one next to the Behavior Tree Runner.", agent);
                    return Status.Failure;
                }
            }
            return m_Barker.Bark(string.IsNullOrEmpty(conversation) ? m_Barker.Conversation : conversation) ? Status.Success : Status.Failure;
        }
    }
}
