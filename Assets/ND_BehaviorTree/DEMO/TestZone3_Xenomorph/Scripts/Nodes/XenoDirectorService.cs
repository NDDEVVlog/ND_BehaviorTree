using UnityEngine;

namespace ND_BehaviorTree.Demo.Xeno
{
    /// <summary>Copies the director's outputs (hint, retreat request, menace, learned values) into the blackboard.</summary>
    [NodeDescription("Copies the Xeno director's outputs (hint, retreat request, menace, learned values) to the blackboard.", "The director knows where the player is but only exposes a fuzzy hint; the tree still decides from its own senses.")]
    [NodeInfo("Xeno Director Link", "Services/Xeno/Director Link", true, true, iconPath: "Assets/ND_BehaviorTree/NDBT/Icons/antivirus.png", isChildOnly: true)]
    public class XenoDirectorService : ServiceNode
    {
        [BlackboardKeyType(typeof(XenoBody))] public Key bodyKey;
        [BlackboardKeyType(typeof(Vector3))] public Key hintKey;
        [BlackboardKeyType(typeof(bool))] public Key retreatKey;
        [BlackboardKeyType(typeof(float))] public Key menaceKey;
        [BlackboardKeyType(typeof(float))] public Key hideCheckChanceKey;
        [BlackboardKeyType(typeof(float))] public Key backstageTimeKey;

        [System.NonSerialized] private bool m_Resolved;
        [System.NonSerialized] private XenoDirector m_Director;
        [System.NonSerialized] private Key<Vector3> m_Hint;
        [System.NonSerialized] private Key<bool> m_Retreat;
        [System.NonSerialized] private Key<float> m_Menace, m_HideCheckChance, m_BackstageTime;

        protected override void OnTick()
        {
            if (!m_Resolved)
            {
                m_Resolved = true;
                var body = KeyResolver.Resolve<XenoBody>(this, bodyKey)?.GetValue();
                m_Director = body != null ? body.Director : null;
                if (m_Director == null) Debug.LogError($"[XenoDirectorService] '{name}': no XenoDirector (Body key empty or XenoBody.director unassigned).", this);
                m_Hint = KeyResolver.Resolve<Vector3>(this, hintKey);
                m_Retreat = KeyResolver.Resolve<bool>(this, retreatKey);
                m_Menace = KeyResolver.Resolve<float>(this, menaceKey);
                m_HideCheckChance = KeyResolver.Resolve<float>(this, hideCheckChanceKey);
                m_BackstageTime = KeyResolver.Resolve<float>(this, backstageTimeKey);
            }
            if (m_Director == null) return;

            m_Hint?.SetValue(m_Director.Hint);
            m_Retreat?.SetValue(m_Director.RetreatRequested);
            m_Menace?.SetValue(m_Director.Menace);
            m_HideCheckChance?.SetValue(m_Director.HideCheckChance);
            m_BackstageTime?.SetValue(m_Director.BackstageTime);
        }
    }
}
