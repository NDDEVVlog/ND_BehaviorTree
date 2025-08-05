// --- MODIFIED FILE: BehaviorTreeRunner.cs ---

using UnityEngine;
using ND_BehaviorTree;
using System.Linq;
using System.Collections.Generic;

public class BehaviorTreeRunner : MonoBehaviour
{
    [Tooltip("The BehaviorTree asset to run. The blackboard defined in this asset will be cloned and used.")]
    public BehaviorTree treeAsset;

    [Tooltip("Define overrides for this specific agent. These values will replace the template values at runtime.")]
    public List<KeyOverride> overrides = new List<KeyOverride>();

    public BehaviorTree RuntimeTree { get; private set; }

    void Start()
    {
        if (treeAsset == null)
        {
            Debug.LogError("Behavior Tree asset is not assigned!", this);
            return;
        }

        // 1. Clone the tree asset to create a unique instance for this agent.
        // This includes cloning the blackboard with its default values.
        RuntimeTree = treeAsset.Clone();
        RuntimeTree.Self = this.gameObject;

        // 2. Apply the per-instance overrides defined in the Inspector.
        // This is where scene references are injected.
        ApplyOverrides();

        // 3. Call the virtual Init() for any additional programmatic setup.
        Init();

        // 4. Log the result for debugging.
        if (RuntimeTree.blackboard == null)
        {
            Debug.LogWarning($"Runner for '{treeAsset.name}' on GameObject '{gameObject.name}' has no blackboard.", this);
        }
        else
        {
            Debug.Log($"[{gameObject.name}] BehaviorTreeRunner initialized.", this);
        }
    }

    /// <summary>
    /// Applies the values from the 'overrides' list to the runtime blackboard.
    /// </summary>
    private void ApplyOverrides()
    {
        if (RuntimeTree.blackboard == null) return;

        foreach (var keyOverride in overrides)
        {
            if (!string.IsNullOrEmpty(keyOverride.keyName) && keyOverride.data != null)
            {
                // This uses the generic SetValueObject method to handle any data type
                RuntimeTree.blackboard.SetValueObject(keyOverride.keyName, keyOverride.data.GetValue());
               
            }
        }
    }


    /// <summary>
    /// This method can be overridden in child classes to populate the blackboard
    /// with component references or initial values at runtime via code.
    /// It is called *after* the Inspector overrides have been applied.
    /// </summary>
    public virtual void Init()
    {
    }

    void Update()
    {
        if (RuntimeTree != null)
        {
            RuntimeTree.Update();
        }
    }
}