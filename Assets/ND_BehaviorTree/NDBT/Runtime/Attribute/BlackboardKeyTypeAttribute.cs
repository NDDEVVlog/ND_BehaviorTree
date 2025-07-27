using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
namespace ND_BehaviorTree
{
    /// <summary>
    /// Attribute used on a 'Key' field in a Node to specify what value type 
    /// the key should hold. This is used by the editor to filter the dropdown.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class BlackboardKeyTypeAttribute : Attribute
    {
        public Type RequiredType { get; }

        public BlackboardKeyTypeAttribute(Type requiredType)
        {
            this.RequiredType = requiredType;
        }
    }
}
