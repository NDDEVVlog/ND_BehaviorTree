namespace ND_BehaviorTree.Demo.Xeno
{
    /// <summary>
    /// Sub-node: when the decorated node starts, copies one key into another (e.g. NoisePosition → SearchCenter before a
    /// search subtree), then runs the node. Replaces a separate "Copy" action in a sequence. Fails if the copy fails.
    /// </summary>
    [NodeInfo("Copy Key On Enter", "Decorator/Copy Key On Enter", true, true, iconPath: "Assets/ND_BehaviorTree/NDBT/Icons/boolean.png")]
    public class CopyKeyOnEnterDecorator : DecoratorNode
    {
        [BlackboardKeyType(typeof(object))] public Key from;
        [BlackboardKeyType(typeof(object))] public Key to;

        [System.NonSerialized] private bool m_Copied;

        protected override void OnEnter() => m_Copied = KeyResolver.Copy(this, from, to);

        protected override Status OnProcess()
        {
            if (!m_Copied) return Status.Failure;
            return child != null ? child.Process() : Status.Success;
        }
    }
}
