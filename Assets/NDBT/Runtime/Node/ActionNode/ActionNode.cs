using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ND_BehaviorTree
{
    public abstract class ActionNode : Node
    {
        public override void AddChild(Node child) { }
        public override void RemoveChild(Node child) { }
        public override System.Collections.Generic.List<Node> GetChildren() => new System.Collections.Generic.List<Node>();

    }
}
