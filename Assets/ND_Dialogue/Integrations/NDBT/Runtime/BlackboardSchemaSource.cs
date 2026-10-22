using ND_BehaviorTree;
using ND.Quests;
using UnityEngine;

namespace ND.Dialogue.NDBT
{
    /// <summary>
    /// Makes a behavior-tree blackboard (usually the shared "World" parent of your agents' blackboards) the world
    /// state of a <see cref="DialogueDatabase"/>: assign it to the database's Schema Source.
    /// The editor then offers the blackboard's keys in condition/action pickers; at runtime the context reads and
    /// writes the shared instance agents use (<see cref="SharedBlackboards"/>).
    /// </summary>
    [CreateAssetMenu(fileName = "BlackboardSchemaSource", menuName = "ND Dialogue/Blackboard Schema Source", order = 20)]
    public class BlackboardSchemaSource : DialogueSchemaSource
    {
        [Tooltip("The world-state blackboard asset (its keys, and its parents' keys, are the dialogue's variables).")]
        public Blackboard blackboard;

        [Tooltip("Quest state lives in int keys named <prefix><quest name>.")]
        public string questKeyPrefix = BlackboardDialogueContext.DefaultQuestPrefix;

        /// <summary>Schema over the asset (read-only use: editor pickers and validation). Empty until a blackboard is assigned.</summary>
        public override IDialogueSchema Schema =>
            blackboard != null ? new BlackboardDialogueContext(blackboard, questKeyPrefix) : Empty;

        public override IDialogueContext CreateContext(DialogueDatabase database, QuestRuntime quests = null)
        {
            if (blackboard == null)
            {
                Debug.LogError($"[BlackboardSchemaSource] '{name}' has no blackboard assigned.", this);
                return null;
            }
            return BlackboardDialogueContext.ForWorld(blackboard, questKeyPrefix, database, quests);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying && blackboard == null)
                Debug.LogWarning($"[BlackboardSchemaSource] '{name}': assign the world blackboard.", this);
        }
#endif
    }
}
