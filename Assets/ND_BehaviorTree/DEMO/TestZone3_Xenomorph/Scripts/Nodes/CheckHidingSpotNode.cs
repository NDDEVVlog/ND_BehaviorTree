using UnityEngine;
using UnityEngine.AI;

namespace ND_BehaviorTree.Demo.Xeno
{
    /// <summary>
    /// Goes to a locker near the center and inspects it. A locker the alien saw the player enter is always checked;
    /// otherwise one is checked only by chance (the learned chance from the director). If the player is inside,
    /// they are dragged out and killed. Always succeeds (nothing to check is not a failure), so it never blocks a search.
    /// </summary>
    [NodeDescription("Goes to a nearby locker and inspects it; drags the player out if they hide inside.", "A locker the alien saw the player enter is always checked; otherwise one is checked by chance (Chance key, e.g. the director's learned HideCheckChance).\nRecently checked lockers are skipped. Always succeeds, so it never blocks a search.")]
    [NodeInfo("Check Hiding Spot", "Action/Xeno/Check Hiding Spot", true, false, iconPath: "Assets/ND_BehaviorTree/NDBT/Icons/CheckCondition.png")]
    public class CheckHidingSpotNode : ActionNode
    {
        [BlackboardKeyType(typeof(XenoBody))] public Key bodyKey;
        [BlackboardKeyType(typeof(Vector3))] public Key centerKey;
        [Tooltip("0..1 chance to check an unmarked locker. Unlinked = use 'chance'.")]
        [BlackboardKeyType(typeof(float))] public Key chanceKey;

        [Range(0f, 1f)] public float chance = 0.3f;
        public float searchRadius = 10f;
        public float inspectDuration = 1.5f;
        [Tooltip("A locker checked less than this many seconds ago is skipped (unless it is marked).")]
        public float recheckCooldown = 20f;

        [NodeProgressBar(nameof(m_InspectElapsed), nameof(inspectDuration))]
        [System.NonSerialized] private float m_InspectElapsed;

        [System.NonSerialized] private XenoBody m_Body;
        [System.NonSerialized] private XenoHidingSpot m_Spot;
        [System.NonSerialized] private bool m_Inspecting;

        protected override void OnEnter()
        {
            m_Body = KeyResolver.Resolve<XenoBody>(this, bodyKey)?.GetValue();
            var center = KeyResolver.Resolve<Vector3>(this, centerKey);
            var chanceSource = chanceKey != null ? KeyResolver.Resolve<float>(this, chanceKey) : null;
            m_Spot = null;
            m_Inspecting = false;
            m_InspectElapsed = 0f;
            if (m_Body == null || center == null || !m_Body.Agent.isOnNavMesh) return;

            m_Spot = PickSpot(center.GetValue(), chanceSource != null ? chanceSource.GetValue() : chance);
            if (m_Spot == null) return;
            m_Body.Agent.stoppingDistance = 0.3f;
            m_Body.Agent.SetDestination(m_Spot.StandPosition);
        }

        private XenoHidingSpot PickSpot(Vector3 center, float checkChance)
        {
            XenoHidingSpot marked = null, candidate = null;
            float markedSqr = float.MaxValue, candidateSqr = float.MaxValue, radiusSqr = searchRadius * searchRadius;
            var spots = XenoHidingSpot.All;
            for (int i = 0; i < spots.Count; i++)
            {
                var spot = spots[i];
                float sqr = (spot.StandPosition - center).sqrMagnitude;
                if (sqr > radiusSqr) continue;
                if (spot.SeenByAlien && sqr < markedSqr) { marked = spot; markedSqr = sqr; }
                if (Time.time - spot.LastCheckedTime >= recheckCooldown && sqr < candidateSqr) { candidate = spot; candidateSqr = sqr; }
            }
            if (marked != null) return marked;
            return candidate != null && Random.value < checkChance ? candidate : null;
        }

        protected override Status OnProcess()
        {
            if (m_Spot == null) return Status.Success;
            var agent = m_Body.Agent;

            if (!m_Inspecting)
            {
                if (agent.pathPending) return Status.Running;
                if (agent.pathStatus == NavMeshPathStatus.PathInvalid) return Status.Success;
                if (agent.remainingDistance > agent.stoppingDistance) return Status.Running;
                m_Inspecting = true;
                agent.ResetPath();
            }

            // Face the locker while "listening" at the door.
            Vector3 look = m_Spot.HidePoint.position - m_Body.transform.position;
            look.y = 0f;
            if (look.sqrMagnitude > 0.001f)
                m_Body.transform.rotation = Quaternion.RotateTowards(m_Body.transform.rotation, Quaternion.LookRotation(look), 360f * Time.deltaTime);

            m_InspectElapsed += Time.deltaTime;
            if (m_InspectElapsed < inspectDuration) return Status.Running;

            m_Spot.LastCheckedTime = Time.time;
            var occupant = m_Spot.Occupant;
            if (occupant != null) occupant.Kill(); // dragged out of the locker
            return Status.Success;
        }

        protected override void OnExit()
        {
            if (m_Body != null && m_Body.Agent.isOnNavMesh && m_Body.Agent.hasPath) m_Body.Agent.ResetPath();
        }
    }
}
