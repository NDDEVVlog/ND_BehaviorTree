using System;

namespace ND_BehaviorTree.Story
{
    /// <summary>
    /// How a Story node reads on the graph, e.g. <c>"{self} says {message}"</c>. Each <c>{name}</c> becomes an inline value:
    ///   * field <c>name</c> (string / float / int / bool / enum) is the value typed on the node;
    ///   * field <c>nameKey</c> (a Key with [BlackboardKeyType]) binds it to a blackboard key instead (the "@" button);
    ///   * <c>{self}</c> is the agent itself unless <c>selfKey</c> is bound.
    /// A placeholder may have only a key field (e.g. a GameObject target).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class StorySentenceAttribute : Attribute
    {
        public string Template { get; }
        public StorySentenceAttribute(string template) => Template = template;
    }
}
