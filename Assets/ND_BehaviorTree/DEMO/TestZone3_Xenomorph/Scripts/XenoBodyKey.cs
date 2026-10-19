using UnityEngine;

namespace ND_BehaviorTree.Demo.Xeno
{
    /// <summary>Blackboard key holding the alien's <see cref="XenoBody"/> (injected by <see cref="XenoRunner"/>).</summary>
    [KeyColor(0.85f, 0.2f, 0.2f)]
    public class XenoBodyKey : Key<XenoBody> { }
}
