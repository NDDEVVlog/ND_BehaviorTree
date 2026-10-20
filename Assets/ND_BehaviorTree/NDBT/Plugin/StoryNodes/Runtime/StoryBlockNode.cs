namespace ND_BehaviorTree.Story
{
    /// <summary>
    /// A stack of actions drawn inside one node on the graph, run top to bottom like a Sequence. Its children are
    /// owned and embedded (<see cref="IEmbeddedChildren"/>): added, removed and reordered from the block itself.
    /// </summary>
    [NodeDescription("A stack of actions in one node, run top to bottom (like a Sequence).",
        "Add actions with '+ Add action', reorder with the arrows, remove with the cross (hover a row). Double-click a row for its properties.\n" +
        "Fails as soon as an action fails; succeeds when every action succeeded.")]
    [NodeInfo("Block", "Story/Block (action stack)", true, false, iconPath: "Assets/ND_BehaviorTree/NDBT/Icons/ProcessSequence.png")]
    public class StoryBlockNode : CompositeNode, IEmbeddedChildren
    {
        [System.NonSerialized] private int m_Index;

        /// <summary>Index of the action currently running (editor live view), or -1.</summary>
        public int RunningIndex => IsRunning() ? m_Index : -1;

        protected override void OnEnter() => m_Index = 0;

        protected override Status OnProcess()
        {
            while (m_Index < children.Count)
            {
                var child = children[m_Index];
                var status = child != null ? child.Process() : Status.Success;
                if (status == Status.Running) return Status.Running;
                if (status == Status.Failure) return Status.Failure;
                m_Index++;
            }
            return Status.Success;
        }

        public override void Reset()
        {
            base.Reset();
            m_Index = 0;
        }
    }
}
