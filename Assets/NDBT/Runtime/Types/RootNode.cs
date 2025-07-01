using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ND_BehaviorTree
{
    [NodeInfo("Root","Default/Root",false,true) ]
    public class RootNode : Node
    {
        public override string OnProcess(BehaviorTree tree)
        {
            Debug.Log("Hello Mao Fuck");
            return base.OnProcess(tree);
        }
    }
}
