using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace ND.Quests
{
    /// <summary>Writes text into a UI.Text or a TextMeshPro component without depending on TextMeshPro.</summary>
    public static class QuestUIText
    {
        private static readonly Dictionary<Type, PropertyInfo> s_TextProperties = new Dictionary<Type, PropertyInfo>();

        /// <summary>Sets the text; logs when the graphic has no writable string 'text' property.</summary>
        public static void Set(Graphic graphic, string text)
        {
            if (graphic == null) return;
            if (graphic is Text legacy) { legacy.text = text; return; }
            var type = graphic.GetType();
            if (!s_TextProperties.TryGetValue(type, out var property))
            {
                property = type.GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
                if (property != null && (property.PropertyType != typeof(string) || !property.CanWrite)) property = null;
                s_TextProperties[type] = property;
            }
            if (property == null)
            {
                Debug.LogError($"[QuestUIText] '{graphic.name}' ({type.Name}) has no writable string 'text' property; assign a UI.Text or TextMeshPro component.", graphic);
                return;
            }
            property.SetValue(graphic, text);
        }
    }
}
