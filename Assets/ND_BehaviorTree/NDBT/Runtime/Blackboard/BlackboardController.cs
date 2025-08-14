
using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEngine.Events; // Thêm dòng này để hỗ trợ UnityEvent

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

    // Các lớp con cho các kiểu dữ liệu được hỗ trợ trực tiếp.
    // Chúng ta có thể thêm nhiều hơn nếu cần.
    [Serializable] public class OverrideDataFloat : KeyOverrideData { public float value; public override object GetValue() => value; }
    [Serializable] public class OverrideDataInt : KeyOverrideData { public int value; public override object GetValue() => value; }
    [Serializable] public class OverrideDataBool : KeyOverrideData { public bool value; public override object GetValue() => value; }
    [Serializable] public class OverrideDataString : KeyOverrideData { public string value; public override object GetValue() => value; }
    [Serializable] public class OverrideDataVector3 : KeyOverrideData { public Vector3 value; public override object GetValue() => value; }
    [Serializable] public class OverrideDataVector2 : KeyOverrideData { public Vector2 value; public override object GetValue() => value; }
    [Serializable] public class OverrideDataVector4 : KeyOverrideData { public Vector4 value; public override object GetValue() => value; }
    [Serializable] public class OverrideDataQuaternion : KeyOverrideData { public Quaternion value; public override object GetValue() => value; }
    [Serializable] public class OverrideDataRect : KeyOverrideData { public Rect value; public override object GetValue() => value; }
    [Serializable] public class OverrideDataBounds : KeyOverrideData { public Bounds value; public override object GetValue() => value; }
    [Serializable] public class OverrideDataColor : KeyOverrideData { public Color value; public override object GetValue() => value; }
    [Serializable] public class OverrideDataDouble : KeyOverrideData { public double value; public override object GetValue() => value; }
    [Serializable] public class OverrideDataChar : KeyOverrideData { public char value; public override object GetValue() => value; }
    [Serializable] public class OverrideDataByte : KeyOverrideData { public byte value; public override object GetValue() => value; }
    [Serializable] public class OverrideDataShort : KeyOverrideData { public short value; public override object GetValue() => value; }
    [Serializable] public class OverrideDataLong : KeyOverrideData { public long value; public override object GetValue() => value; }
    [Serializable] public class OverrideDataULong : KeyOverrideData { public ulong value; public override object GetValue() => value; }
    [Serializable] public class OverrideDataUInt : KeyOverrideData { public uint value; public override object GetValue() => value; }
    [Serializable] public class OverrideDataUShort : KeyOverrideData { public ushort value; public override object GetValue() => value; }
    [Serializable] public class OverrideDataSByte : KeyOverrideData { public sbyte value; public override object GetValue() => value; }
    [Serializable] public class OverrideDataObject : KeyOverrideData { public UnityEngine.Object value; public override object GetValue() => value; }
    [Serializable] public class OverrideDataTransform : KeyOverrideData { public Transform value; public override object GetValue() => value; }
    [Serializable] public class OverrideDataGameObject : KeyOverrideData { public GameObject value; public override object GetValue() => value; }
    [Serializable] public class OverrideDataRigidbody : KeyOverrideData { public Rigidbody value; public override object GetValue() => value; }
    [Serializable] public class OverrideDataCollider : KeyOverrideData { public Collider value; public override object GetValue() => value; }
    [Serializable] public class OverrideDataCamera : KeyOverrideData { public Camera value; public override object GetValue() => value; }
    [Serializable] public class OverrideDataLight : KeyOverrideData { public Light value; public override object GetValue() => value; }
    [Serializable] public class OverrideDataAudioSource : KeyOverrideData { public AudioSource value; public override object GetValue() => value; }
    [Serializable] public class OverrideDataParticleSystem : KeyOverrideData { public ParticleSystem value; public override object GetValue() => value; }
    [Serializable] public class OverrideDataAnimationCurve : KeyOverrideData { public AnimationCurve value; public override object GetValue() => value; }
    [Serializable] public class OverrideDataGradient : KeyOverrideData { public Gradient value; public override object GetValue() => value; }
    [Serializable] public class OverrideDataDateTime : KeyOverrideData { public DateTime value; public override object GetValue() => value; }
    [Serializable] public class OverrideDataTimeSpan : KeyOverrideData { public TimeSpan value; public override object GetValue() => value; }
    [Serializable] public class OverideDataAudioClip : KeyOverrideData
    {
        public AudioClip value;
        public override object GetValue() => value;
    }
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
    // Lớp đặc biệt để xử lý UnityEvent
    [Serializable] public class OverrideDataUnityEvent : KeyOverrideData { public UnityEvent value; public override object GetValue() => value; }

    public abstract class OverrideDataList<T> : KeyOverrideData
    {
        public List<T> value = new List<T>();
        public override object GetValue() => value;
    }
    [Serializable] public class OverrideDataListFloat : OverrideDataList<float> {}
    [Serializable] public class OverrideDataListString : OverrideDataList<string> {}

    
}