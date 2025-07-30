using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ND_BehaviorTree
{
    [System.AttributeUsage(System.AttributeTargets.Class, Inherited = true, AllowMultiple = true)]
    public class RequireComponentInRunnerAttribute : Attribute
    {
        public System.Type RequiredComponentType { get; private set; }

        public RequireComponentInRunnerAttribute(System.Type requiredComponentType)
        {
            // Ensure the type is a component
            if (!typeof(Component).IsAssignableFrom(requiredComponentType))
            {
                Debug.LogError($"'{requiredComponentType.Name}' is not a valid Component type for RequireComponentInRunnerAttribute.");
                this.RequiredComponentType = null;
            }
            else
            {
                this.RequiredComponentType = requiredComponentType;
            }
        }
    }
}
