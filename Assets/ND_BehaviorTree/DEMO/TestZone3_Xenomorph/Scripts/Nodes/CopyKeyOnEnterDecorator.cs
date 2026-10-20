namespace ND_BehaviorTree.Demo.Xeno
{
    /// <summary>
    /// Sub-node: when the decorated node starts, copies one key into another (e.g. NoisePosition → SearchCenter before a
    /// search subtree), then runs the node. Replaces a separate "Copy" action in a sequence. Fails if the copy fails.
    /// </summary>
    [NodeDescription("Sub-node: copies one key into another when its node starts, then runs the node.", "Example: NoisePosition -> SearchCenter on a search subtree. Fails (without running the node) if the copy fails.")]
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
