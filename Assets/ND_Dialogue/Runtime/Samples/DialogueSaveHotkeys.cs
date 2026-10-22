using UnityEngine;

namespace ND.Dialogue
{
    /// <summary>Sample-only: F5 saves the dialogue state to a slot, F9 loads it (legacy Input), with a short note on screen.</summary>
    [AddComponentMenu("ND Dialogue/Samples/Dialogue Save Hotkeys")]
    public class DialogueSaveHotkeys : MonoBehaviour
    {
        [SerializeField] private DialogueManager m_Manager;
        [SerializeField] private string m_Slot = "sample";
        [Tooltip("Optional: where 'Saved' / 'Loaded' is shown for a moment.")]
        [SerializeField] private UnityEngine.UI.Graphic m_Note;

        private float m_HideNoteAt;

        private void Update()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (m_Manager == null) return;
            if (Input.GetKeyDown(KeyCode.F5)) Note(m_Manager.Save(m_Slot) ? "Saved" : "Save failed (see Console)");
            else if (Input.GetKeyDown(KeyCode.F9)) Note(!m_Manager.HasSave(m_Slot) ? "No save yet (F5)" : m_Manager.Load(m_Slot) ? "Loaded" : "Load failed (see Console)");
#endif
            if (m_Note != null && m_Note.gameObject.activeSelf && Time.time > m_HideNoteAt) m_Note.gameObject.SetActive(false);
        }

        private void Note(string text)
        {
            if (m_Note == null) return;
            UIText.Set(m_Note, text);
            m_Note.gameObject.SetActive(true);
            m_HideNoteAt = Time.time + 2f;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying && m_Manager == null) Debug.LogWarning($"[DialogueSaveHotkeys] '{name}': assign the Dialogue Manager.", this);
        }
#endif
    }
}
