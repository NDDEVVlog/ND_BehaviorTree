using System;

namespace ND_BehaviorTree.Editor.CustomGraph
{
    /// <summary>
    /// Put on an <see cref="ND_BTNodeView"/> subclass to make it the view of a node type (and its subclasses), with no
    /// asset configuration needed — plugins use this to ship their own node looks. The view class needs a
    /// constructor (Node, ViewThemeData). The most specific node type wins; a mapping with a custom view script in
    /// NodeEditorConfig still takes precedence.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class CustomNodeViewAttribute : Attribute
    {
        public Type NodeType { get; }
        public CustomNodeViewAttribute(Type nodeType) => NodeType = nodeType;
    }
}
