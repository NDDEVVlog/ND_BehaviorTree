using System;
using System.Globalization;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ND.Dialogue
{
    /// <summary>
    /// Types a dialogue variable / character attribute can hold. Object holds any Unity object reference
    /// (GameObject, Transform, a ScriptableObject…), matching blackboard object keys.
    /// </summary>
    public enum VariableType { Bool, Int, Float, String, Vector2, Vector3, Color, Object }

    /// <summary>A typed value of a dialogue/world variable. Only the field matching <see cref="type"/> is meaningful.</summary>
    [Serializable]
    public struct VariableValue : IEquatable<VariableValue>
    {
        public VariableType type;
        public bool boolValue;
        public int intValue;
        public float floatValue;
        public string stringValue;
        /// <summary>Vector2 (xy) and Vector3 (xyz).</summary>
        public Vector4 vectorValue;
        public Color colorValue;
        public Object objectValue;

        public static VariableValue Bool(bool v) => new VariableValue { type = VariableType.Bool, boolValue = v };
        public static VariableValue Int(int v) => new VariableValue { type = VariableType.Int, intValue = v };
        public static VariableValue Float(float v) => new VariableValue { type = VariableType.Float, floatValue = v };
        public static VariableValue String(string v) => new VariableValue { type = VariableType.String, stringValue = v ?? string.Empty };
        public static VariableValue Vector2(Vector2 v) => new VariableValue { type = VariableType.Vector2, vectorValue = v };
        public static VariableValue Vector3(Vector3 v) => new VariableValue { type = VariableType.Vector3, vectorValue = v };
        public static VariableValue Color(Color v) => new VariableValue { type = VariableType.Color, colorValue = v };
        public static VariableValue Object(Object v) => new VariableValue { type = VariableType.Object, objectValue = v };

        public bool IsNumber => type == VariableType.Int || type == VariableType.Float;
        public bool IsVector => type == VariableType.Vector2 || type == VariableType.Vector3;

        /// <summary>Numeric view (Int and Float compare with each other); 0 for other types.</summary>
        public double AsNumber => type == VariableType.Int ? intValue : type == VariableType.Float ? floatValue : 0d;

        /// <summary>
        /// Parses <paramref name="text"/> as <paramref name="targetType"/> (invariant culture): <c>true</c>, <c>3</c>,
        /// <c>2.5</c>, <c>word</c>, <c>1,2</c> / <c>(1, 2, 3)</c> for vectors, <c>#FF8800</c> or <c>1,0.5,0,1</c> for colours.
        /// An Object cannot be written as text except <c>null</c>: pass the reference with <see cref="Build"/>.
        /// </summary>
        public static bool TryParse(VariableType targetType, string text, out VariableValue value)
        {
            value = default;
            text = text?.Trim() ?? string.Empty;
            switch (targetType)
            {
                case VariableType.Bool:
                    if (!bool.TryParse(text, out bool b)) return false;
                    value = Bool(b);
                    return true;
                case VariableType.Int:
                    if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i)) return false;
                    value = Int(i);
                    return true;
                case VariableType.Float:
                    if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float f)) return false;
                    value = Float(f);
                    return true;
                case VariableType.Vector2:
                case VariableType.Vector3:
                {
                    int count = targetType == VariableType.Vector2 ? 2 : 3;
                    if (!TryParseFloats(text, count, count, out var parts)) return false;
                    value = new VariableValue { type = targetType, vectorValue = new Vector4(parts[0], parts[1], count > 2 ? parts[2] : 0f, 0f) };
                    return true;
                }
                case VariableType.Color:
                {
                    if (text.StartsWith("#", StringComparison.Ordinal))
                    {
                        if (!ColorUtility.TryParseHtmlString(text, out var html)) return false;
                        value = Color(html);
                        return true;
                    }
                    if (!TryParseFloats(text, 3, 4, out var c)) return false;
                    value = Color(new Color(c[0], c[1], c[2], c.Length > 3 ? c[3] : 1f));
                    return true;
                }
                case VariableType.Object:
                    if (!string.Equals(text, "null", StringComparison.OrdinalIgnoreCase) && text.Length > 0) return false;
                    value = Object(null);
                    return true;
                default:
                    value = String(text);
                    return true;
            }
        }

        /// <summary>A value of <paramref name="targetType"/> from a condition/action: Object types use <paramref name="reference"/>, the rest parse <paramref name="text"/>.</summary>
        public static bool Build(VariableType targetType, string text, Object reference, out VariableValue value)
        {
            if (targetType == VariableType.Object)
            {
                value = Object(reference);
                return true;
            }
            return TryParse(targetType, text, out value);
        }

        private static bool TryParseFloats(string text, int min, int max, out float[] values)
        {
            values = null;
            var parts = text.Trim('(', ')', ' ').Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < min || parts.Length > max) return false;
            values = new float[parts.Length];
            for (int i = 0; i < parts.Length; i++)
                if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out values[i])) return false;
            return true;
        }

        public bool Equals(VariableValue other)
        {
            if (IsNumber && other.IsNumber) return AsNumber.Equals(other.AsNumber);
            if (type != other.type) return false;
            switch (type)
            {
                case VariableType.Bool: return boolValue == other.boolValue;
                case VariableType.Vector2:
                case VariableType.Vector3: return vectorValue == other.vectorValue; // Unity's approximate equality
                case VariableType.Color: return colorValue == other.colorValue;
                case VariableType.Object: return objectValue == other.objectValue;
                default: return string.Equals(stringValue ?? string.Empty, other.stringValue ?? string.Empty, StringComparison.Ordinal);
            }
        }

        public override bool Equals(object obj) => obj is VariableValue other && Equals(other);

        public override int GetHashCode()
        {
            switch (type)
            {
                case VariableType.Int:
                case VariableType.Float: return AsNumber.GetHashCode();
                case VariableType.Bool: return boolValue.GetHashCode();
                case VariableType.Vector2:
                case VariableType.Vector3: return vectorValue.GetHashCode();
                case VariableType.Color: return colorValue.GetHashCode();
                case VariableType.Object: return objectValue != null ? objectValue.GetHashCode() : 0;
                default: return (stringValue ?? string.Empty).GetHashCode();
            }
        }

        public override string ToString()
        {
            var ci = CultureInfo.InvariantCulture;
            switch (type)
            {
                case VariableType.Bool: return boolValue ? "true" : "false";
                case VariableType.Int: return intValue.ToString(ci);
                case VariableType.Float: return floatValue.ToString(ci);
                case VariableType.Vector2: return string.Format(ci, "({0}, {1})", vectorValue.x, vectorValue.y);
                case VariableType.Vector3: return string.Format(ci, "({0}, {1}, {2})", vectorValue.x, vectorValue.y, vectorValue.z);
                case VariableType.Color: return "#" + ColorUtility.ToHtmlStringRGBA(colorValue);
                case VariableType.Object: return objectValue != null ? objectValue.name : "null";
                default: return stringValue ?? string.Empty;
            }
        }
    }
}
