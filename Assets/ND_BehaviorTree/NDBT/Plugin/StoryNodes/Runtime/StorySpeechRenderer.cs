using UnityEngine;

namespace ND_BehaviorTree.Story
{
    /// <summary>Draws <see cref="StorySpeech"/> bubbles above their speakers (IMGUI; one per scene is enough).</summary>
    public class StorySpeechRenderer : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private float heightOffset = 2.2f;
        [SerializeField] private int fontSize = 14;

        private GUIStyle m_Style;

        private void OnGUI()
        {
            StorySpeech.Prune();
            var bubbles = StorySpeech.Active;
            if (bubbles.Count == 0) return;
            if (targetCamera == null)
            {
                Debug.LogError($"[StorySpeechRenderer] '{name}': targetCamera is not assigned.", this);
                enabled = false;
                return;
            }

            m_Style ??= new GUIStyle(GUI.skin.box) { fontSize = fontSize, wordWrap = true, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            for (int i = 0; i < bubbles.Count; i++)
            {
                var bubble = bubbles[i];
                Vector3 screen = targetCamera.WorldToScreenPoint(bubble.Speaker.transform.position + Vector3.up * heightOffset);
                if (screen.z < 0f) continue;
                var content = new GUIContent(bubble.Text);
                Vector2 size = m_Style.CalcSize(content);
                size.x = Mathf.Min(size.x + 12f, 260f);
                size.y = m_Style.CalcHeight(content, size.x) + 6f;
                GUI.Box(new Rect(screen.x - size.x / 2f, Screen.height - screen.y - size.y, size.x, size.y), content, m_Style);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying && targetCamera == null)
                Debug.LogWarning($"[StorySpeechRenderer] '{name}': assign targetCamera.", this);
        }
#endif
    }
}
