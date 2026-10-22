using System.Collections.Generic;
using UnityEngine;

namespace ND.Dialogue
{
    /// <summary>
    /// Marks the scene object that plays a dialogue character, so sequences can name it (<c>Anim(Malfoy, Blush)</c>,
    /// or <c>speaker</c> for whoever says the line).
    /// </summary>
    [AddComponentMenu("ND Dialogue/Dialogue Actor Object")]
    public class DialogueActorObject : MonoBehaviour
    {
        [DialogueActor, SerializeField] private string m_Actor;

        private static readonly Dictionary<string, DialogueActorObject> s_ByActor = new Dictionary<string, DialogueActorObject>();

        public string Actor { get => m_Actor; set { Unregister(); m_Actor = value; if (isActiveAndEnabled) Register(); } }

        /// <summary>The enabled object playing <paramref name="actorName"/>, or null.</summary>
        public static DialogueActorObject Find(string actorName) =>
            !string.IsNullOrEmpty(actorName) && s_ByActor.TryGetValue(actorName, out var found) && found != null ? found : null;

        private void OnEnable() => Register();
        private void OnDisable() => Unregister();

        private void Register()
        {
            if (string.IsNullOrEmpty(m_Actor)) return;
            if (s_ByActor.TryGetValue(m_Actor, out var other) && other != null && other != this)
                Debug.LogWarning($"[DialogueActorObject] '{name}' and '{other.name}' both play '{m_Actor}'; sequences use '{name}'.", this);
            s_ByActor[m_Actor] = this;
        }

        private void Unregister()
        {
            if (!string.IsNullOrEmpty(m_Actor) && s_ByActor.TryGetValue(m_Actor, out var current) && current == this) s_ByActor.Remove(m_Actor);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying && string.IsNullOrEmpty(m_Actor)) Debug.LogWarning($"[DialogueActorObject] '{name}': choose the character this object plays.", this);
        }
#endif
    }
}
