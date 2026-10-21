using UnityEngine;

namespace ND.Dialogue
{
    /// <summary>
    /// Sample-only top-down movement (WASD / arrow keys on the XZ plane) that stands still while a conversation runs.
    /// Replace it with your own controller; listen to <see cref="DialogueManager.ConversationStarted"/> the same way.
    /// </summary>
    [AddComponentMenu("ND Dialogue/Samples/Dialogue Sample Mover")]
    public class DialogueSampleMover : MonoBehaviour
    {
        [SerializeField] private DialogueManager m_Manager;
        [SerializeField] private float m_Speed = 4f;

        private void Update()
        {
            if (m_Manager != null && m_Manager.IsTalking) return;
#if ENABLE_LEGACY_INPUT_MANAGER
            var input = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
            if (input.sqrMagnitude > 1f) input.Normalize();
            transform.position += input * (m_Speed * Time.deltaTime);
            if (input.sqrMagnitude > 0.01f) transform.forward = input;
#endif
        }
    }
}
