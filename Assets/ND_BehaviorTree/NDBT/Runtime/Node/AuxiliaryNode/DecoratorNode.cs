

using System.Collections.Generic;
using UnityEngine;

namespace ND_BehaviorTree
{
    /// <summary>
    /// Decorator nodes are "wrapper" nodes that alter the behavior of the single child node they contain.
    /// They can be used to invert results, repeat execution, or add conditions to a node.
    /// A decorator is part of the main execution flow of the tree.
    /// </summary>
    public abstract class DecoratorNode : AuxiliaryNode
    {
        public override Node Clone()
        {
            DecoratorNode node = base.Clone() as DecoratorNode;
            // The child itself is cloned and assigned by the BehaviorTree's Clone method.
            node.child = null;
            return node;
        }

        /// <summary>
        /// Called when the decorator node's Process method is called for the first time.
        /// Place any decorator-specific initialization logic here.
        /// </summary>
        protected override void OnEnter() { }

        /// <summary>
        /// Called when the decorator node's status is no longer Running.
        /// Place any decorator-specific cleanup logic here.
        /// </summary>
        protected override void OnExit() { }
        
        // OnProcess is inherited as abstract from Node. It must be implemented by concrete
        // decorator classes (e.g., Inverter, Repeater). The implementation will typically
        // call 'child.Process()' and then modify or react to the returned status.
    }
}