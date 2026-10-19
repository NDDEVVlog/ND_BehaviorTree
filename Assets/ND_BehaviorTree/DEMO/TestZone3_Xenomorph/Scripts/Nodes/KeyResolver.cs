using UnityEngine;

namespace ND_BehaviorTree.Demo.Xeno
{
    /// <summary>Resolves a node's key slot to the typed runtime key once, so per-tick reads/writes skip the name lookup.</summary>
    public static class KeyResolver
    {
        /// <summary>The runtime key for <paramref name="slot"/>, or null (logged) when it is unlinked, missing or of another type.</summary>
        public static Key<T> Resolve<T>(Node owner, Key slot, bool required = true)
        {
            if (slot == null)
            {
                if (required) Debug.LogError($"[{owner.GetType().Name}] '{owner.name}': a {typeof(T).Name} key slot is not linked.", owner);
                return null;
            }
            var blackboard = owner.blackboard;
            if (blackboard != null && blackboard.TryGetKey(slot.keyName, out var key) && key is Key<T> typed) return typed;
            Debug.LogError($"[{owner.GetType().Name}] '{owner.name}': key '{slot.keyName}' is missing from the runtime blackboard or is not {typeof(T).Name}.", owner);
            return null;
        }

        /// <summary>
        /// Copies the runtime value of <paramref name="from"/> into <paramref name="to"/> (same type). Common value types
        /// are copied without boxing. Returns false (logged) when a slot is unlinked/missing or the types differ.
        /// </summary>
        public static bool Copy(Node owner, Key from, Key to)
        {
            var blackboard = owner.blackboard;
            if (blackboard == null || from == null || to == null
                || !blackboard.TryGetKey(from.keyName, out var source) || !blackboard.TryGetKey(to.keyName, out var target))
            {
                Debug.LogError($"[{owner.GetType().Name}] '{owner.name}': 'from' or 'to' is not linked to a key of the runtime blackboard.", owner);
                return false;
            }
            if (source.GetValueType() != target.GetValueType())
            {
                Debug.LogError($"[{owner.GetType().Name}] '{owner.name}': cannot copy {source.GetValueType().Name} '{source.keyName}' into {target.GetValueType().Name} '{target.keyName}'.", owner);
                return false;
            }

            switch (source)
            {
                case Key<Vector3> v: ((Key<Vector3>)target).SetValue(v.GetValue()); break;
                case Key<float> f: ((Key<float>)target).SetValue(f.GetValue()); break;
                case Key<int> i: ((Key<int>)target).SetValue(i.GetValue()); break;
                case Key<bool> b: ((Key<bool>)target).SetValue(b.GetValue()); break;
                default: target.SetValueObject(source.GetValueObject()); break;
            }
            return true;
        }
    }
}
