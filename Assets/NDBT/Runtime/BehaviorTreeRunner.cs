// --- MODIFIED FILE: BehaviorTreeRunner.cs ---

using UnityEngine;
using ND_BehaviorTree;

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
        //RuntimeTree.Bind(this.gameObject); // Bind the agent to the tree

        // If there is an override blackboard, clone it and replace the one on the runtime tree
        if (blackboardOverride != null)
        {
            // Clone the override blackboard to ensure this runner has its own instance of the state.
            RuntimeTree.blackboard = blackboardOverride.Clone();
        }
        
        // If blackboard is still null after the above (i.e., treeAsset.blackboard was null and no override was provided),
        // we can optionally create an empty one. For now, we assume one is provided.
        if (RuntimeTree.blackboard == null)
        {
            Debug.LogWarning($"Runner for '{treeAsset.name}' has no blackboard assigned (neither on the tree asset nor as an override).", this);
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