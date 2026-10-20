using UnityEngine;

namespace ND_BehaviorTree.Story
{
    [StorySentence("Wait for {seconds} seconds")]
    [NodeDescription("Waits a number of seconds (typed or from a float key), then succeeds.", "Running until the time has passed since the node started. The progress bar shows the elapsed time.")]
    [NodeInfo("Wait For", "Story/Wait For", true, false, iconPath: "Assets/ND_BehaviorTree/NDBT/Icons/Wait.png")]
    public class WaitForNode : StoryActionNode
    {
        public float seconds = 1f;
        [BlackboardKeyType(typeof(float))] public Key secondsKey;

        [NodeProgressBar(nameof(m_Elapsed), nameof(m_Duration))]
        [System.NonSerialized] private float m_Elapsed;
        [System.NonSerialized] private float m_Duration;

        protected override void OnEnter()
        {
            m_Elapsed = 0f;
            m_Duration = Read(secondsKey, seconds);
        }

        protected override Status OnProcess()
        {
            m_Elapsed += Time.deltaTime;
            return m_Elapsed < m_Duration ? Status.Running : Status.Success;
        }
    }
}
