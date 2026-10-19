using UnityEngine;

namespace ND_BehaviorTree.Demo.Xeno
{
    /// <summary>Runs the Xenomorph tree and injects the scene references the blackboard cannot store in the asset.</summary>
    public class XenoRunner : BehaviorTreeRunner
    {
        public const string AgentKey = "Agent";
        public const string BodyKey = "Body";
        public const string PlayerKey = "Player";
        public const string BurnedEvent = "Burned";

        [SerializeField] private XenoBody body;

        public XenoBody Body => body;

        public override void Init()
        {
            var blackboard = RuntimeTree.blackboard;
            if (blackboard == null)
            {
                Debug.LogError($"[XenoRunner] '{name}': the tree '{treeAsset.name}' has no blackboard.", this);
                return;
            }
            blackboard.SetValue(AgentKey, body.Agent);
            blackboard.SetValue(BodyKey, body);
            blackboard.SetValue(PlayerKey, body.Player.transform);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying) return;
            if (body == null) Debug.LogWarning($"[XenoRunner] '{name}': body is not assigned.", this);
            if (treeAsset == null) Debug.LogWarning($"[XenoRunner] '{name}': treeAsset is not assigned.", this);
        }
#endif
    }
}
