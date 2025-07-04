// --- MODIFIED FILE: BehaviorTreeRunner.cs ---

using UnityEngine;
using ND_BehaviorTree;
using System.Linq; // Added for LINQ to easily get key names

public class BehaviorTreeRunner : MonoBehaviour
{
    [Tooltip("The BehaviorTree asset to run.")]
    public BehaviorTree treeAsset;
    
    [Tooltip("(Optional) A specific Blackboard asset to use for this runner. If left empty, a clone of the one from the Tree Asset will be used. Values can be set below.")]
    public Blackboard blackboardOverride;

    public BehaviorTree RuntimeTree { get; private set; }

    void Start()
    {
        if (treeAsset == null)
        {
            Debug.LogError("Behavior Tree asset is not assigned!", this);
            return;
        }

        // Clone the asset to create a runtime instance for this agent
        RuntimeTree = treeAsset.Clone();

        // If there is an override blackboard, clone it and replace the one on the runtime tree
        if (blackboardOverride != null)
        {
            RuntimeTree.blackboard = blackboardOverride.Clone();
        }
        
        // If blackboard is still null after the above, print a warning.
        if (RuntimeTree.blackboard == null)
        {
            Debug.LogWarning($"Runner for '{treeAsset.name}' on GameObject '{gameObject.name}' has no blackboard assigned (neither on the tree asset nor as an override).", this);
        }
        else // Otherwise, log the names of the keys it was initialized with.
        {
            // --- THIS IS THE CORRECTED LINE ---
            // Use key.keyName (the logical name) instead of key.name (the asset name)
            var keyNames = RuntimeTree.blackboard.keys.Select(key => key.keyName);
            string keysDebugString = string.Join(", ", keyNames);
            
            // Log a formatted message to the console. The 'this' context makes it clickable.
            Debug.Log($"[{gameObject.name}] BehaviorTreeRunner initialized with Blackboard keys: [{keysDebugString}]", this);
        }
    }

    void Update()
    {
        if (RuntimeTree != null)
        {
            RuntimeTree.Update();
        }
    }
}