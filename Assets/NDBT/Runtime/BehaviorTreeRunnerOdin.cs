// --- FINAL VERSION WITH TYPE VALIDATION: BehaviorTreeRunnerOdin.cs ---

using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;
using ND_BehaviorTree;
using Sirenix.OdinInspector.Editor;

public class BehaviorTreeRunnerOdin : SerializedMonoBehaviour
{
    // ... (no changes in this part of the class) ...
    [Title("Assets")]
    [Required("A BehaviorTree asset must be assigned.")]
    public BehaviorTree treeAsset;

    [Tooltip("(Optional) A specific Blackboard asset to use for this runner. If left empty, a clone of the one from the Tree Asset will be used.")]
    public Blackboard blackboardOverride;

    [Title("Runtime Blackboard Overrides")]
    [InfoBox("Dynamically override blackboard key values. This allows you to reference SCENE OBJECTS from your asset-based Behavior Tree.")]
    [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
    public List<BlackboardKeyOverride> blackboardValueOverrides = new List<BlackboardKeyOverride>();

    public BehaviorTree RuntimeTree { get; private set; }
    
    public Blackboard TargetBlackboard => blackboardOverride ?? (treeAsset != null ? treeAsset.blackboard : null);

    void Start()
    {
        if (treeAsset == null)
        {
            Debug.LogError("Behavior Tree asset is not assigned!", this);
            return;
        }

        // 1. Clone the asset to create a runtime instance for this agent
        RuntimeTree = treeAsset.Clone();

        // 2. If there is an override blackboard, clone it and replace the default one
        //    This must happen BEFORE Init() so we populate the correct blackboard.
        if (blackboardOverride != null)
        {
            RuntimeTree.blackboard = blackboardOverride.Clone();
        }

        // 3. Now that the finalblackboard is in place, initialize it with runtime values.
        //Init();

        // If blackboard is still null after the above, print a warning.
        if (RuntimeTree.blackboard == null)
        {
            Debug.LogWarning($"Runner for '{treeAsset.name}' on GameObject '{gameObject.name}' has no blackboard assigned (neither on the tree asset nor as an override).", this);
        }
        else // Otherwise, log th e names of the keys it was initialized with.
        {
            var keyNames = RuntimeTree.blackboard.keys.Select(key => key.keyName);
            string keysDebugString = string.Join(", ", keyNames);

            // Log a formatted message to the console. The 'this' context makes it clickable.
            Debug.Log($"[{gameObject.name}] BehaviorTreeRunner initialized with Blackboard keys: [{keysDebugString}]", this);
        }
    }
    public virtual void InitializeBlackboardOverrides() { /* ... no changes ... */ }
    void Update()
    {
        RuntimeTree.Update();

     }
}

public class BlackboardKeyOverride
{
    [ValueDropdown("GetKeyNamesForDropdown")]
    [OnValueChanged("OnKeyChanged")]
    [HorizontalGroup("Row", Width = 150)]
    [HideLabel]
    public string keyName;

    [OdinSerialize]
    private object value;
    
    [ShowInInspector]
    [HorizontalGroup("Row")]
    [HideLabel]
    [ShowIf("HasKeyName")]
    // --- NEW: Add the validation attribute here ---
    [ValidateInput("IsValueTypeValid", "The assigned value is not compatible with the key's type.", InfoMessageType.Error)]
    public object Value
    {
        get => this.value;
        set => this.value = value;
    }

    // --- ODIN HELPER AND CALLBACK METHODS ---

    private bool HasKeyName() => !string.IsNullOrEmpty(keyName);
    
    [NonSerialized]
    private BehaviorTreeRunnerOdin _parentRunner;
    
    [OnInspectorInit]
    private void OnInspectorInit(InspectorProperty property)
    {
        if (property.Tree.WeakTargets.Count > 0)
        {
            _parentRunner = property.Tree.WeakTargets[0] as BehaviorTreeRunnerOdin;
        }
    }
    
    private void OnKeyChanged()
    {
        // ... (no changes in this method) ...
        if (!HasKeyName() || _parentRunner == null)
        {
            this.value = null;
            return;
        }
        
        var blackboard = _parentRunner.TargetBlackboard;
        if (blackboard == null) return;

        Type targetType = blackboard.keys.FirstOrDefault(k => k.keyName == this.keyName)?.GetValueType();

        if (targetType != null)
        {
            this.value = targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
        }
        else
        {
            this.value = null;
        }
    }

    private IEnumerable<ValueDropdownItem> GetKeyNamesForDropdown()
    {
        // ... (no changes in this method) ...
        if (_parentRunner == null || _parentRunner.TargetBlackboard == null)
        {
            yield return new ValueDropdownItem("No Blackboard Found", null);
            yield break;
        }

        foreach (var key in _parentRunner.TargetBlackboard.keys)
        {
            if (key != null && !string.IsNullOrEmpty(key.keyName))
            {
                yield return new ValueDropdownItem($"{key.keyName} ({key.GetValueType().Name})", key.keyName);
            }
        }
    }

    // --- NEW: The validation method called by [ValidateInput] ---
    private bool IsValueTypeValid(object val, ref string errorMessage)
    {
        // If no key is selected or the value is null, it's valid.
        if (!HasKeyName() || val == null) return true;
        
        // Find the type required by the selected key.
        var blackboard = _parentRunner?.TargetBlackboard;
        if (blackboard == null) return true; // Can't validate without a blackboard.
        
        Type expectedType = blackboard.keys.FirstOrDefault(k => k.keyName == this.keyName)?.GetValueType();
        if (expectedType == null) return true; // Can't find the key, can't validate.

        Type actualType = val.GetType();

        // 1. Direct Type Match or Inheritance:
        // This checks if 'val' is an instance of 'expectedType' or a class that inherits from it.
        // This works for float, int, string, Vector3, custom classes, etc.
        if (expectedType.IsAssignableFrom(actualType))
        {
            return true;
        }
        
        // 2. Special Case: Assigning a GameObject to a Component Key
        // This is a very common use case. The key wants a 'Rigidbody', but you drag the 'Player' GameObject.
        if (typeof(Component).IsAssignableFrom(expectedType) && val is GameObject go)
        {
            // Check if the GameObject has the required component type.
            if (go.GetComponent(expectedType) != null)
            {
                return true; // The GameObject has the component, this is a valid assignment.
            }

            // The GameObject is missing the component. This is an error.
            errorMessage = $"The assigned GameObject '{go.name}' does not have the required '{expectedType.Name}' component.";
            return false;
        }

        // 3. If all checks fail, the types are incompatible.
        return false; // Let the default error message from the attribute be used.
    }
}