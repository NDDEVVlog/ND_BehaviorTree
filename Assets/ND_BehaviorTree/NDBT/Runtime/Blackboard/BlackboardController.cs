// FILE: BlackboardController.cs (RUNTIME-SAFE AND CORRECTED)

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
    [Serializable] public class OverrideDataObject : KeyOverrideData { public UnityEngine.Object value; public override object GetValue() => value; }
    [Serializable] public class OverrideDataTransform : KeyOverrideData { public Transform value; public override object GetValue() => value; }
    [Serializable] public class OverrideDataEnum : KeyOverrideData 
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

}