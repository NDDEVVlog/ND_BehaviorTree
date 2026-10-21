using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ND_BehaviorTree.Editor.Graph
{
    /// <summary>A blackboard Key field on a node (possibly inside a [SerializeReference] / [Serializable] member).</summary>
    public sealed class KeySlot
    {
        public string PropertyPath;
        public string Label;
        public FieldInfo Field;
        /// <summary>Concrete Key class the field accepts (e.g. Key, Key&lt;NavMeshAgent&gt;).</summary>
        public Type FieldType;
        /// <summary>Value type the key must hold; null = any.</summary>
        public Type RequiredValueType;
        public Key Current;
    }

    /// <summary>
    /// Finds Key fields on nodes and applies the same type rules as KeyDrawer:
    /// value type = Key&lt;T&gt; generic argument → [DetachKey] control key's type → [BlackboardKeyType] → any.
    /// </summary>
    public static class KeySlotScanner
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public static List<KeySlot> Scan(Node node)
        {
            var slots = new List<KeySlot>();
            if (node == null) return slots;
            ScanObject(node, node.GetType(), string.Empty, string.Empty, slots, depth: 0, root: node);
            return slots;
        }

        private static void ScanObject(object owner, Type type, string pathPrefix, string labelPrefix, List<KeySlot> slots, int depth, Node root)
        {
            foreach (var field in GetSerializedFields(type))
            {
                string path = pathPrefix + field.Name;
                string label = labelPrefix + ObjectNames.NicifyVariableName(field.Name);

                if (typeof(Key).IsAssignableFrom(field.FieldType))
                {
                    slots.Add(new KeySlot
                    {
                        PropertyPath = path,
                        Label = label,
                        Field = field,
                        FieldType = field.FieldType,
                        RequiredValueType = GetRequiredValueType(field, owner),
                        Current = field.GetValue(owner) as Key
                    });
                    continue;
                }

                if (depth >= 1 || !CanContainKeys(field)) continue; // one nested level is enough for strategy objects

                var value = field.GetValue(owner);
                if (value == null) continue;
                ScanObject(value, value.GetType(), path + ".", label + " ▸ ", slots, depth + 1, root);
            }
        }

        private static readonly Dictionary<FieldInfo, bool> s_ContainerCache = new Dictionary<FieldInfo, bool>();

        /// <summary>[SerializeReference] or [Serializable] class field (not a list / Unity object / primitive): may hold Key fields.</summary>
        private static bool CanContainKeys(FieldInfo field)
        {
            if (s_ContainerCache.TryGetValue(field, out bool cached)) return cached;
            var type = field.FieldType;
            bool result = !typeof(UnityEngine.Object).IsAssignableFrom(type) && !type.IsPrimitive && type != typeof(string)
                          && !typeof(System.Collections.IEnumerable).IsAssignableFrom(type)
                          && (field.GetCustomAttribute<SerializeReference>() != null || (type.IsClass && type.GetCustomAttribute<SerializableAttribute>() != null));
            s_ContainerCache[field] = result;
            return result;
        }

        // Reflection metadata is cached per type / field: Scan runs periodically for every node on screen.
        private static readonly Dictionary<Type, FieldInfo[]> s_FieldCache = new Dictionary<Type, FieldInfo[]>();
        private sealed class FieldMeta
        {
            public Type GenericValueType;
            public DetachKeyAttribute Detach;
            public FieldInfo DetachControl;
            public Type AttributeType;
        }
        private static readonly Dictionary<FieldInfo, FieldMeta> s_MetaCache = new Dictionary<FieldInfo, FieldMeta>();

        private static FieldInfo[] GetSerializedFields(Type type)
        {
            if (s_FieldCache.TryGetValue(type, out var cached)) return cached;

            var chain = new List<Type>();
            for (var t = type; t != null && t != typeof(Node) && t != typeof(object) && t != typeof(ScriptableObject); t = t.BaseType) chain.Add(t);
            chain.Reverse(); // base fields first, like the inspector

            var fields = new List<FieldInfo>();
            foreach (var t in chain)
            {
                foreach (var field in t.GetFields(Flags | BindingFlags.DeclaredOnly))
                {
                    if (field.IsStatic || field.IsNotSerialized) continue;
                    if (field.GetCustomAttribute<HideInInspector>() != null) continue;
                    if (!field.IsPublic && field.GetCustomAttribute<SerializeField>() == null && field.GetCustomAttribute<SerializeReference>() == null) continue;
                    fields.Add(field);
                }
            }
            var result = fields.ToArray();
            s_FieldCache[type] = result;
            return result;
        }

        private static FieldMeta GetMeta(FieldInfo field, Type ownerType)
        {
            if (s_MetaCache.TryGetValue(field, out var meta)) return meta;
            meta = new FieldMeta();
            for (var t = field.FieldType; t != null && t != typeof(Key); t = t.BaseType)
            {
                if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Key<>)) { meta.GenericValueType = t.GetGenericArguments()[0]; break; }
            }
            meta.Detach = field.GetCustomAttribute<DetachKeyAttribute>();
            if (meta.Detach != null) meta.DetachControl = ownerType.GetField(meta.Detach.ControlKeyFieldName, Flags);
            var attr = field.GetCustomAttribute<BlackboardKeyTypeAttribute>();
            if (attr != null && attr.RequiredType != typeof(object)) meta.AttributeType = attr.RequiredType;
            s_MetaCache[field] = meta;
            return meta;
        }

        public static Type GetRequiredValueType(FieldInfo field, object owner)
        {
            var meta = GetMeta(field, owner.GetType());
            if (meta.GenericValueType != null) return meta.GenericValueType;
            if (meta.DetachControl != null && meta.DetachControl.GetValue(owner) is Key control
                && control.GetValueType() != null && control.GetValueType() != typeof(object))
                return control.GetValueType();
            return meta.AttributeType;
        }

        public static bool IsCompatible(KeySlot slot, Key key, out string reason)
        {
            reason = null;
            if (key == null) return true;
            if (!slot.FieldType.IsInstanceOfType(key))
                reason = $"'{slot.Label}' needs a {FriendlyTypeName(slot.FieldType)}, not {FriendlyTypeName(key.GetType())}";
            else if (slot.RequiredValueType != null && (key.GetValueType() == null || !slot.RequiredValueType.IsAssignableFrom(key.GetValueType())))
                reason = $"'{slot.Label}' needs a {FriendlyTypeName(slot.RequiredValueType)} key; '{key.keyName}' holds {FriendlyTypeName(key.GetValueType())}";
            return reason == null;
        }

        /// <summary>True when the key is one of the tree's blackboard keys (linked), false for a detached local value.</summary>
        public static bool IsLinked(BehaviorTree tree, Key key) =>
            key != null && tree != null && tree.blackboard != null && tree.blackboard.ContainsInChain(key);

        public static string FriendlyTypeName(Type type)
        {
            if (type == null) return "any";
            if (type == typeof(float)) return "Float";
            if (type == typeof(int)) return "Int";
            if (type == typeof(bool)) return "Bool";
            if (type == typeof(string)) return "String";
            if (type == typeof(double)) return "Double";
            if (type == typeof(long)) return "Long";
            if (type == typeof(object)) return "Any";
            if (type.IsGenericType)
            {
                string args = string.Join(", ", type.GetGenericArguments().Select(FriendlyTypeName));
                string name = type.Name.Substring(0, type.Name.IndexOf('`'));
                return name == "Key" ? $"{args} key" : $"{name}<{args}>";
            }
            return type.Name;
        }

        public static Color KeyColor(Type keyType)
        {
            var attr = keyType?.GetCustomAttribute<KeyColorAttribute>();
            return attr?.Color ?? new Color(0.6f, 0.6f, 0.6f);
        }

        /// <summary>Colour for a value type, taken from the first Key class holding that type.</summary>
        public static Color ValueTypeColor(Type valueType)
        {
            if (valueType == null) return new Color(0.6f, 0.6f, 0.6f);
            if (s_ValueTypeColors.TryGetValue(valueType, out var cached)) return cached;

            var color = new Color(0.6f, 0.6f, 0.6f);
            foreach (var keyType in TypeCache.GetTypesDerivedFrom<Key>())
            {
                if (keyType.IsAbstract) continue;
                bool match = false;
                for (var t = keyType.BaseType; t != null && !match; t = t.BaseType)
                    match = t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Key<>) && t.GetGenericArguments()[0] == valueType;
                if (match) { color = KeyColor(keyType); break; }
            }
            s_ValueTypeColors[valueType] = color;
            return color;
        }

        private static readonly Dictionary<Type, Color> s_ValueTypeColors = new Dictionary<Type, Color>();
    }
}
