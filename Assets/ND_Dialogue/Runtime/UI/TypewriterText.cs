using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace ND.Dialogue
{
    /// <summary>
    /// Types a line into a TextMeshPro text: it builds the line's <see cref="LineTimeline"/> once, then each frame asks
    /// it what to show (<see cref="LineTimeline.Compose"/>) and draws that — text only when it changed, then the glyph
    /// motions (wave, shake) of the styles. It decides nothing itself: the markup in the line does (pauses, speed,
    /// styles, fixes, events). An optional blip plays as characters appear.
    /// </summary>
    [AddComponentMenu("ND Dialogue/Typewriter Text (TextMeshPro)")]
    public class TypewriterText : MonoBehaviour
    {
        private const string LogTag = "[TypewriterText]";
        private const float Epsilon = 0.0001f;

        [SerializeField] private TMP_Text m_Text;
        [Tooltip("Characters per second (0 = the whole line at once). <speed> tags change it inside a line.")]
        [SerializeField, Min(0f)] private float m_CharactersPerSecond = LineTimeline.DefaultCharactersPerSecond;
        [Header("Motion")]
        [SerializeField] private float m_WaveHeight = 4f;
        [SerializeField] private float m_WaveSpeed = 6f;
        [SerializeField] private float m_ShakeAmount = 1.5f;
        [Header("Blip (optional)")]
        [SerializeField] private AudioSource m_BlipSource;
        [SerializeField] private AudioClip m_Blip;
        [Tooltip("A blip every N characters.")]
        [SerializeField, Min(1)] private int m_BlipEvery = 2;

        private LineTimeline m_Timeline;
        private readonly LineFrame m_Frame = new LineFrame();
        private readonly List<LineEvent> m_Reached = new List<LineEvent>();
        private float m_Time;
        private int m_LastVisible;
        // Glyph vertices at rest, per mesh (grown when needed, reused: no allocation per character).
        private Vector3[][] m_RestingVertices = Array.Empty<Vector3[]>();
        private bool m_HasResting;

        public bool IsPlaying { get; private set; }
        public LineTimeline Timeline => m_Timeline;
        public float CharactersPerSecond { get => m_CharactersPerSecond; set => m_CharactersPerSecond = Mathf.Max(0f, value); }
        public TMP_Text Text { get => m_Text; set => m_Text = value; }

        /// <summary>(event) Typing reached an &lt;event&gt; of the line.</summary>
        public event Action<LineEvent> EventReached;
        /// <summary>The line is fully shown (typed out or completed).</summary>
        public event Action Finished;

        /// <summary>Starts typing <paramref name="line"/>. <paramref name="fitSeconds"/> &gt; 0: the typing lasts that long (e.g. its voice clip).</summary>
        public void Play(string line, float fitSeconds = 0f)
        {
            if (m_Text == null)
            {
                Debug.LogError($"{LogTag} '{name}': {nameof(Play)} rejected: no TextMeshPro text assigned.", this);
                return;
            }
            m_Timeline = LineTimeline.Build(line, m_CharactersPerSecond, fitSeconds);
            m_Time = -Epsilon; // so events at 0 s count as reached by the first step
            m_LastVisible = 0;
            m_HasResting = false;
            m_Frame.Reset();
            IsPlaying = true;
            Advance(Epsilon);
        }

        /// <summary>Shows the rest of the line at once (the events still due are raised).</summary>
        public void Complete()
        {
            if (!IsPlaying) return;
            Advance(Mathf.Max(0f, m_Timeline.Duration - m_Time) + Epsilon);
        }

        public void Clear()
        {
            IsPlaying = false;
            m_Timeline = null;
            m_HasResting = false;
            m_Frame.Reset();
            if (m_Text != null) m_Text.text = string.Empty;
        }

        private void Update()
        {
            if (IsPlaying) Advance(Time.unscaledDeltaTime);
            else if (m_Frame.Effects.Count > 0 && m_Timeline != null) Animate();
        }

        /// <summary>Moves the line <paramref name="seconds"/> on (Update calls it; tests too).</summary>
        public void Advance(float seconds)
        {
            if (m_Timeline == null) return;
            float from = m_Time;
            m_Time += seconds;
            m_Timeline.Compose(m_Time, m_Frame);
            if (m_Frame.Changed)
            {
                m_Text.SetText(m_Frame.Builder); // no string: TMP copies the builder into its own buffer
                m_Text.ForceMeshUpdate();
                m_HasResting = m_Frame.Effects.Count > 0 && KeepResting();
            }
            if (m_Frame.Effects.Count > 0) Animate();
            if (m_Blip != null && m_BlipSource != null && m_Frame.VisibleCharacters / m_BlipEvery > m_LastVisible / m_BlipEvery) m_BlipSource.PlayOneShot(m_Blip);
            m_LastVisible = m_Frame.VisibleCharacters;

            m_Reached.Clear();
            m_Timeline.EventsBetween(from, m_Time, m_Reached);
            foreach (var e in m_Reached) EventReached?.Invoke(e);

            if (IsPlaying && m_Frame.Done)
            {
                IsPlaying = false;
                Finished?.Invoke();
            }
        }

        /// <summary>Copies the glyph vertices at rest into the reused buffers (they grow only when the text gets longer).</summary>
        private bool KeepResting()
        {
            var meshes = m_Text.textInfo.meshInfo;
            if (m_RestingVertices.Length < meshes.Length) Array.Resize(ref m_RestingVertices, meshes.Length);
            for (int m = 0; m < meshes.Length; m++)
            {
                var source = meshes[m].vertices;
                if (source == null) continue;
                if (m_RestingVertices[m] == null || m_RestingVertices[m].Length < source.Length) m_RestingVertices[m] = new Vector3[source.Length];
                Array.Copy(source, m_RestingVertices[m], source.Length);
            }
            return true;
        }

        /// <summary>Moves the glyphs of the effect ranges from their resting place (no allocation).</summary>
        private void Animate()
        {
            if (!m_HasResting || m_Text == null) return;
            var info = m_Text.textInfo;
            float now = Time.unscaledTime;
            foreach (var range in m_Frame.Effects)
            {
                for (int c = range.Start; c < range.Start + range.Length && c < info.characterCount; c++)
                {
                    var character = info.characterInfo[c];
                    if (!character.isVisible) continue;
                    int mesh = character.materialReferenceIndex, vertex = character.vertexIndex;
                    if (mesh >= m_RestingVertices.Length || m_RestingVertices[mesh] == null) continue;
                    var rest = m_RestingVertices[mesh];
                    var live = info.meshInfo[mesh].vertices;
                    if (vertex + 4 > rest.Length || vertex + 4 > live.Length) continue;
                    Vector3 offset = range.Effect == LineEffect.Wave
                        ? new Vector3(0f, Mathf.Sin(now * m_WaveSpeed + c * 0.45f) * m_WaveHeight, 0f)
                        : new Vector3((Mathf.PerlinNoise(c * 3.1f, now * 20f) - 0.5f) * 2f * m_ShakeAmount, (Mathf.PerlinNoise(now * 20f, c * 1.7f) - 0.5f) * 2f * m_ShakeAmount, 0f);
                    for (int v = 0; v < 4; v++) live[vertex + v] = rest[vertex + v] + offset;
                }
            }
            m_Text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying && m_Text == null) Debug.LogWarning($"{LogTag} '{name}': assign the TextMeshPro text it types into.", this);
        }
#endif
    }
}
