using UnityEngine;

namespace ND.Dialogue
{
    /// <summary>
    /// Sample-only: keeps the camera at a fixed offset from the player. It stands still while a conversation runs, so
    /// sequence camera cuts (<c>Camera(target)</c>) hold until the conversation ends, then it glides back.
    /// </summary>
    [AddComponentMenu("ND Dialogue/Samples/Dialogue Sample Follow Camera")]
    public class DialogueSampleFollowCamera : MonoBehaviour
    {
        [SerializeField] private Transform m_Target;
        [SerializeField] private DialogueManager m_Manager;
        [SerializeField] private Vector3 m_Offset = new Vector3(0f, 9f, -8f);
        [SerializeField] private float m_Smoothing = 6f;

        private Quaternion m_Rotation;

        private void Awake()
        {
            m_Rotation = transform.rotation;
            if (m_Target == null) Debug.LogError($"[DialogueSampleFollowCamera] '{name}': assign the target to follow.", this);
        }

        private void LateUpdate()
        {
            if (m_Target == null || (m_Manager != null && m_Manager.IsTalking)) return;
            float t = 1f - Mathf.Exp(-m_Smoothing * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, m_Target.position + m_Offset, t);
            transform.rotation = Quaternion.Slerp(transform.rotation, m_Rotation, t);
        }
    }
}
