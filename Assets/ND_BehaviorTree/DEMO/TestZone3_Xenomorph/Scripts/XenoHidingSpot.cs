using System.Collections.Generic;
using UnityEngine;

namespace ND_BehaviorTree.Demo.Xeno
{
    /// <summary>
    /// A locker the player can hide in. If the alien saw the player get in (<see cref="SeenByAlien"/>), it will check
    /// this locker for certain; otherwise it only checks lockers by chance (a chance that grows as the player hides more).
    /// </summary>
    public class XenoHidingSpot : MonoBehaviour
    {
        private static readonly List<XenoHidingSpot> s_All = new List<XenoHidingSpot>();
        public static IReadOnlyList<XenoHidingSpot> All => s_All;

        [Tooltip("Where the hidden player is placed (inside the locker).")]
        [SerializeField] private Transform hidePoint;
        [Tooltip("Where the player stands to get in, and where the alien stands to inspect. Must be on the NavMesh.")]
        [SerializeField] private Transform standPoint;
        [Tooltip("Tinted while occupied (optional).")]
        [SerializeField] private Renderer doorRenderer;

        public Transform HidePoint => hidePoint;
        public Vector3 StandPosition => standPoint.position;
        public XenoPlayer Occupant { get; private set; }
        public bool SeenByAlien { get; private set; }
        public float LastCheckedTime { get; set; } = float.NegativeInfinity;

        private static readonly int s_ColorId = Shader.PropertyToID("_Color");
        private MaterialPropertyBlock m_Block;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_All.Clear();

        private void OnEnable() => s_All.Add(this);
        private void OnDisable() => s_All.Remove(this);

        public void Enter(XenoPlayer player)
        {
            Occupant = player;
            SetDoorColor(new Color(0.9f, 0.75f, 0.2f));
        }

        public void Exit()
        {
            Occupant = null;
            SeenByAlien = false;
            SetDoorColor(new Color(0.2f, 0.3f, 0.45f));
        }

        /// <summary>The alien saw the player get in.</summary>
        public void MarkSeen() => SeenByAlien = true;

        private void SetDoorColor(Color color)
        {
            if (doorRenderer == null) return;
            m_Block ??= new MaterialPropertyBlock();
            m_Block.SetColor(s_ColorId, color);
            doorRenderer.SetPropertyBlock(m_Block);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying) return;
            if (hidePoint == null) Debug.LogWarning($"[XenoHidingSpot] '{name}': hidePoint is not assigned.", this);
            if (standPoint == null) Debug.LogWarning($"[XenoHidingSpot] '{name}': standPoint is not assigned.", this);
        }
#endif

        private void OnDrawGizmos()
        {
            if (standPoint == null) return;
            Gizmos.color = SeenByAlien ? Color.red : new Color(0.4f, 0.7f, 1f);
            Gizmos.DrawWireSphere(standPoint.position, 0.3f);
        }
    }
}
