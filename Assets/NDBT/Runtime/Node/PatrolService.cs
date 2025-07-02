using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ND_BehaviorTree
{
    // Example Service
    [NodeInfo("Patrol Service", "Services/Patrol", true, true, iconPath:"Assets/NDBT/Icons/antivirus.png",isChildOnly: true)]
    public class PatrolService : ServiceNode
    { /* ... your logic ... */
        protected override void OnTick()
        {
            throw new System.NotImplementedException();
        }
    }
}
