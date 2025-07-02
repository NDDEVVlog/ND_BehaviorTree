using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ND_BehaviorTree
{
    [NodeInfo("Debug_Log", "Debug/DebugLog")]
    
    public class DebugLogNode : Node
    {   
        [ExposeProperty()]
        public string Log;

        public int number;

        protected override Status OnProcess()
        {
            throw new System.NotImplementedException();
        }
    }
}
