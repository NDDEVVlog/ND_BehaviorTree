using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ND_BehaviorTree
{
    // Example Service (placeholder: no behaviour yet). Kept because assets (e.g. Wolf.asset) reference it.
    [NodeDescription("Placeholder service with no behaviour (kept so old assets load).", "Logs an error once when it ticks. Remove it, or implement OnTick.")]
    [NodeInfo("Patrol Service", "Services/Patrol", true, true, iconPath:"Assets/ND_BehaviorTree/NDBT/Icons/antivirus.png",isChildOnly: true)]
    public class PatrolService : ServiceNode
    {
        [System.NonSerialized] private bool m_NotImplementedLogged;

        protected override void OnTick()
        {
            // Used to throw NotImplementedException, which broke every node this service was attached to.
            if (m_NotImplementedLogged) return;
            m_NotImplementedLogged = true;
            Debug.LogError($"[PatrolService] '{name}' is a placeholder with no behaviour; it does nothing. Remove it or implement OnTick. (Logged once.)", this);
        }
    }
}
