using System;

namespace ND_BehaviorTree
{
    /// <summary>
    /// What a node does, for people building trees. <see cref="Summary"/> is the hover tooltip on the graph (and in the
    /// search popup); <see cref="Details"/> is shown in the Node Properties window (double-click a node).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class NodeDescriptionAttribute : Attribute
    {
        public string Summary { get; }
        public string Details { get; }

        public NodeDescriptionAttribute(string summary, string details = null)
        {
            Summary = summary;
            Details = details;
        }
    }
}
