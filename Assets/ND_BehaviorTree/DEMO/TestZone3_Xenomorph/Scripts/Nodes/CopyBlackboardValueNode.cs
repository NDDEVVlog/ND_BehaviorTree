namespace ND_BehaviorTree.Demo.Xeno
{
    /// <summary>
    /// Copies one blackboard key into another of the same type (e.g. NoisePosition → SearchCenter).
    /// To copy right before a node runs without an extra node in the graph, attach <see cref="CopyKeyOnEnterDecorator"/> instead.
    /// </summary>
    [NodeDescription("Copies one blackboard key into another of the same type.", "Succeeds after copying; fails if a key is missing or the types differ.\nTo copy right before a node runs without an extra node, attach the 'Copy Key On Enter' sub-node instead.")]
    [NodeInfo("Copy Blackboard Value", "Action/Blackboard/Copy Value", true, false, iconPath: "Assets/ND_BehaviorTree/NDBT/Icons/boolean.png")]
    public class CopyBlackboardValueNode : ActionNode
    {
        [BlackboardKeyType(typeof(object))]
        public Key from;

        [BlackboardKeyType(typeof(object))]
        public Key to;

        protected override Status OnProcess() => KeyResolver.Copy(this, from, to) ? Status.Success : Status.Failure;
    }
}
