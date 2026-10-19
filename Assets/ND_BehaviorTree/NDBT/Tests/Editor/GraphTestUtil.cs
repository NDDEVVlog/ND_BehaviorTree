using System.Collections.Generic;
using ND_BehaviorTree.Editor.Graph;
using NUnit.Framework;
using UnityEngine;

namespace ND_BehaviorTree.Editor.Tests
{
    /// <summary>Builds throwaway in-memory trees and tracks every object for cleanup.</summary>
    internal sealed class GraphFixture
    {
        public BehaviorTree Tree { get; private set; }
        public BehaviorTreeGraphService Graph { get; private set; }
        public RootNode Root { get; private set; }

        public GraphFixture()
        {
            Tree = ScriptableObject.CreateInstance<BehaviorTree>();
            Tree.name = "TestTree";
            Root = ScriptableObject.CreateInstance<RootNode>();
            Root.name = "Root";
            Tree.nodes.Add(Root);
            Tree.rootNode = Root;
            Graph = new BehaviorTreeGraphService(Tree);
            Graph.History.Capacity = 100; // independent of the project setting
            Current = this;
        }

        public T Create<T>(float x, float y = 0f) where T : Node
        {
            return (T)Graph.CreateNode(typeof(T), new Vector2(x, y));
        }

        public void Destroy()
        {
            Graph.History.Clear();
            var all = new List<Object>();
            foreach (var n in Tree.nodes)
            {
                if (n == null) continue;
                all.Add(n);
                all.AddRange(n.decorators);
                all.AddRange(n.services);
            }
            all.Add(Tree);
            foreach (var o in all) if (o != null) Object.DestroyImmediate(o);
        }

        /// <summary>The fixture created last (tests run one at a time); used by the static undo helpers.</summary>
        public static GraphFixture Current { get; private set; }

        /// <summary>Undoes one step of the current fixture's graph history (the graph no longer uses Unity's Undo).</summary>
        public static void UndoOnce() => Assert.IsTrue(Current.Graph.History.Undo(), "nothing to undo");

        public static void RedoOnce() => Assert.IsTrue(Current.Graph.History.Redo(), "nothing to redo");
    }
}
