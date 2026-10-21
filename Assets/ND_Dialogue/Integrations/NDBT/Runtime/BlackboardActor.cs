using System;
using System.Collections.Generic;
using ND_BehaviorTree;
using UnityEngine;

namespace ND.Dialogue.NDBT
{
    /// <summary>
    /// A character whose attributes are the keys of a behavior-tree blackboard (usually the blackboard of the NPC's
    /// tree): <c>Guard.trust</c> in a condition reads the key <c>trust</c>. Supported key types: bool, int, float, string.
    /// <para>At runtime the values come from the NPC agent's own blackboard when a <see cref="DialogueActorAgent"/>
    /// binds it (dialogue and AI then share them); otherwise from one shared play-session instance of
    /// <see cref="blackboard"/>.</para>
    /// </summary>
    [Serializable]
    public class BlackboardActor : Actor
    {
        [Tooltip("This character's attributes: its keys (bool / int / float / string / Vector2 / Vector3 / Color / object). Use the blackboard of the NPC's behavior tree.")]
        public Blackboard blackboard;

        public override string EditorNote => blackboard == null
            ? "Assign the character's blackboard: its keys become this character's attributes."
            : $"Attributes come from the keys of '{blackboard.name}' (the Attributes list is not used).";

        /// <summary>The keys of <see cref="blackboard"/> (own keys only; its parent is the world, not the character).</summary>
        public override IEnumerable<VariableDefinition> AttributeDefinitions
        {
            get
            {
                if (blackboard == null) yield break;
                foreach (var key in blackboard.keys)
                    if (key != null && BlackboardDialogueContext.TryReadValue(key, out var value))
                        yield return new VariableDefinition { name = key.keyName, initialValue = value };
            }
        }

        /// <summary>The blackboard dialogue reads/writes at runtime: the bound agent's, else the shared instance of <see cref="blackboard"/>.</summary>
        public Blackboard RuntimeBlackboard()
        {
            if (DialogueActorBoards.TryGet(name, out var bound)) return bound;
            return blackboard != null ? SharedBlackboards.GetRuntime(blackboard) : null;
        }
    }

    /// <summary>Runtime blackboards bound to character names (by <see cref="DialogueActorAgent"/> or code).</summary>
    public static class DialogueActorBoards
    {
        private static readonly Dictionary<string, Blackboard> s_Boards = new Dictionary<string, Blackboard>();

        /// <summary>Dialogue about <paramref name="actorName"/> now reads/writes <paramref name="runtimeBlackboard"/> (e.g. an agent's RuntimeTree.blackboard).</summary>
        public static void Bind(string actorName, Blackboard runtimeBlackboard)
        {
            if (string.IsNullOrEmpty(actorName) || runtimeBlackboard == null)
            {
                Debug.LogError($"[DialogueActorBoards] Bind rejected: actor='{actorName}', blackboard={(runtimeBlackboard != null ? runtimeBlackboard.name : "null")}.");
                return;
            }
            if (s_Boards.TryGetValue(actorName, out var existing) && existing != null && existing != runtimeBlackboard)
                Debug.LogWarning($"[DialogueActorBoards] '{actorName}' was bound to '{existing.name}'; now '{runtimeBlackboard.name}' (one agent per character).");
            s_Boards[actorName] = runtimeBlackboard;
        }

        /// <summary>Unbinds only if <paramref name="runtimeBlackboard"/> is the one bound (a newer binding is kept).</summary>
        public static void Unbind(string actorName, Blackboard runtimeBlackboard)
        {
            if (actorName != null && s_Boards.TryGetValue(actorName, out var bound) && bound == runtimeBlackboard) s_Boards.Remove(actorName);
        }

        public static bool TryGet(string actorName, out Blackboard runtimeBlackboard)
        {
            runtimeBlackboard = null;
            return actorName != null && s_Boards.TryGetValue(actorName, out runtimeBlackboard) && runtimeBlackboard != null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void Clear() => s_Boards.Clear();
    }
}
