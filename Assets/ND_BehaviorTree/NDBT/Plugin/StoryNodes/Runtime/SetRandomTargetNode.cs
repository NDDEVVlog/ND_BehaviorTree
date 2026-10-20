using UnityEngine;

namespace ND_BehaviorTree.Story
{
    [StorySentence("Set random target {target} from tag {tag}")]
    [NodeDescription("Picks a random GameObject with a tag (never Self) and stores it in a GameObject key.",
        "Fails (logged once) if no other GameObject has the tag. The tag must exist in Project Settings > Tags and Layers.")]
    [NodeInfo("Set Random Target", "Story/Set Random Target", true, false, iconPath: "Assets/ND_BehaviorTree/NDBT/Icons/RandomBox.png")]
    public class SetRandomTargetNode : StoryActionNode
    {
        [BlackboardKeyType(typeof(GameObject))] public Key targetKey;
        public string tag = "Untagged";
        [BlackboardKeyType(typeof(string))] public Key tagKey;

        [System.NonSerialized] private bool m_NoneLogged;

        protected override Status OnProcess()
        {
            if (targetKey == null || blackboard == null)
            {
                LogKeyError("bind 'target' to a GameObject key.");
                return Status.Failure;
            }

            string wantedTag = Read(tagKey, tag);
            GameObject[] candidates;
            try { candidates = GameObject.FindGameObjectsWithTag(wantedTag); } // once per run, not per frame
            catch (UnityException)
            {
                LogKeyError($"tag '{wantedTag}' is not defined in Tags and Layers.");
                return Status.Failure;
            }

            var self = Self;
            int count = 0;
            for (int i = 0; i < candidates.Length; i++) if (candidates[i] != self) candidates[count++] = candidates[i];
            if (count == 0)
            {
                if (!m_NoneLogged) { m_NoneLogged = true; Debug.LogError($"[SetRandomTargetNode] '{name}': no GameObject tagged '{wantedTag}' (other than Self). (Logged once.)", this); }
                return Status.Failure;
            }

            return blackboard.SetValue(targetKey.keyName, candidates[Random.Range(0, count)]) ? Status.Success : Status.Failure;
        }
    }
}
