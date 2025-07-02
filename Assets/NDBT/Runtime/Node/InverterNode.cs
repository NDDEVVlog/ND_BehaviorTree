using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ND_BehaviorTree
{
    // Example Decorator
    [NodeInfo("Inverter", "Decorators/Inverter", true, true,iconPath:"Assets/NDBT/Icons/antivirus.png", isChildOnly: true)]
    public class InverterNode : DecoratorNode
    { /* ... your logic ... */
        protected override Status OnProcess()
        {
            throw new System.NotImplementedException();
        }
    }
}
