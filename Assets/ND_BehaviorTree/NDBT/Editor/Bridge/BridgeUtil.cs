using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using ND_BehaviorTree.Editor.Graph;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ND_BehaviorTree.Editor.Bridge
{
    /// <summary>A command error with a message meant for the caller (shown as {"ok": false, "error": ...}).</summary>
    public sealed class BridgeException : Exception
    {
        public BridgeException(string message) : base(message) { }
    }

    /// <summary>Resolution of trees / nodes / types and SerializedProperty ⇄ JSON conversion for bridge commands.</summary>
    public static class BridgeUtil
    {
        #region Arguments

        public static string Str(Dictionary<string, object> args, string key, bool required = true)
        {
            if (args != null && args.TryGetValue(key, out var v) && v != null) return Convert.ToString(v, CultureInfo.InvariantCulture);
            if (required) throw new BridgeException($"Missing argument '{key}'.");
            return null;
        }

        public static bool Bool(Dictionary<string, object> args, string key, bool fallback = false)
        {
            if (args == null || !args.TryGetValue(key, out var v) || v == null) return fallback;
            if (v is bool b) return b;
            return string.Equals(Convert.ToString(v, CultureInfo.InvariantCulture), "true", StringComparison.OrdinalIgnoreCase);
        }

        public static List<object> List(Dictionary<string, object> args, string key, bool required = true)
        {
            if (args != null && args.TryGetValue(key, out var v))
            {
                if (v is List<object> list) return list;
                if (v is string s && s.TrimStart().StartsWith("[")) return MiniJson.Parse(s) as List<object>;
                if (v != null) return new List<object> { v };
            }
            if (required) throw new BridgeException($"Missing list argument '{key}'.");
            return null;
        }

        public static Dictionary<string, object> Dict(Dictionary<string, object> args, string key)
        {
            if (args == null || !args.TryGetValue(key, out var v) || v == null) return null;
            if (v is Dictionary<string, object> d) return d;
            if (v is string s && s.TrimStart().StartsWith("{")) return MiniJson.Parse(s) as Dictionary<string, object>;
            throw new BridgeException($"Argument '{key}' must be an object.");
        }

        public static Vector2 Vec2(object value, Vector2 fallback)
        {
            if (value is List<object> l && l.Count >= 2) return new Vector2(ToFloat(l[0]), ToFloat(l[1]));
            if (value is Dictionary<string, object> d && d.ContainsKey("x")) return new Vector2(ToFloat(d["x"]), d.TryGetValue("y", out var y) ? ToFloat(y) : 0f);
            return fallback;
        }

        public static float ToFloat(object v) => Convert.ToSingle(v, CultureInfo.InvariantCulture);

        #endregion

        #region Trees and nodes

        public static BehaviorTree ResolveTree(string arg)
        {
            if (string.IsNullOrWhiteSpace(arg)) throw new BridgeException("Missing tree (asset path, GUID or name).");
            arg = arg.Trim().Replace('\\', '/');

            string path = null;
            if (arg.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) || arg.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)) path = arg;
            else if (arg.Length == 32 && arg.All(Uri.IsHexDigit)) path = AssetDatabase.GUIDToAssetPath(arg);
            else
            {
                var matches = AssetDatabase.FindAssets("t:BehaviorTree").Select(AssetDatabase.GUIDToAssetPath)
                    .Where(p => string.Equals(System.IO.Path.GetFileNameWithoutExtension(p), arg, StringComparison.OrdinalIgnoreCase)).ToList();
                if (matches.Count == 1) path = matches[0];
                else if (matches.Count > 1) throw new BridgeException($"Tree name '{arg}' is ambiguous: {string.Join(", ", matches)}. Use the asset path or GUID.");
            }

            var tree = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<BehaviorTree>(path);
            if (tree == null) throw new BridgeException($"No BehaviorTree found for '{arg}'. Use tree_list to see available trees.");
            BehaviorTreeMigrator.MigrateIfNeeded(tree);
            return tree;
        }

        public static string PathOf(Object asset) => AssetDatabase.GetAssetPath(asset);
        public static string GuidOf(Object asset) => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
        public static string ShortId(Node node) => node == null || string.IsNullOrEmpty(node.id) ? null : node.id.Substring(0, Math.Min(8, node.id.Length));

        /// <summary>All nodes of a tree, including attached decorators and services.</summary>
        public static IEnumerable<Node> AllNodes(BehaviorTree tree)
        {
            foreach (var node in tree.nodes)
            {
                if (node == null) continue;
                yield return node;
                foreach (var d in node.decorators) if (d != null) yield return d;
                foreach (var s in node.services) if (s != null) yield return s;
            }
        }

        /// <summary>"root", a full node GUID, or a unique GUID prefix (≥ 4 chars, e.g. the 8-char ids in outlines).</summary>
        public static Node ResolveNode(BehaviorTree tree, string id)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new BridgeException("Missing node id.");
            id = id.Trim();
            if (string.Equals(id, "root", StringComparison.OrdinalIgnoreCase))
                return tree.rootNode ?? throw new BridgeException($"Tree '{tree.name}' has no root node.");

            var all = AllNodes(tree).ToList();
            var exact = all.FirstOrDefault(n => n.id == id);
            if (exact != null) return exact;
            if (id.Length < 4) throw new BridgeException($"Node id '{id}' is too short; use at least 4 characters of its id.");

            var prefixed = all.Where(n => n.id != null && n.id.StartsWith(id, StringComparison.OrdinalIgnoreCase)).ToList();
            if (prefixed.Count == 1) return prefixed[0];
            if (prefixed.Count > 1) throw new BridgeException($"Node id '{id}' matches {prefixed.Count} nodes; use more characters.");
            throw new BridgeException($"No node '{id}' in tree '{tree.name}'. Use tree_get to see node ids.");
        }

        #endregion

        #region Node types

        public enum TypeKind { GraphNode, Decorator, Service, Any }

        public static NodeInfoAttribute Info(Type type) => type.GetCustomAttribute<NodeInfoAttribute>();

        public static string Title(Type type) => Info(type)?.title ?? ObjectNames.NicifyVariableName(type.Name);

        public static string KindOf(Type type)
        {
            if (typeof(RootNode).IsAssignableFrom(type)) return "root";
            if (typeof(EventNode).IsAssignableFrom(type)) return "event";
            if (typeof(InitNode).IsAssignableFrom(type)) return "init";
            if (typeof(DecoratorNode).IsAssignableFrom(type)) return "decorator";
            if (typeof(ServiceNode).IsAssignableFrom(type)) return "service";
            if (typeof(SubtreeNode).IsAssignableFrom(type)) return "subtree";
            if (typeof(CompositeNode).IsAssignableFrom(type)) return "composite";
            return "action";
        }

        public static IEnumerable<Type> NodeTypes() =>
            TypeCache.GetTypesDerivedFrom<Node>().Where(t => !t.IsAbstract && !t.IsGenericType);

        /// <summary>Accepts full name, class name (with or without "Node"), [NodeInfo] title, menu path or its last segment.</summary>
        public static Type ResolveNodeType(string name, TypeKind kind)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new BridgeException("Missing node type.");
            string n = name.Trim();
            bool Match(Type t)
            {
                var info = Info(t);
                string menu = info?.menuItem;
                return string.Equals(t.FullName, n, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(t.Name, n, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(t.Name, n + "Node", StringComparison.OrdinalIgnoreCase)
                    || (info?.title != null && string.Equals(info.title, n, StringComparison.OrdinalIgnoreCase))
                    || (!string.IsNullOrEmpty(menu) && (string.Equals(menu, n, StringComparison.OrdinalIgnoreCase)
                                                        || string.Equals(menu.Substring(menu.LastIndexOf('/') + 1), n, StringComparison.OrdinalIgnoreCase)));
            }

            var candidates = NodeTypes().Where(t =>
                kind == TypeKind.Any
                || (kind == TypeKind.Decorator && typeof(DecoratorNode).IsAssignableFrom(t))
                || (kind == TypeKind.Service && typeof(ServiceNode).IsAssignableFrom(t))
                || (kind == TypeKind.GraphNode && !typeof(DecoratorNode).IsAssignableFrom(t) && !typeof(ServiceNode).IsAssignableFrom(t) && !typeof(RootNode).IsAssignableFrom(t)))
                .Where(Match).ToList();

            if (candidates.Count == 1) return candidates[0];
            if (candidates.Count > 1) throw new BridgeException($"Type '{name}' is ambiguous: {string.Join(", ", candidates.Select(c => c.FullName))}.");
            throw new BridgeException($"Unknown {(kind == TypeKind.Any ? "node" : kind.ToString().ToLowerInvariant())} type '{name}'. Use node_list to see available types.");
        }

        #endregion

        #region SerializedProperty <-> JSON

        private static readonly HashSet<string> s_HiddenFields = new HashSet<string>
        {
            "m_Script", "m_guid", "m_position", "children", "child", "decorators", "services"
        };

        /// <summary>Visible serialized fields as JSON (keys are excluded: they are reported as key slots).</summary>
        public static Dictionary<string, object> FieldsOf(Object target, ICollection<string> excludePaths = null)
        {
            var result = new Dictionary<string, object>();
            var so = new SerializedObject(target);
            var it = so.GetIterator();
            if (!it.NextVisible(true)) return result;
            do
            {
                if (s_HiddenFields.Contains(it.name)) continue;
                if (excludePaths != null && excludePaths.Contains(it.propertyPath)) continue;
                result[it.name] = PropertyToJson(it.Copy(), 0, excludePaths);
            } while (it.NextVisible(false));
            return result;
        }

        public static object PropertyToJson(SerializedProperty p, int depth, ICollection<string> excludePaths = null)
        {
            if (depth > 4) return "…";
            switch (p.propertyType)
            {
                case SerializedPropertyType.Integer: return p.longValue;
                case SerializedPropertyType.Boolean: return p.boolValue;
                case SerializedPropertyType.Float: return Math.Round(p.doubleValue, 4);
                case SerializedPropertyType.String: return p.stringValue;
                case SerializedPropertyType.Enum:
                    return p.enumValueIndex >= 0 && p.enumValueIndex < p.enumNames.Length ? p.enumNames[p.enumValueIndex] : (object)p.intValue;
                case SerializedPropertyType.LayerMask: return p.intValue;
                case SerializedPropertyType.Vector2: return new List<object> { p.vector2Value.x, p.vector2Value.y };
                case SerializedPropertyType.Vector3: return new List<object> { p.vector3Value.x, p.vector3Value.y, p.vector3Value.z };
                case SerializedPropertyType.Vector4: return new List<object> { p.vector4Value.x, p.vector4Value.y, p.vector4Value.z, p.vector4Value.w };
                case SerializedPropertyType.Vector2Int: return new List<object> { p.vector2IntValue.x, p.vector2IntValue.y };
                case SerializedPropertyType.Vector3Int: return new List<object> { p.vector3IntValue.x, p.vector3IntValue.y, p.vector3IntValue.z };
                case SerializedPropertyType.Color: return "#" + ColorUtility.ToHtmlStringRGBA(p.colorValue);
                case SerializedPropertyType.ObjectReference:
                    if (p.objectReferenceValue == null) return null;
                    if (p.objectReferenceValue is Key key) return $"key:{key.keyName}";
                    var path = AssetDatabase.GetAssetPath(p.objectReferenceValue);
                    return string.IsNullOrEmpty(path) ? p.objectReferenceValue.name : $"{path}#{p.objectReferenceValue.name}";
                case SerializedPropertyType.ManagedReference:
                {
                    if (p.managedReferenceValue == null) return null;
                    var obj = new Dictionary<string, object> { ["$type"] = p.managedReferenceValue.GetType().FullName };
                    AddChildren(p, obj, depth, excludePaths);
                    return obj;
                }
                case SerializedPropertyType.Generic:
                    if (p.isArray)
                    {
                        var list = new List<object>();
                        for (int i = 0; i < Math.Min(p.arraySize, 100); i++) list.Add(PropertyToJson(p.GetArrayElementAtIndex(i), depth + 1, excludePaths));
                        return list;
                    }
                    var generic = new Dictionary<string, object>();
                    AddChildren(p, generic, depth, excludePaths);
                    return generic;
                default:
                    return $"({p.propertyType})";
            }
        }

        private static void AddChildren(SerializedProperty p, Dictionary<string, object> obj, int depth, ICollection<string> excludePaths)
        {
            var child = p.Copy();
            var end = p.GetEndProperty();
            if (!child.NextVisible(true)) return;
            while (!SerializedProperty.EqualContents(child, end))
            {
                if (excludePaths == null || !excludePaths.Contains(child.propertyPath))
                    obj[child.name] = PropertyToJson(child.Copy(), depth + 1, excludePaths);
                if (!child.NextVisible(false)) break;
            }
        }

        /// <summary>Writes a JSON value into a property. Throws BridgeException with a helpful message on mismatch.</summary>
        public static void SetProperty(SerializedProperty p, object value)
        {
            try
            {
                switch (p.propertyType)
                {
                    case SerializedPropertyType.Integer: p.longValue = Convert.ToInt64(value, CultureInfo.InvariantCulture); return;
                    case SerializedPropertyType.Boolean: p.boolValue = value is bool b ? b : bool.Parse(Convert.ToString(value, CultureInfo.InvariantCulture)); return;
                    case SerializedPropertyType.Float: p.doubleValue = Convert.ToDouble(value, CultureInfo.InvariantCulture); return;
                    case SerializedPropertyType.String: p.stringValue = value == null ? string.Empty : Convert.ToString(value, CultureInfo.InvariantCulture); return;
                    case SerializedPropertyType.LayerMask: p.intValue = Convert.ToInt32(value, CultureInfo.InvariantCulture); return;
                    case SerializedPropertyType.Enum:
                    {
                        if (value is string s)
                        {
                            int index = Array.FindIndex(p.enumNames, e => string.Equals(e, s, StringComparison.OrdinalIgnoreCase));
                            if (index < 0) index = Array.FindIndex(p.enumDisplayNames, e => string.Equals(e, s, StringComparison.OrdinalIgnoreCase));
                            if (index < 0) throw new BridgeException($"'{s}' is not a value of {p.name}. Allowed: {string.Join(", ", p.enumNames)}.");
                            p.enumValueIndex = index;
                        }
                        else p.intValue = Convert.ToInt32(value, CultureInfo.InvariantCulture);
                        return;
                    }
                    case SerializedPropertyType.Vector2: { var l = Floats(value, 2); p.vector2Value = new Vector2(l[0], l[1]); return; }
                    case SerializedPropertyType.Vector3: { var l = Floats(value, 3); p.vector3Value = new Vector3(l[0], l[1], l[2]); return; }
                    case SerializedPropertyType.Vector4: { var l = Floats(value, 4); p.vector4Value = new Vector4(l[0], l[1], l[2], l[3]); return; }
                    case SerializedPropertyType.Vector2Int: { var l = Floats(value, 2); p.vector2IntValue = new Vector2Int((int)l[0], (int)l[1]); return; }
                    case SerializedPropertyType.Vector3Int: { var l = Floats(value, 3); p.vector3IntValue = new Vector3Int((int)l[0], (int)l[1], (int)l[2]); return; }
                    case SerializedPropertyType.Color:
                    {
                        if (value is string hex && ColorUtility.TryParseHtmlString(hex, out var c)) { p.colorValue = c; return; }
                        var l = Floats(value, 3);
                        p.colorValue = new Color(l[0], l[1], l[2], l.Length > 3 ? l[3] : 1f);
                        return;
                    }
                    case SerializedPropertyType.ObjectReference:
                    {
                        if (value == null) { p.objectReferenceValue = null; return; }
                        string s = Convert.ToString(value, CultureInfo.InvariantCulture);
                        if (s.StartsWith("key:")) throw new BridgeException($"'{p.propertyPath}' is a blackboard key field: use the link-key operation with key \"{s.Substring(4)}\".");
                        string path = s.Contains("#") ? s.Substring(0, s.IndexOf('#')) : s;
                        if (path.Length == 32 && path.All(Uri.IsHexDigit)) path = AssetDatabase.GUIDToAssetPath(path);
                        var asset = AssetDatabase.LoadAssetAtPath<Object>(path);
                        if (asset == null) throw new BridgeException($"No asset at '{s}' for field '{p.propertyPath}'.");
                        p.objectReferenceValue = asset;
                        return;
                    }
                    case SerializedPropertyType.ManagedReference:
                    {
                        if (value == null) { p.managedReferenceValue = null; return; }
                        if (!(value is Dictionary<string, object> obj)) throw new BridgeException($"'{p.propertyPath}' needs an object like {{\"$type\": \"TypeName\", ...fields}}.");
                        if (obj.TryGetValue("$type", out var typeName) && typeName != null)
                            p.managedReferenceValue = CreateManagedInstance(p, Convert.ToString(typeName, CultureInfo.InvariantCulture));
                        else if (p.managedReferenceValue == null)
                            throw new BridgeException($"'{p.propertyPath}' is empty: give \"$type\" to choose an implementation.");
                        foreach (var kvp in obj.Where(k => k.Key != "$type"))
                            SetProperty(Child(p, kvp.Key), kvp.Value);
                        return;
                    }
                    case SerializedPropertyType.Generic:
                    {
                        if (p.isArray)
                        {
                            if (!(value is List<object> list)) throw new BridgeException($"'{p.propertyPath}' needs a list.");
                            p.arraySize = list.Count;
                            for (int i = 0; i < list.Count; i++) SetProperty(p.GetArrayElementAtIndex(i), list[i]);
                            return;
                        }
                        if (!(value is Dictionary<string, object> fields)) throw new BridgeException($"'{p.propertyPath}' needs an object.");
                        foreach (var kvp in fields) SetProperty(Child(p, kvp.Key), kvp.Value);
                        return;
                    }
                    default:
                        throw new BridgeException($"Field '{p.propertyPath}' has unsupported type {p.propertyType}; edit it in the Unity editor.");
                }
            }
            catch (BridgeException) { throw; }
            catch (Exception e) when (e is FormatException || e is InvalidCastException || e is OverflowException)
            {
                throw new BridgeException($"Value '{MiniJson.Serialize(value)}' does not fit field '{p.propertyPath}' ({p.propertyType}).");
            }
        }

        private static SerializedProperty Child(SerializedProperty parent, string name)
        {
            var child = parent.FindPropertyRelative(name);
            if (child == null) throw new BridgeException($"'{parent.propertyPath}' has no field '{name}'.");
            return child;
        }

        private static float[] Floats(object value, int min)
        {
            if (value is List<object> l && l.Count >= min) return l.Select(ToFloat).ToArray();
            if (value is Dictionary<string, object> d)
            {
                var names = new[] { "x", "y", "z", "w" };
                var r = new List<float>();
                for (int i = 0; i < names.Length && d.ContainsKey(names[i]); i++) r.Add(ToFloat(d[names[i]]));
                if (r.Count >= min) return r.ToArray();
            }
            throw new BridgeException($"Expected a list of {min} numbers.");
        }

        private static object CreateManagedInstance(SerializedProperty p, string typeName)
        {
            // managedReferenceFieldTypename = "Assembly Namespace.Type" of the declared field type.
            string declared = p.managedReferenceFieldTypename;
            string declaredName = declared.Substring(declared.IndexOf(' ') + 1);
            var baseType = TypeCache.GetTypesDerivedFrom<object>().FirstOrDefault(t => t.FullName == declaredName)
                           ?? AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(declaredName)).FirstOrDefault(t => t != null);
            if (baseType == null) throw new BridgeException($"Cannot resolve the field type of '{p.propertyPath}'.");

            var options = TypeCache.GetTypesDerivedFrom(baseType).Append(baseType)
                .Where(t => !t.IsAbstract && !t.IsInterface && !typeof(Object).IsAssignableFrom(t) && t.GetConstructor(Type.EmptyTypes) != null).ToList();
            var match = options.FirstOrDefault(t => string.Equals(t.FullName, typeName, StringComparison.OrdinalIgnoreCase) || string.Equals(t.Name, typeName, StringComparison.OrdinalIgnoreCase));
            if (match == null) throw new BridgeException($"'{typeName}' is not an implementation of {baseType.Name}. Options: {string.Join(", ", options.Select(o => o.Name))}.");
            return Activator.CreateInstance(match);
        }

        #endregion
    }
}
