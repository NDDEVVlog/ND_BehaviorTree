using System;
using UnityEngine;

namespace ND_BehaviorTree.GOAP
{
    public enum GOAPValueType { Bool, Int, Float, GameObject, String /*, Vector3, etc. */ }
    public enum GOAPComparisonType { IsEqualTo, IsNotEqualTo, IsGreaterThan, IsLessThan }

    [Serializable]
    public class GOAPState
    {
        public string key;
        public GOAPValueType valueType = GOAPValueType.Bool;
        
        [Tooltip("How to compare the value for Ints and Floats.")]
        public GOAPComparisonType comparison = GOAPComparisonType.IsEqualTo;

        // --- Value fields ---
        public bool boolValue;
        public int intValue;
        public float floatValue;
        public GameObject gameObjectValue;
        public string stringValue;

        // Helper method to get the value as a generic object
        public object GetValue()
        {
            switch (valueType)
            {
                case GOAPValueType.Bool:       return boolValue;
                case GOAPValueType.Int:        return intValue;
                case GOAPValueType.Float:      return floatValue;
                case GOAPValueType.GameObject: return gameObjectValue;
                case GOAPValueType.String:     return stringValue;
                default:                       return null;
            }
        }
    }
}