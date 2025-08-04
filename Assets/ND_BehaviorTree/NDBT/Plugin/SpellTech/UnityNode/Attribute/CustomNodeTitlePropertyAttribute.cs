// FILE: CustomNodeTitlePropertyAttribute.cs
using System;

namespace ND_BehaviorTree
{
    /// <summary>
    /// Specifies a custom format for a node's title in the Behavior Tree editor.
    /// Use bracketed property names to create dynamic fields in the title.
    /// Example: [CustomNodeTitleProperty("[keyName] is set to [targetValue]")]
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class CustomNodeTitlePropertyAttribute : Attribute
    {
        /// <summary>
        /// The format string for the dynamic title.
        /// Property names should be enclosed in square brackets, e.g., [myProperty].
        /// </summary>
        public string TitleFormat { get; private set; }

        /// <summary>
        /// Initializes a new instance of the CustomNodeTitlePropertyAttribute class.
        /// </summary>
        /// <param name="titleFormat">The format string for the title.</param>
        public CustomNodeTitlePropertyAttribute(string titleFormat)
        {
            this.TitleFormat = titleFormat;
        }
    }
}