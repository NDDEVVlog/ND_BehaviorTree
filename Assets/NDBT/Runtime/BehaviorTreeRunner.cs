// --- MODIFIED FILE: BehaviorTreeRunner.cs ---

using UnityEngine;
using ND_BehaviorTree;

public class BehaviorTreeRunner : MonoBehaviour
{
    [Tooltip("The BehaviorTree asset to run.")]
    public BehaviorTree treeAsset;
    
    // --- NEW ---
    [Tooltip("(Optional) A specific Blackboard instance to use for this runner. If left empty, the one from the Tree Asset will be used.")]
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

        // --- NEW: If there is an override blackboard, clone it and replace the one on the runtime tree ---
        if (blackboardOverride != null)
        {
            RuntimeTree.blackboard = blackboardOverride.Clone();
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