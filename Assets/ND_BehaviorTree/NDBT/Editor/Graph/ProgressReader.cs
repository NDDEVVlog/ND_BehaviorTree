using System;
using System.Collections.Generic;
using System.Reflection;

namespace ND_BehaviorTree.Editor.Graph
{
    /// <summary>
    /// Reads 0..1 progress from a runtime node for the live view:
    ///   - [NodeProgressBar(currentField, maxField)] on a field (e.g. TimeLimit elapsedTime/timeLimit, Cooldown remaining/duration),
    ///   - ServiceNode.TickProgress for services.
    /// Field lookups are cached per type.
    /// </summary>
    public static class ProgressReader
    {
        private sealed class Binding
        {
            public FieldInfo Current;
            public FieldInfo Max;
            public float Start;
        }

        private static readonly Dictionary<Type, Binding> s_Cache = new Dictionary<Type, Binding>();
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public static bool HasProgress(Type nodeType) => typeof(ServiceNode).IsAssignableFrom(nodeType) || GetBinding(nodeType) != null;

        public static bool TryGetProgress(Node runtimeNode, out float progress01)
        {
            progress01 = 0f;
            if (runtimeNode == null) return false;
            if (runtimeNode is ServiceNode service)
            {
                progress01 = service.TickProgress;
                return true;
            }

            var binding = GetBinding(runtimeNode.GetType());
            if (binding == null) return false;
            float current = ToFloat(binding.Current.GetValue(runtimeNode));
            float max = ToFloat(binding.Max.GetValue(runtimeNode));
            if (max - binding.Start <= 0.0001f) return false;
            progress01 = Math.Max(0f, Math.Min(1f, (current - binding.Start) / (max - binding.Start)));
            return true;
        }

        private static Binding GetBinding(Type type)
        {
            if (s_Cache.TryGetValue(type, out var cached)) return cached;
            Binding binding = null;
            for (var t = type; t != null && binding == null; t = t.BaseType)
            {
                foreach (var field in t.GetFields(Flags | BindingFlags.DeclaredOnly))
                {
                    var attr = field.GetCustomAttribute<NodeProgressBar>();
                    if (attr == null || string.IsNullOrEmpty(attr.currentValueField) || string.IsNullOrEmpty(attr.maxValueField)) continue;
                    var current = FindField(type, attr.currentValueField);
                    var max = FindField(type, attr.maxValueField);
                    if (current != null && max != null) { binding = new Binding { Current = current, Max = max, Start = attr.fieldStartValue }; break; }
                }
            }
            s_Cache[type] = binding;
            return binding;
        }

        private static FieldInfo FindField(Type type, string name)
        {
            for (var t = type; t != null; t = t.BaseType)
            {
                var f = t.GetField(name, Flags | BindingFlags.DeclaredOnly);
                if (f != null) return f;
            }
            return null;
        }

        private static float ToFloat(object value) => value switch
        {
            float f => f,
            int i => i,
            double d => (float)d,
            _ => 0f
        };
    }
}
