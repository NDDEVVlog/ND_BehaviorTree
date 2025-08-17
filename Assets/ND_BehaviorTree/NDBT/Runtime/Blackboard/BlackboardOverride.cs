using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEngine.Events;

namespace ND_BehaviorTree
{
    [Serializable]
    public class KeyOverride
    {
        public string keyName;
        [SerializeReference]
        public KeyOverrideData data;
    }

    [Serializable]
    public abstract class KeyOverrideData
    {
        public abstract object GetValue();
    }
    
    // ####################################################################
    // ############### NEW GENERIC WRAPPER FOR STRUCTS/VALUES ###############
    // ####################################################################
    
    /// <summary>
    /// A generic, serializable wrapper for overriding any value type (like structs)
    /// in the BlackboardOverride system. The reflection in KeyOverrideDrawer looks for the 'value' field.
    /// </summary>
    [Serializable]
    public abstract class OverrideDataValue<T> : KeyOverrideData
    {
        public T value;
        public override object GetValue() => value;
    }

    // ####################################################################
    // ##################### BUILT-IN TYPE SUPPORT ########################
    // ####################################################################
    
    // --- Primitive Types ---
    [Serializable] public class OverrideDataFloat : OverrideDataValue<float> {}
    [Serializable] public class OverrideDataInt : OverrideDataValue<int> {}
    [Serializable] public class OverrideDataBool : OverrideDataValue<bool> {}
    [Serializable] public class OverrideDataString : OverrideDataValue<string> {}
    [Serializable] public class OverrideDataDouble : OverrideDataValue<double> {}
    [Serializable] public class OverrideDataLong : OverrideDataValue<long> {}
    [Serializable] public class OverrideDataChar : OverrideDataValue<char> {}
    [Serializable] public class OverrideDataShort : OverrideDataValue<short> {}
    [Serializable] public class OverrideDataByte : OverrideDataValue<byte> {}
    [Serializable] public class OverrideDataSByte : OverrideDataValue<sbyte> {}
    [Serializable] public class OverrideDataUInt : OverrideDataValue<uint> {}
    [Serializable] public class OverrideDataULong : OverrideDataValue<ulong> {}
    [Serializable] public class OverrideDataUShort : OverrideDataValue<ushort> {}

    // --- Unity Structs ---
    [Serializable] public class OverrideDataVector2 : OverrideDataValue<Vector2> {}
    [Serializable] public class OverrideDataVector3 : OverrideDataValue<Vector3> {}
    [Serializable] public class OverrideDataVector4 : OverrideDataValue<Vector4> {}
    [Serializable] public class OverrideDataQuaternion : OverrideDataValue<Quaternion> {}
    [Serializable] public class OverrideDataRect : OverrideDataValue<Rect> {}
    [Serializable] public class OverrideDataBounds : OverrideDataValue<Bounds> {}
    [Serializable] public class OverrideDataColor : OverrideDataValue<Color> {}
    [Serializable] public class OverrideDataAnimationCurve : OverrideDataValue<AnimationCurve> {}
    [Serializable] public class OverrideDataGradient : OverrideDataValue<Gradient> {}

    // --- Unity Objects ---
    [Serializable] public class OverrideDataObject : OverrideDataValue<UnityEngine.Object> {}
    [Serializable] public class OverrideDataGameObject : OverrideDataValue<GameObject> {}
    [Serializable] public class OverrideDataTransform : OverrideDataValue<Transform> {}
    [Serializable] public class OverrideDataRigidbody : OverrideDataValue<Rigidbody> {}
    [Serializable] public class OverrideDataCollider : OverrideDataValue<Collider> {}
    [Serializable] public class OverrideDataCamera : OverrideDataValue<Camera> {}
    [Serializable] public class OverrideDataLight : OverrideDataValue<Light> {}
    [Serializable] public class OverrideDataAudioSource : OverrideDataValue<AudioSource> {}
    [Serializable] public class OverrideDataParticleSystem : OverrideDataValue<ParticleSystem> {}
    [Serializable] public class OverrideDataAudioClip : OverrideDataValue<AudioClip> {}

    // --- System Types ---
    [Serializable] public class OverrideDataDateTime : OverrideDataValue<DateTime> {}
    [Serializable] public class OverrideDataTimeSpan : OverrideDataValue<TimeSpan> {}
    
    // --- Special Handlers ---
    [Serializable] public class OverrideDataUnityEvent : OverrideDataValue<UnityEvent> {}
    
    [Serializable]
    public class OverrideDataEnum : KeyOverrideData
    {
        public int value;
        public string enumType;
        public override object GetValue()
        {
            if (string.IsNullOrEmpty(enumType)) return null;
            Type type = Type.GetType(enumType);
            return type != null ? Enum.ToObject(type, value) : null;
        }
    }
    
    // --- Generic List Wrappers ---
    public abstract class OverrideDataList<T> : KeyOverrideData
    {
        public List<T> value = new List<T>();
        public override object GetValue() => value;
    }
    [Serializable] public class OverrideDataListFloat : OverrideDataList<float> {}
    [Serializable] public class OverrideDataListString : OverrideDataList<string> {}
    
    // ####################################################################
    // ##################### YOUR CUSTOM TYPE SUPPORT #####################
    // ####################################################################
    

    [Serializable]
    public class OverrideDataPatrolRoute : OverrideDataValue<PatrolRoute> { }
}