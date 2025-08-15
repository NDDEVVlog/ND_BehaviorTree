////////////////////////////////////////////////////////////////////////////
//
// FB: https://www.facebook.com/profile.php?id=100090693452227
// Github: https://github.com/NDDEVVlog/ND_BehaviorTree
//
////////////////////////////////////////////////////////////////////////////

using UnityEngine;
using ND_BehaviorTree;

[NodeInfo("SelfDestructNode", "Action/SelfDestructNode", true, false,iconPath:"Assets/ND_BehaviorTree/NDBT/Icons/boom.png")]
public class SelfDestructNode : ActionNode
{
    //BOOOMMMMMMMMMMMMMMMMM
    
    protected override Status OnProcess()
    {
        Destroy(GetOwnerTreeGameObject());
        return Status.Success;
    }
}