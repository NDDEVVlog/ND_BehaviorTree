using System.Linq;
using ND_BehaviorTree.Editor.Graph;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ND_BehaviorTree.Editor.Tests
{
    /// <summary>Same operations on a real .asset file: sub-asset registration must survive undo/redo and save.</summary>
    public class GraphServiceAssetTests
    {
        private const string Folder = "Assets/__NDBT_TestTemp";
        private string m_Path;
        private BehaviorTree m_Tree;
        private BehaviorTreeGraphService m_Graph;

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "__NDBT_TestTemp");
            m_Path = AssetDatabase.GenerateUniqueAssetPath($"{Folder}/Tree.asset");
            m_Tree = ScriptableObject.CreateInstance<BehaviorTree>();
            AssetDatabase.CreateAsset(m_Tree, m_Path);
            m_Tree.EditorInit();
            m_Graph = new BehaviorTreeGraphService(m_Tree);
            Undo.ClearAll();
        }

        [TearDown]
        public void TearDown()
        {
            Undo.ClearAll();
            AssetDatabase.DeleteAsset(Folder);
        }

        [Test]
        public void CreatedNode_IsSubAsset_AndSurvivesUndoRedoAndSave()
        {
            var node = m_Graph.CreateNode(typeof(SequenceNode), Vector2.zero);
            string id = node.id;
            Assert.AreEqual(m_Path, AssetDatabase.GetAssetPath(node));

            Assert.IsTrue(m_Graph.History.Undo());
            Assert.IsFalse(m_Tree.nodes.Any(n => n != null && n.id == id));

            Assert.IsTrue(m_Graph.History.Redo());
            var redone = m_Tree.nodes.SingleOrDefault(n => n != null && n.id == id);
            Assert.IsNotNull(redone);
            Assert.AreEqual(m_Path, AssetDatabase.GetAssetPath(redone), "redo keeps the node inside the tree asset");

            m_Graph.Save();
            var onDisk = AssetDatabase.LoadAllAssetsAtPath(m_Path).OfType<Node>().Select(n => n.id).ToList();
            CollectionAssert.Contains(onDisk, id);
        }

        [Test]
        public void PastedNodes_AreSubAssetsOfTargetTree()
        {
            var seq = m_Graph.CreateNode(typeof(SequenceNode), Vector2.zero);
            var wait = m_Graph.CreateNode(typeof(WaitNode), new Vector2(100, 100));
            m_Graph.Connect(seq, wait);

            using (var clip = GraphClipboard.Capture(m_Tree, new Node[] { seq, wait }))
            {
                var pasted = m_Graph.Paste(clip, new Vector2(500, 0));
                Assert.IsTrue(pasted.All(p => AssetDatabase.GetAssetPath(p) == m_Path));
            }
        }
    }
}
