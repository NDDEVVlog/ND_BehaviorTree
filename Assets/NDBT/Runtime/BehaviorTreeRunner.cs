// --- START OF FILE BehaviorTreeRunner.cs ---

using UnityEngine;
using ND_BehaviorTree;

public class BehaviorTreeRunner : MonoBehaviour
{
    // Assign your BehaviorTree asset in the Unity Inspector
    public BehaviorTree treeAsset;

    // MODIFIED: Changed from private field to a public property with a private setter.
    // This is the "live" copy of the tree that this agent will run.
    public BehaviorTree RuntimeTree { get; private set; }

    void Start()
    {
        if (treeAsset == null)
        {
            Debug.LogError("Behavior Tree asset is not assigned!", this);
            return;
        }

        // Clone the asset to create a runtime instance for this agent
        // MODIFIED: Use the 'RuntimeTree' property
        RuntimeTree = treeAsset.Clone();
    }

    void Update()
    {
        // Update the tree every frame
        // MODIFIED: Use the 'RuntimeTree' property
        if (RuntimeTree != null)
        {
            RuntimeTree.Update();
        }
    }
}
// --- END OF FILE BehaviorTreeRunner.cs ---