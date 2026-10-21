using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace ND.Dialogue
{
    /// <summary>Writes text into a UI.Text or a TextMeshPro component without depending on TextMeshPro.</summary>
    public static class UIText
    {
        private static readonly Dictionary<Type, PropertyInfo> s_TextProperties = new Dictionary<Type, PropertyInfo>();

        /// <summary>The writable string 'text' property of the graphic's type, or null.</summary>
        public static PropertyInfo TextProperty(Graphic graphic)
        {
            var type = graphic.GetType();
            if (!s_TextProperties.TryGetValue(type, out var property))
            {
                property = type.GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
                if (property != null && (property.PropertyType != typeof(string) || !property.CanWrite)) property = null;
                s_TextProperties[type] = property;
            }
            return property;
        }

        /// <summary>Sets the text; logs when the graphic has no writable string 'text' property.</summary>
        public static void Set(Graphic graphic, string text)
        {
            if (graphic == null) return;
            if (graphic is Text legacy) { legacy.text = text; return; }
            var property = TextProperty(graphic);
            if (property == null)
            {
                Debug.LogError($"[UIText] '{graphic.name}' ({graphic.GetType().Name}) has no writable string 'text' property; assign a UI.Text or TextMeshPro component.", graphic);
                return;
            }
            property.SetValue(graphic, text);
        }
    }
}
