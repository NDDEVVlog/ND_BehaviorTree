
using System;
using UnityEngine;

namespace ND_BehaviorTree
{
    /// <summary>
    /// Defines a color to be used for this key type in custom editors.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class KeyColorAttribute : Attribute
    {
        public Color Color { get; private set; }

        /// <summary>
        /// Assigns a color using RGB float values (0.0 to 1.0).
        /// </summary>
        public KeyColorAttribute(float r, float g, float b)
        {
            this.Color = new Color(r, g, b);
        }
    }
}