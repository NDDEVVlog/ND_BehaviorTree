
using System;

namespace ND_BehaviorTree
{
    /// <summary>
    /// Used on a Key field to make its editor behavior dependent on another Key field.
    /// If the 'control' key is not linked to the Blackboard ([None]), this key will
    /// display a direct value editor instead of a Blackboard key selector.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class DetachKeyAttribute : Attribute
    {
        public string ControlKeyFieldName { get; }

        public DetachKeyAttribute(string controlKeyFieldName)
        {
            ControlKeyFieldName = controlKeyFieldName;
        }
    }
}