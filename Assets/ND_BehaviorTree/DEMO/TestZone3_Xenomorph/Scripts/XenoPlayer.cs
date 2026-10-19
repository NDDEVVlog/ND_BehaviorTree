using UnityEngine;

namespace ND_BehaviorTree.Demo.Xeno
{
    /// <summary>
    /// Minimal top-down survivor for the test zone (legacy Input Manager).
    /// WASD move · Shift run (loud) · C crouch (quiet, harder to see) · E hide in / leave a locker ·
    /// Q throw a noisemaker · hold F flamethrower (repels the alien into the vents).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class XenoPlayer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CharacterController controller;
        [SerializeField] private Renderer[] renderers;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private XenoDirector director;
        [Tooltip("The alien's runner; the flamethrower sends it the 'Burned' event.")]
        [SerializeField] private XenoRunner alien;
        [SerializeField] private GameObject flameVisual;

        [Header("Movement")]
        [SerializeField] private float walkSpeed = 3f;
        [SerializeField] private float runSpeed = 6f;
        [SerializeField] private float crouchSpeed = 1.5f;

        [Header("Noise (radius in metres; 0 = silent)")]
        [SerializeField] private float walkNoise = 4f;
        [SerializeField] private float runNoise = 13f;
        [SerializeField] private float crouchNoise = 0f;
        [SerializeField] private float stepInterval = 0.45f;

        [Header("Tools")]
        [SerializeField] private float hideReach = 1.6f;
        [SerializeField] private int noisemakers = 3;
        [SerializeField] private float noisemakerDistance = 9f;
        [SerializeField] private float noisemakerRadius = 25f;
        [SerializeField] private float flameRange = 5f;
        [SerializeField] private float flameHalfAngle = 25f;
        [SerializeField] private float flameFuel = 100f;
        [SerializeField] private float flameCostPerSecond = 30f;
        [SerializeField] private float flameNoise = 15f;
        [SerializeField] private float flameHitCooldown = 2f;
        [SerializeField] private float respawnDelay = 3f;

        public bool IsDead { get; private set; }
        public bool IsHidden => CurrentSpot != null;
        public bool IsCrouching { get; private set; }
        public bool IsRunning { get; private set; }
        public XenoHidingSpot CurrentSpot { get; private set; }
        public Vector3 Velocity { get; private set; }
        public float Fuel => m_Fuel;
        public int Noisemakers => m_Noisemakers;

        /// <summary>How visible the player is to the alien's eyes (0 = invisible). Multiplies the detection rate.</summary>
        public float Visibility => IsDead || IsHidden ? 0f : IsCrouching ? 0.45f : IsRunning ? 1.4f : 1f;

        /// <summary>Chest height (the pivot is the capsule center): what the alien's sight ray aims at.</summary>
        public Vector3 SightTarget => transform.position + Vector3.up * (IsCrouching ? -0.1f : 0.3f);

        private float m_StepTimer;
        private float m_Fuel;
        private int m_Noisemakers;
        private float m_RespawnAt;
        private float m_NextFlameHit;
        private float m_NextFlameNoise;

        private void Awake()
        {
            m_Fuel = flameFuel;
            m_Noisemakers = noisemakers;
        }

        private void Update()
        {
            if (IsDead)
            {
                if (Time.time >= m_RespawnAt) Respawn();
                return;
            }

            if (Input.GetKeyDown(KeyCode.E))
            {
                if (IsHidden) LeaveSpot();
                else TryHide();
            }
            if (IsHidden) return;

            Move();
            if (Input.GetKeyDown(KeyCode.Q)) ThrowNoisemaker();
            UpdateFlamethrower();
        }

        private void Move()
        {
            IsCrouching = Input.GetKey(KeyCode.C);
            IsRunning = !IsCrouching && Input.GetKey(KeyCode.LeftShift);

            var input = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
            if (input.sqrMagnitude > 1f) input.Normalize();
            float speed = IsCrouching ? crouchSpeed : IsRunning ? runSpeed : walkSpeed;
            Velocity = input * speed;
            controller.SimpleMove(Velocity);

            if (input.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(input), 15f * Time.deltaTime);
                m_StepTimer -= Time.deltaTime * (IsRunning ? 1.6f : 1f);
                if (m_StepTimer <= 0f)
                {
                    m_StepTimer = stepInterval;
                    XenoNoise.Emit(transform.position, IsCrouching ? crouchNoise : IsRunning ? runNoise : walkNoise, XenoNoiseSource.Footstep);
                }
            }
            else
            {
                IsRunning = false;
            }

            var scale = transform.localScale;
            scale.y = IsCrouching ? 0.6f : 1f;
            transform.localScale = scale;
        }

        private void TryHide()
        {
            var spots = XenoHidingSpot.All;
            for (int i = 0; i < spots.Count; i++)
            {
                var spot = spots[i];
                if (spot.Occupant != null) continue;
                if ((spot.StandPosition - transform.position).sqrMagnitude > hideReach * hideReach) continue;

                CurrentSpot = spot;
                spot.Enter(this);
                controller.enabled = false;
                transform.position = spot.HidePoint.position;
                SetVisible(false);
                Velocity = Vector3.zero;
                director.OnPlayerHid();
                return;
            }
        }

        private void LeaveSpot()
        {
            var spot = CurrentSpot;
            CurrentSpot = null;
            spot.Exit();
            transform.position = spot.StandPosition + Vector3.up * (controller.height * 0.5f + 0.05f); // pivot = capsule center
            controller.enabled = true;
            SetVisible(true);
        }

        private void ThrowNoisemaker()
        {
            if (m_Noisemakers <= 0) return;
            m_Noisemakers--;
            Vector3 target = transform.position + transform.forward * noisemakerDistance;
            if (Physics.Raycast(transform.position + Vector3.up, transform.forward, out var hit, noisemakerDistance, ~0, QueryTriggerInteraction.Ignore))
                target = hit.point - transform.forward * 0.5f;
            target.y = 0f;
            XenoNoise.Emit(target, noisemakerRadius, XenoNoiseSource.Noisemaker);
        }

        private void UpdateFlamethrower()
        {
            bool firing = Input.GetKey(KeyCode.F) && m_Fuel > 0f;
            if (flameVisual != null && flameVisual.activeSelf != firing) flameVisual.SetActive(firing);
            if (!firing) return;

            m_Fuel = Mathf.Max(0f, m_Fuel - flameCostPerSecond * Time.deltaTime);
            if (Time.time >= m_NextFlameNoise)
            {
                m_NextFlameNoise = Time.time + 0.5f;
                XenoNoise.Emit(transform.position, flameNoise, XenoNoiseSource.Flamethrower);
            }

            if (Time.time < m_NextFlameHit || alien == null) return;
            var body = alien.Body;
            if (body.InVent) return;
            Vector3 toAlien = body.transform.position - transform.position;
            toAlien.y = 0f;
            if (toAlien.magnitude > flameRange || Vector3.Angle(transform.forward, toAlien) > flameHalfAngle) return;

            m_NextFlameHit = Time.time + flameHitCooldown;
            director.RegisterFlameHit();
            alien.SendEvent(XenoRunner.BurnedEvent);
        }

        /// <summary>Called by the alien (attack, or dragging the player out of a locker).</summary>
        public void Kill()
        {
            if (IsDead) return;
            if (IsHidden) LeaveSpot();
            IsDead = true;
            Velocity = Vector3.zero;
            controller.enabled = false;
            SetVisible(false);
            if (flameVisual != null) flameVisual.SetActive(false);
            m_RespawnAt = Time.time + respawnDelay;
            director.OnPlayerKilled();
        }

        private void Respawn()
        {
            IsDead = false;
            transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
            controller.enabled = true;
            SetVisible(true);
            m_Fuel = flameFuel;
            m_Noisemakers = noisemakers;
        }

        private void SetVisible(bool visible)
        {
            for (int i = 0; i < renderers.Length; i++) renderers[i].enabled = visible;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying) return;
            if (controller == null) Debug.LogWarning($"[XenoPlayer] '{name}': controller is not assigned.", this);
            if (spawnPoint == null) Debug.LogWarning($"[XenoPlayer] '{name}': spawnPoint is not assigned.", this);
            if (director == null) Debug.LogWarning($"[XenoPlayer] '{name}': director is not assigned.", this);
            if (alien == null) Debug.LogWarning($"[XenoPlayer] '{name}': alien is not assigned (the flamethrower will not repel it).", this);
            if (renderers == null || renderers.Length == 0) Debug.LogWarning($"[XenoPlayer] '{name}': renderers is empty (hiding/death will not hide the body).", this);
        }
#endif
    }
}
