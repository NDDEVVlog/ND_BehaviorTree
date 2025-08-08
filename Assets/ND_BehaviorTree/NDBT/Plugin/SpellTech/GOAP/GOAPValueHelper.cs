using UnityEngine;

namespace ND_BehaviorTree.GOAP
{
    public static class GOAPValueHelper
    {
        public static string GetValueFieldName(GOAPValueType type)
        {
            switch (type)
            {
                case GOAPValueType.Bool:       return "boolValue";
                case GOAPValueType.Int:        return "intValue";
                case GOAPValueType.Float:      return "floatValue";
                case GOAPValueType.GameObject: return "gameObjectValue";
                case GOAPValueType.String:     return "stringValue";
                default:                       return null;
            }
        }

        public static bool CompareValues(object worldValue, GOAPState condition)
        {
            if (worldValue == null) return condition.GetValue() == null;

            switch (condition.valueType)
            {
                case GOAPValueType.Bool:
                    if (worldValue is bool b) return b == condition.boolValue;
                    break;

                case GOAPValueType.GameObject:
                    if (worldValue is GameObject go) return go == condition.gameObjectValue;
                    if (worldValue is Transform t) return t.gameObject == condition.gameObjectValue;
                    break;

                case GOAPValueType.String:
                     if (worldValue is string s) return s == condition.stringValue;
                    break;

                case GOAPValueType.Int:
                    if (worldValue is int i)
                    {
                        switch (condition.comparison)
                        {
                            case GOAPComparisonType.IsEqualTo:    return i == condition.intValue;
                            case GOAPComparisonType.IsNotEqualTo: return i != condition.intValue;
                            case GOAPComparisonType.IsGreaterThan:return i > condition.intValue;
                            case GOAPComparisonType.IsLessThan:   return i < condition.intValue;
                        }
                    }
                    break;

                case GOAPValueType.Float:
                    if (worldValue is float f)
                    {
                        switch (condition.comparison)
                        {
                            case GOAPComparisonType.IsEqualTo:    return Mathf.Approximately(f, condition.floatValue);
                            case GOAPComparisonType.IsNotEqualTo: return !Mathf.Approximately(f, condition.floatValue);
                            case GOAPComparisonType.IsGreaterThan:return f > condition.floatValue;
                            case GOAPComparisonType.IsLessThan:   return f < condition.floatValue;
                        }
                    }
                    break;
            }
            return false;
        }
    }
}