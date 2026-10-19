using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ND_BehaviorTree.Editor.Tests
{
    // TEMPORARY diagnostic: shows how EditorJsonUtility writes object references in this Unity version.
    public class ZzJsonProbeTests
    {
        [Test]
        public void Probe()
        {
            var root = ScriptableObject.CreateInstance<RootNode>();
            var wait = ScriptableObject.CreateInstance<WaitNode>();
            root.AddChild(wait);
            string json = EditorJsonUtility.ToJson(root);
#pragma warning disable CS0618
            string ids = $"waitInstanceId={wait.GetInstanceID()}";
#pragma warning restore CS0618
            var copy = ScriptableObject.CreateInstance<RootNode>();
            EditorJsonUtility.FromJsonOverwrite(json, copy);
            bool roundTrip = copy.child == wait;
            Object.DestroyImmediate(root); Object.DestroyImmediate(wait); Object.DestroyImmediate(copy);
            Assert.Fail($"PROBE {ids} roundTrip={roundTrip} json={json}");
        }
    }
}
