////////////////////////////////////////////////////////////////////////////
//
// FB: https://www.facebook.com/profile.php?id=100090693452227
// Github: https://github.com/NDDEVVlog/ND_BehaviorTree
//
////////////////////////////////////////////////////////////////////////////

using System;
using UnityEngine;
using ND_BehaviorTree;

namespace ND_BehaviorTree
{
    [NodeDescription("Resets a blackboard key to its default (null / 0 / false).", "Fails if the key is not on the runtime blackboard.")]
    [NodeInfo("Clear Blackboard Key", "Action/Blackboard/ClearKey", true, false, iconPath: "Assets/ND_BehaviorTree/NDBT/Icons/ClearKey.png")]
    public class ClearBlackboardKeyNode : ActionNode
    {
        [Tooltip("The Blackboard key to clear (reference types are set to null, value types to their default).")]
        public Key keyToClear;

        [NonSerialized] private bool m_MissingKeyLogged;

        protected override Status OnProcess()
        {
            if (keyToClear == null || blackboard == null) return Status.Failure;

            // Works for every key type (SetValue<object> only matched ObjectKey).
            if (!blackboard.TryGetKey(keyToClear.keyName, out var key))
            {
                if (!m_MissingKeyLogged)
                {
                    m_MissingKeyLogged = true;
                    Debug.LogWarning($"[ClearBlackboardKeyNode] '{name}': key '{keyToClear.keyName}' not found in the blackboard. (Logged once.)", this);
                }
                return Status.Failure;
            }

            Type valueType = key.GetValueType();
            object cleared = valueType != null && valueType.IsValueType && Nullable.GetUnderlyingType(valueType) == null
                ? Activator.CreateInstance(valueType)
                : null;
            key.SetValueObject(cleared);
            return Status.Success;
        }
    }
}
