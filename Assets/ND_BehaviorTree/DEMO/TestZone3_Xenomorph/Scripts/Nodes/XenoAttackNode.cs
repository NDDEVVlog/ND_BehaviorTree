using UnityEngine;

namespace ND_BehaviorTree.Demo.Xeno
{
    /// <summary>Stops, faces the player and strikes after a short wind-up. Success = kill; Failure = the player got away.</summary>
    [NodeDescription("Faces the player and strikes after a short wind-up; kills them if still in reach.", "Success = kill. Failure = the player got out of reach, hid or is already dead.")]
    [NodeInfo("Xeno Attack", "Action/Xeno/Attack", true, false, iconPath: "Assets/ND_BehaviorTree/NDBT/Icons/CheckCondition.png")]
    public class XenoAttackNode : ActionNode
    {
        [BlackboardKeyType(typeof(XenoBody))] public Key bodyKey;

        public float windUp = 0.45f;
        [Tooltip("Extra reach on top of XenoBody.attackRange when the strike lands.")]
        public float reachTolerance = 0.6f;

        [NodeProgressBar(nameof(m_Elapsed), nameof(windUp))]
        [System.NonSerialized] private float m_Elapsed;
        [System.NonSerialized] private XenoBody m_Body;

        protected override void OnEnter()
        {
            m_Elapsed = 0f;
            m_Body = KeyResolver.Resolve<XenoBody>(this, bodyKey)?.GetValue();
            if (m_Body != null && m_Body.Agent.isOnNavMesh) m_Body.Agent.ResetPath();
        }

        protected override Status OnProcess()
        {
            if (m_Body == null) return Status.Failure;
            var player = m_Body.Player;
            if (player.IsDead || player.IsHidden) return Status.Failure;

            Vector3 toPlayer = player.transform.position - m_Body.transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.001f)
                m_Body.transform.rotation = Quaternion.RotateTowards(m_Body.transform.rotation, Quaternion.LookRotation(toPlayer), 720f * Time.deltaTime);

            m_Elapsed += Time.deltaTime;
            if (m_Elapsed < windUp) return Status.Running;

            if (toPlayer.magnitude > m_Body.attackRange + reachTolerance) return Status.Failure;
            player.Kill();
            return Status.Success;
        }
    }
}
