using System.Collections.Generic;
using System.Linq;
using ND_BehaviorTree;
using ND_BehaviorTree.Editor.Graph;
using ND_BehaviorTree.GOAP;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Rebuilds the TestZone2 GOAP demo tree (TestGoap.asset) from scratch: a woodcutter with two goals.
///   Stay healthy (priority 10): Health >= 40
///   Collect logs (priority 1):  NumberOfLog >= 50
/// Chopping costs 20 health: after the 4th chop "Stay healthy" takes over the running plan (Eat), then logging resumes.
/// The action subtrees only wait and log: effects are written by the planner.
/// </summary>
public static class GoapDemoBuilder
{
    private const string TreePath = "Assets/ND_BehaviorTree/DEMO/TestZone2/TestGoap.asset";

    [MenuItem("ND_BehaviorTree/Demo/Rebuild GOAP Demo Tree (TestZone2)")]
    public static void Build()
    {
        var tree = AssetDatabase.LoadAssetAtPath<BehaviorTree>(TreePath);
        if (tree == null || tree.blackboard == null)
        {
            Debug.LogError($"[GoapDemoBuilder] {TreePath} or its blackboard is missing.");
            return;
        }
        var bb = tree.blackboard;
        var hasAxe = RequireKey<BoolKey>(bb, "HasWeapon");
        var treeFound = RequireKey<BoolKey>(bb, "IsThereAnyTreeAround");
        var logs = RequireKey<IntKey>(bb, "NumberOfLog");
        var health = RequireKey<IntKey>(bb, "Health");
        if (hasAxe == null || treeFound == null || logs == null || health == null) return;

        // Starting values of the demo.
        hasAxe.SetValue(false);
        treeFound.SetValue(false);
        logs.SetValue(0);
        health.SetValue(100);
        foreach (var key in new Key[] { hasAxe, treeFound, logs, health }) EditorUtility.SetDirty(key);

        var graph = new BehaviorTreeGraphService(tree);
        using (new GraphUndoScope(tree, "Rebuild GOAP Demo"))
        {
            var old = tree.nodes.Where(n => n != null && !(n is RootNode)).ToList();
            if (old.Count > 0) graph.DeleteNodes(old);

            var planner = (GOAPPlannerNode)graph.CreateNode(typeof(GOAPPlannerNode), new Vector2(700, 420));
            planner.name = planner.typeName = "Woodcutter Planner";
            planner.logPlans = true;
            planner.goals = new List<GoapGoal>
            {
                new GoapGoal { name = "Stay healthy", priority = 10, conditions = { Cond(health, ComparisonType.GreaterEqual, i: 40) } },
                new GoapGoal { name = "Collect logs", priority = 1, conditions = { Cond(logs, ComparisonType.GreaterEqual, i: 50) } },
            };
            graph.Connect(tree.rootNode, planner);

            Action(graph, planner, "Get Axe", 1, 0, 1f, "Picked up an axe",
                new[] { Cond(hasAxe, ComparisonType.Equal, b: false) },
                Set(hasAxe, b: true));
            Action(graph, planner, "Scout For Tree", 2, 1, 1.5f, "Found a tree",
                new[] { Cond(treeFound, ComparisonType.Equal, b: false) },
                Set(treeFound, b: true));
            Action(graph, planner, "Chop Tree", 1, 2, 2f, "Chopped a tree: +10 logs, -20 health",
                new[] { Cond(hasAxe, ComparisonType.Equal, b: true), Cond(treeFound, ComparisonType.Equal, b: true), Cond(health, ComparisonType.GreaterEqual, i: 20) },
                Add(logs, 10), Set(treeFound, b: false), Add(health, -20));
            Action(graph, planner, "Eat", 1, 3, 1f, "Ate: health restored",
                new KeyCondition[0],
                Set(health, i: 100));

            EditorUtility.SetDirty(planner);
        }
        EditorUtility.SetDirty(tree);
        AssetDatabase.SaveAssets();
        Debug.Log($"[GoapDemoBuilder] Rebuilt {TreePath}. Open TestZone2 and press Play: the planner logs each plan.", tree);
    }

    private static void Action(BehaviorTreeGraphService graph, GOAPPlannerNode planner, string name, float cost, int column, float seconds, string message,
        KeyCondition[] preconditions, params KeyEffect[] effects)
    {
        float x = 250 + column * 300;
        var action = (GOAPActionNode)graph.CreateNode(typeof(GOAPActionNode), new Vector2(x, 620));
        action.name = action.typeName = name;
        action.cost = cost;
        action.preconditions = preconditions.ToList();
        action.effects = effects.ToList();
        graph.Connect(planner, action);

        var sequence = graph.CreateNode(typeof(SequenceNode), new Vector2(x, 820));
        graph.Connect(action, sequence);
        var wait = (WaitNode)graph.CreateNode(typeof(WaitNode), new Vector2(x - 80, 980));
        wait.duration = seconds;
        graph.Connect(sequence, wait);
        var log = (DebugLogNode)graph.CreateNode(typeof(DebugLogNode), new Vector2(x + 80, 980));
        log.message = $"[Woodcutter] {message}";
        graph.Connect(sequence, log);
        foreach (var n in new Node[] { action, sequence, wait, log }) EditorUtility.SetDirty(n);
    }

    private static T RequireKey<T>(Blackboard bb, string name) where T : Key
    {
        if (bb.TryGetKey(name, out var key) && key is T typed) return typed;
        Debug.LogError($"[GoapDemoBuilder] Blackboard '{bb.name}' needs a {typeof(T).Name} named '{name}'.", bb);
        return null;
    }

    private static KeyCondition Cond(Key key, ComparisonType op, bool b = false, int i = 0) =>
        new KeyCondition { key = key, comparison = op, value = new GoapValue { boolValue = b, intValue = i } };

    private static KeyEffect Set(Key key, bool b = false, int i = 0) =>
        new KeyEffect { key = key, operation = GoapEffectOperation.Set, value = new GoapValue { boolValue = b, intValue = i } };

    private static KeyEffect Add(Key key, int amount) =>
        new KeyEffect { key = key, operation = GoapEffectOperation.Add, value = new GoapValue { intValue = amount } };
}
