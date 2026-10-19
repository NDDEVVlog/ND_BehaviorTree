// --- MODIFIED FILE: BehaviorTreeRunner.cs ---

using UnityEngine;
using ND_BehaviorTree;
using System.Collections.Generic;

public class BehaviorTreeRunner : MonoBehaviour
{
    [Tooltip("The BehaviorTree asset to run. The blackboard defined in this asset will be cloned and used.")]
    public BehaviorTree treeAsset;

    [Tooltip("Define overrides for this specific agent. These values will replace the template values at runtime.")]
    public List<KeyOverride> overrides = new List<KeyOverride>();

    [Tooltip("Log a line when this agent's tree is initialized. Off by default: with many agents the strings and console entries add GC and noise.")]
    [SerializeField] private bool logInitialization;

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

        // 3b. Run the tree's optional Init node branch (after overrides and Init(), so it can read their values).
        //     If it is still Running, Update keeps ticking it before the main tree starts.
        RuntimeTree.RunInitialization();

        // 4. Missing blackboard is always worth a warning; the success line is opt-in (no per-agent string/log by default).
        if (RuntimeTree.blackboard == null)
        {
            Debug.LogWarning($"Runner for '{treeAsset.name}' on GameObject '{gameObject.name}' has no blackboard.", this);
        }
        else if (logInitialization)
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

    /// <summary>
    /// Runs the EventNode(s) with this name on this agent's runtime tree (starts on the next Update).
    /// Main thread only. Returns false if the tree is not running yet or no EventNode has that name.
    /// </summary>
    public bool SendEvent(string eventName)
    {
        if (RuntimeTree == null)
        {
            Debug.LogWarning($"[BehaviorTreeRunner] SendEvent('{eventName}') ignored on '{name}': the runtime tree is not created yet (Start has not run, or no tree asset).", this);
            return false;
        }
        return RuntimeTree.SendEvent(eventName);
    }

    // Shows each agent's tick (and any GC it allocates) as "ND_BehaviorTree.Tick" in the Profiler.
    private static readonly Unity.Profiling.ProfilerMarker s_TickMarker = new Unity.Profiling.ProfilerMarker("ND_BehaviorTree.Tick");

    void Update()
    {
        if (RuntimeTree != null)
        {
            using (s_TickMarker.Auto())
            {
                RuntimeTree.Update();
            }
        }
    }

    /// <summary>
    /// Destroys this agent's runtime clones (tree, nodes incl. attached decorators/services, blackboard + keys,
    /// and subtree runtime clones). Assets are never touched.
    /// </summary>
    void OnDestroy()
    {
        if (RuntimeTree == null) return;
        DestroyRuntimeTree(RuntimeTree, null, new HashSet<BehaviorTree>());
        RuntimeTree = null;
    }

    /// <param name="sharedBlackboard">The parent's runtime blackboard; a subtree that uses it must not destroy it.</param>
    private static void DestroyRuntimeTree(BehaviorTree tree, Blackboard sharedBlackboard, HashSet<BehaviorTree> visited)
    {
        if (tree == null || !visited.Add(tree)) return;

        foreach (var node in tree.nodes)
        {
            if (node is SubtreeNode subtreeNode && subtreeNode.RuntimeSubtree != null)
            {
                DestroyRuntimeTree(subtreeNode.RuntimeSubtree, tree.blackboard, visited);
            }
        }

        foreach (var node in tree.nodes)
        {
            DestroyRuntimeObject(node);
        }

        var blackboard = tree.blackboard;
        if (blackboard != null && blackboard != sharedBlackboard && !IsAsset(blackboard))
        {
            foreach (var key in blackboard.keys) DestroyRuntimeObject(key);
            DestroyRuntimeObject(blackboard);
        }

        DestroyRuntimeObject(tree);
    }

    private static void DestroyRuntimeObject(Object obj)
    {
        if (obj == null || IsAsset(obj)) return;
        if (Application.isPlaying) Destroy(obj);
        else DestroyImmediate(obj);
    }

    private static bool IsAsset(Object obj)
    {
#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.Contains(obj);
#else
        return false;
#endif
    }
}