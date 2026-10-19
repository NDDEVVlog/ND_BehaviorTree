using UnityEngine;
using UnityEngine.AI;

namespace ND_BehaviorTree.Demo.Xeno
{
    /// <summary>
    /// The "macro" brain from Alien: Isolation. It always knows where the player is, but never steers the alien
    /// directly. It only:
    ///   * gives a fuzzy <see cref="Hint"/> near the player (tighter the longer the alien goes without contact),
    ///   * fills a <see cref="Menace"/> gauge while the alien is close, and asks it to go backstage (into the vents)
    ///     when the gauge is full, so the player gets breathing room,
    ///   * tracks how the player plays ("learning"): the more they hide, the more lockers get checked; the more they
    ///     use the flamethrower, the shorter the alien stays away.
    /// The alien's behavior tree reads these through XenoDirectorService and decides on its own, using only its senses.
    /// </summary>
    public class XenoDirector : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private XenoPlayer player;
        [SerializeField] private XenoBody alien;

        [Header("Menace gauge")]
        [SerializeField] private float menaceRadius = 10f;
        [SerializeField] private float menaceRise = 0.08f;
        [SerializeField] private float menaceDecay = 0.04f;

        [Header("Hint")]
        [SerializeField] private float hintInterval = 12f;
        [SerializeField] private float hintErrorMax = 12f;
        [SerializeField] private float hintErrorMin = 3f;
        [Tooltip("Seconds without contact for the hint error to shrink from max to min.")]
        [SerializeField] private float hintTightenTime = 60f;

        [Header("Learning")]
        [SerializeField] private float baseHideCheckChance = 0.15f;
        [SerializeField] private float hideCheckPerHide = 0.12f;
        [SerializeField] private float baseBackstageTime = 12f;
        [SerializeField] private float backstagePerFlameHit = 2f;
        [SerializeField] private float minBackstageTime = 4f;

        [Header("Debug")]
        [SerializeField] private bool showHud = true;

        public float Menace { get; private set; }
        public bool RetreatRequested { get; private set; }
        public int HideCount { get; private set; }
        public int FlameHits { get; private set; }
        public float HideCheckChance => Mathf.Min(0.9f, baseHideCheckChance + hideCheckPerHide * HideCount);
        public float BackstageTime => Mathf.Max(minBackstageTime, baseBackstageTime - backstagePerFlameHit * FlameHits);

        private Vector3 m_Hint;
        private bool m_HasHint;
        private float m_NextHintTime;
        private float m_LastContactTime;

        /// <summary>A navigable point near the player, with an error that shrinks the longer the alien has no contact.</summary>
        public Vector3 Hint
        {
            get
            {
                if (!m_HasHint) PickHint();
                return m_Hint;
            }
        }

        private void Update()
        {
            if (alien.PlayerVisible) m_LastContactTime = Time.time;

            float distance = Vector3.Distance(alien.transform.position, player.transform.position);
            bool near = !alien.InVent && !player.IsDead && distance < menaceRadius;
            Menace = Mathf.Clamp01(Menace + (near ? menaceRise : -menaceDecay) * Time.deltaTime);

            // Never pull the alien out of a chase: only ask for a retreat while it has no sight of the player.
            if (!RetreatRequested && Menace >= 1f && !alien.PlayerVisible) RetreatRequested = true;

            if (Time.time >= m_NextHintTime) PickHint();
        }

        private void PickHint()
        {
            m_NextHintTime = Time.time + hintInterval;
            float t = Mathf.Clamp01((Time.time - m_LastContactTime) / hintTightenTime);
            float error = Mathf.Lerp(hintErrorMax, hintErrorMin, t);
            Vector2 offset = Random.insideUnitCircle * error;
            Vector3 candidate = player.transform.position + new Vector3(offset.x, 0f, offset.y);
            if (NavMesh.SamplePosition(candidate, out var hit, error + 2f, NavMesh.AllAreas))
            {
                m_Hint = hit.position;
                m_HasHint = true;
            }
            else if (!m_HasHint)
            {
                Debug.LogWarning($"[XenoDirector] No NavMesh near the player at {player.transform.position}; using the player position as the hint.", this);
                m_Hint = player.transform.position;
                m_HasHint = true;
            }
        }

        /// <summary>The alien came back out of the vents: reset the gauge.</summary>
        public void NotifyAlienReturned()
        {
            RetreatRequested = false;
            Menace = 0f;
        }

        public void OnPlayerHid() => HideCount++;

        public void RegisterFlameHit() => FlameHits++;

        public void OnPlayerKilled()
        {
            // Give the respawned player room: send the alien backstage.
            Menace = 1f;
            RetreatRequested = true;
            m_LastContactTime = Time.time;
        }

        // ---------------------------------------------------------------- HUD (strings are constant: no per-frame GC)
        private static Texture2D s_White;

        private void OnGUI()
        {
            if (!showHud) return;
            if (s_White == null) s_White = Texture2D.whiteTexture;

            GUI.Box(new Rect(10, 10, 300, 168), GUIContent.none);
            Bar(new Rect(20, 20, 280, 18), "Menace", Menace, new Color(0.8f, 0.3f, 0.9f));
            Bar(new Rect(20, 44, 280, 18), "Alien detection", alien.Detection, alien.PlayerVisible ? Color.red : new Color(1f, 0.6f, 0.1f));
            Bar(new Rect(20, 68, 280, 18), "Locker check chance (learned)", HideCheckChance, new Color(0.3f, 0.7f, 1f));
            Bar(new Rect(20, 92, 280, 18), "Flamethrower fuel", player.Fuel / 100f, new Color(1f, 0.45f, 0.1f));
            GUI.Label(new Rect(20, 114, 280, 20), alien.InVent ? "Alien: in the vents" : RetreatRequested ? "Alien: told to go backstage" : alien.PlayerVisible ? "Alien: HUNTING YOU" : "Alien: searching");
            GUI.Label(new Rect(20, 134, 280, 40), "WASD move · Shift run · C crouch · E locker\nQ noisemaker · hold F flamethrower");
        }

        private static void Bar(Rect rect, string label, float value, Color color)
        {
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(rect, s_White);
            GUI.color = color;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(value), rect.height), s_White);
            GUI.color = old;
            GUI.Label(new Rect(rect.x + 4, rect.y, rect.width, rect.height), label);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying) return;
            if (player == null) Debug.LogWarning($"[XenoDirector] '{name}': player is not assigned.", this);
            if (alien == null) Debug.LogWarning($"[XenoDirector] '{name}': alien is not assigned.", this);
        }
#endif

        private void OnDrawGizmos()
        {
            if (!m_HasHint) return;
            Gizmos.color = new Color(0.8f, 0.3f, 0.9f);
            Gizmos.DrawWireSphere(m_Hint, 1f);
        }
    }
}
