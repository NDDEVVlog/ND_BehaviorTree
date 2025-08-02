// --- MODIFIED FILE: BehaviorTreeRunner.cs ---

using UnityEngine;
using ND_BehaviorTree;
using System.Linq;

public class BehaviorTreeRunner : MonoBehaviour
{
    [Tooltip("The BehaviorTree asset to run. The blackboard defined in this asset will be cloned and used.")]
    public BehaviorTree treeAsset;

    // The 'blackboardOverride' field has been removed to simplify the component.

    public BehaviorTree RuntimeTree { get; private set; }

    void Start()
    {
        if (treeAsset == null)
        {
            Debug.LogError("Behavior Tree asset is not assigned!", this);
            return;
        }

        RuntimeTree = treeAsset.Clone();
        RuntimeTree.Self = this.gameObject;
        

        // 2. Now that the tree and its blackboard are cloned, initialize it with runtime values.
        Init();

        // 3. Log the result for debugging purposes.
        if (RuntimeTree.blackboard == null)
        {
            Debug.LogWarning($"Runner for '{treeAsset.name}' on GameObject '{gameObject.name}' has no blackboard assigned to the tree asset.", this);
        }
        else
        {
            var keyNames = RuntimeTree.blackboard.keys.Select(key => key.name);
            string keysDebugString = string.Join(", ", keyNames);
            Debug.Log($"[{gameObject.name}] BehaviorTreeRunner initialized with Blackboard keys: [{keysDebugString}]", this);
        }
    }

    /// <summary>
    /// This method can be overridden in child classes to populate the blackboard
    /// with component references or initial values at runtime.
    /// It is called after the tree and blackboard have been cloned.
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