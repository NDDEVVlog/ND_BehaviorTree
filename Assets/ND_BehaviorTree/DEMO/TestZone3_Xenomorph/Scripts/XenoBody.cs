using UnityEngine;
using UnityEngine.AI;

namespace ND_BehaviorTree.Demo.Xeno
{
    /// <summary>
    /// The alien's body: capabilities (agent, eyes, vents) and sense tuning. The behavior tree decides; this component
    /// only executes and exposes what the senses produced (mirrored from the blackboard for gizmos and the HUD).
    /// </summary>
    public class XenoBody : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private NavMeshAgent agent;
        [SerializeField] private Transform eye;
        [SerializeField] private Collider bodyCollider;
        [SerializeField] private Renderer[] renderers;
        [SerializeField] private XenoPlayer player;
        [SerializeField] private XenoDirector director;

        [Header("Sight")]
        public float sightRange = 18f;
        public float fieldOfView = 110f;
        [Tooltip("Inside this radius the alien senses the player whatever it faces (unless the player is hidden).")]
        public float proximityRadius = 2.5f;
        [Tooltip("Detection gained per second at full visibility (the player is 'spotted' at 1).")]
        public float detectionRate = 1.3f;
        public float detectionDecay = 0.35f;
        [Tooltip("A glimpse above this level makes the alien come and look.")]
        [Range(0f, 1f)] public float suspicionThreshold = 0.4f;
        [Tooltip("Seconds the alien keeps 'seeing' the player after line of sight breaks.")]
        public float loseSightGrace = 0.75f;
        public LayerMask sightBlockers = ~0;

        [Header("Hearing / Attack")]
        public float hearingMultiplier = 1f;
        public float attackRange = 1.8f;

        [Header("Start")]
        [Tooltip("Start hidden in the vents; the Init branch drops the alien out.")]
        [SerializeField] private bool startInVent = true;

        public NavMeshAgent Agent => agent;
        public XenoPlayer Player => player;
        public XenoDirector Director => director;
        public Vector3 EyePosition => eye.position;
        public Vector3 Forward => eye.forward;
        public bool InVent { get; private set; }

        // Written by XenoSenseService (debug / HUD / director); the blackboard holds the values the tree uses.
        public float Detection { get; set; }
        public bool PlayerVisible { get; set; }
        public Vector3 LastKnownPosition { get; set; }
        public Vector3 LastNoisePosition { get; private set; }
        public float LastNoiseTime { get; private set; } = float.NegativeInfinity;

        private static readonly int s_ColorId = Shader.PropertyToID("_Color");
        private MaterialPropertyBlock m_Block;
        private int m_TintLevel = -1;

        private void Awake()
        {
            if (startInVent) SetHidden(true);
        }

        private void Update()
        {
            // Tint: dark = unaware, orange = suspicious, red = spotted. Only touches the renderers when the level changes.
            int level = PlayerVisible ? 2 : Detection >= suspicionThreshold ? 1 : 0;
            if (level == m_TintLevel) return;
            m_TintLevel = level;
            m_Block ??= new MaterialPropertyBlock();
            m_Block.SetColor(s_ColorId, level == 2 ? new Color(0.85f, 0.1f, 0.1f) : level == 1 ? new Color(0.9f, 0.5f, 0.1f) : new Color(0.12f, 0.12f, 0.15f));
            for (int i = 0; i < renderers.Length; i++) renderers[i].SetPropertyBlock(m_Block);
        }

        public void OnHeardNoise(Vector3 position)
        {
            LastNoisePosition = position;
            LastNoiseTime = Time.time;
        }

        /// <summary>Crawls into <paramref name="vent"/>: invisible, intangible, stopped.</summary>
        public void EnterVent(XenoVent vent)
        {
            if (agent.isOnNavMesh) agent.ResetPath();
            agent.Warp(vent.Position);
            SetHidden(true);
        }

        /// <summary>Drops out of <paramref name="vent"/>.</summary>
        public void EmergeAt(XenoVent vent)
        {
            agent.Warp(vent.Position);
            SetHidden(false);
        }

        private void SetHidden(bool hidden)
        {
            InVent = hidden;
            for (int i = 0; i < renderers.Length; i++) renderers[i].enabled = !hidden;
            bodyCollider.enabled = !hidden;
            if (agent.isOnNavMesh) agent.isStopped = hidden;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying) return;
            if (agent == null) Debug.LogWarning($"[XenoBody] '{name}': agent is not assigned.", this);
            if (eye == null) Debug.LogWarning($"[XenoBody] '{name}': eye is not assigned.", this);
            if (bodyCollider == null) Debug.LogWarning($"[XenoBody] '{name}': bodyCollider is not assigned.", this);
            if (player == null) Debug.LogWarning($"[XenoBody] '{name}': player is not assigned.", this);
            if (director == null) Debug.LogWarning($"[XenoBody] '{name}': director is not assigned.", this);
            if (renderers == null || renderers.Length == 0) Debug.LogWarning($"[XenoBody] '{name}': renderers is empty (vents will not hide the body).", this);
        }
#endif

        private void OnDrawGizmos()
        {
            if (eye == null) return;
            Vector3 origin = eye.position;
            Gizmos.color = PlayerVisible ? Color.red : new Color(1f, 0.8f, 0.2f, 0.6f);
            Quaternion left = Quaternion.Euler(0f, -fieldOfView * 0.5f, 0f), right = Quaternion.Euler(0f, fieldOfView * 0.5f, 0f);
            Gizmos.DrawLine(origin, origin + left * eye.forward * sightRange);
            Gizmos.DrawLine(origin, origin + right * eye.forward * sightRange);
            Gizmos.DrawWireSphere(transform.position, proximityRadius);

            if (!Application.isPlaying) return;
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(LastKnownPosition, 0.5f);
            if (Time.time - LastNoiseTime < 3f)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(LastNoisePosition, 0.7f);
            }
        }
    }
}
