using UnityEngine;

namespace ND.Dialogue.NDBT
{
    /// <summary>
    /// Put next to an NPC's BehaviorTreeRunner: dialogue about this character (its <see cref="BlackboardActor"/>
    /// attributes) then reads and writes the agent's own runtime blackboard, so AI and dialogue share e.g. 'trust'.
    /// Runs after the runner's Start, which is where the runtime blackboard is created.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [AddComponentMenu("ND Dialogue/Dialogue Actor Agent")]
    public class DialogueActorAgent : MonoBehaviour, IDialogueDatabaseOwner
    {
        [SerializeField] private DialogueDatabase m_Database;
        [DialogueActor, SerializeField] private string m_ActorName;
        [SerializeField] private BehaviorTreeRunner m_Runner;

        public DialogueDatabase Database => m_Database;
        public string ActorName => m_ActorName;

        private ND_BehaviorTree.Blackboard m_Bound;

        private void Start()
        {
            if (m_Runner == null || string.IsNullOrEmpty(m_ActorName))
            {
                Debug.LogError($"[DialogueActorAgent] '{name}': assign the runner and the character.", this);
                return;
            }
            m_Bound = m_Runner.RuntimeTree != null ? m_Runner.RuntimeTree.blackboard : null;
            if (m_Bound == null)
            {
                Debug.LogError($"[DialogueActorAgent] '{name}': the runner has no runtime blackboard (no tree, or its Start has not run).", this);
                return;
            }
            DialogueActorBoards.Bind(m_ActorName, m_Bound);
        }

        private void OnDestroy()
        {
            if (m_Bound != null) DialogueActorBoards.Unbind(m_ActorName, m_Bound);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying) return;
            if (m_Runner == null) Debug.LogWarning($"[DialogueActorAgent] '{name}': assign the BehaviorTreeRunner of this NPC.", this);
            if (m_Database != null && !string.IsNullOrEmpty(m_ActorName) && !(m_Database.FindActor(m_ActorName) is BlackboardActor))
                Debug.LogWarning($"[DialogueActorAgent] '{name}': '{m_ActorName}' is not a Blackboard Actor in '{m_Database.name}', so its attributes do not live in a blackboard.", this);
        }
#endif
    }
}
