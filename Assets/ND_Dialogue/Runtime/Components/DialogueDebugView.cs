using System.Collections.Generic;
using System.Linq;
using ND.Localization;
using UnityEngine;

namespace ND.Dialogue
{
    /// <summary>
    /// Minimal play-mode UI (IMGUI) to try a database without building a dialogue UI: start any conversation, read
    /// lines (speaker tinted by emotion), pick choices, switch language, and watch variables, attributes and events.
    /// For testing and samples — a game would listen to <see cref="DialogueRunner"/> events with its own UI.
    /// </summary>
    [AddComponentMenu("ND Dialogue/Dialogue Debug View")]
    public class DialogueDebugView : MonoBehaviour, IDialogueDatabaseOwner
    {
        [SerializeField] private DialogueDatabase m_Database;
        [Tooltip("Optional: use this manager's world state and runner (its UI shows the lines; this view adds buttons and the state panel).")]
        [SerializeField] private DialogueManager m_Manager;
        [Tooltip("Started automatically on Play (empty = wait for a button). Ignored with a manager (it has its own).")]
        [SerializeField] private string m_StartConversation;
        [SerializeField] private bool m_ShowState = true;

        private IDialogueContext m_Context;
        private DialogueRunner m_Runner;
        private readonly List<string> m_Events = new List<string>();
        private readonly List<(string name, string value)> m_State = new List<(string, string)>();
        private float m_NextStateRefresh;
        private Vector2 m_StateScroll;

        public DialogueDatabase Database => m_Database;
        public DialogueRunner Runner => m_Runner;

        private void Start()
        {
            if (m_Manager != null)
            {
                if (!m_Manager.IsReady)
                {
                    Debug.LogError($"[DialogueDebugView] '{name}': manager '{m_Manager.name}' is not ready (state={m_Manager.State}).", this);
                    enabled = false;
                    return;
                }
                m_Database = m_Manager.Database;
                m_Context = m_Manager.Context;
                m_Runner = m_Manager.Runner;
                return;
            }
            if (m_Database == null)
            {
                Debug.LogError($"[DialogueDebugView] '{name}': assign a Dialogue Database.", this);
                enabled = false;
                return;
            }
            m_Context = m_Database.CreateContext();
            if (m_Context == null)
            {
                Debug.LogError($"[DialogueDebugView] '{name}': the database's schema source returned no context.", this);
                enabled = false;
                return;
            }
            m_Runner = new DialogueRunner(m_Database, m_Context);
            if (!string.IsNullOrEmpty(m_StartConversation)) m_Runner.Start(m_StartConversation);
        }

        private void OnEnable() => DialogueEvents.Raised += OnEvent;
        private void OnDisable() => DialogueEvents.Raised -= OnEvent;

        private void OnEvent(string eventName, string argument)
        {
            m_Events.Insert(0, string.IsNullOrEmpty(argument) ? eventName : $"{eventName}({argument})");
            if (m_Events.Count > 6) m_Events.RemoveAt(m_Events.Count - 1);
        }

        private void OnGUI()
        {
            if (m_Runner == null) return;
            const float Pad = 10f;
            float width = Mathf.Min(560f, Screen.width - 2 * Pad);

            GUILayout.BeginArea(new Rect(Pad, Pad, width, Screen.height - 2 * Pad));
            DrawToolbar();
            GUILayout.Space(6);
            if (m_Manager == null) DrawConversation(); // with a manager its UI shows the lines
            else DrawEvents();
            GUILayout.EndArea();

            if (m_ShowState)
            {
                float stateWidth = Mathf.Min(320f, Screen.width - width - 3 * Pad);
                if (stateWidth > 120f)
                {
                    GUILayout.BeginArea(new Rect(Screen.width - stateWidth - Pad, Pad, stateWidth, Screen.height - 2 * Pad), GUI.skin.box);
                    DrawState();
                    GUILayout.EndArea();
                }
            }
        }

        private void DrawToolbar()
        {
            GUILayout.BeginHorizontal();
            foreach (var conversation in m_Database.conversations.Where(c => c != null))
            {
                if (GUILayout.Button(conversation.title))
                {
                    m_Runner.Stop();
                    if (m_Manager != null) m_Manager.StartConversation(conversation.id);
                    else m_Runner.Start(conversation.id);
                }
            }
            GUILayout.FlexibleSpace();
            if (m_Manager != null)
            {
                const string Slot = "debug";
                if (GUILayout.Button("Save", GUILayout.Width(48))) m_Manager.Save(Slot);
                GUI.enabled = m_Manager.HasSave(Slot);
                if (GUILayout.Button("Load", GUILayout.Width(48))) m_Manager.Load(Slot);
                GUI.enabled = true;
            }
            if (LocalizationService.IsReady)
            {
                foreach (var locale in LocalizationService.Settings.locales.Where(l => l != null))
                {
                    bool current = locale == LocalizationService.SelectedLocale;
                    if (GUILayout.Toggle(current, locale.code, GUI.skin.button, GUILayout.Width(36)) && !current)
                        LocalizationService.SetLocale(locale);
                }
            }
            GUILayout.EndHorizontal();
        }

        private void DrawConversation()
        {
            GUILayout.BeginVertical(GUI.skin.box);
            switch (m_Runner.State)
            {
                case DialogueRunnerState.ShowingLine:
                {
                    var line = m_Runner.CurrentLine;
                    var previous = GUI.color;
                    GUI.color = line.Emotion != null ? line.Emotion.ResolveTint() : Color.white;
                    string emotion = line.Emotion != null ? $"  ({line.Emotion.id})" : string.Empty;
                    GUILayout.Label($"<b>{line.SpeakerName}</b>{emotion}", RichLabel());
                    GUI.color = previous;
                    GUILayout.Label(line.Text, RichLabel());
                    if (GUILayout.Button("Continue ▶")) m_Runner.Continue();
                    break;
                }
                case DialogueRunnerState.WaitingForChoice:
                    GUILayout.Label("<i>Choose:</i>", RichLabel());
                    foreach (var choice in m_Runner.CurrentChoices.ToList())
                        if (GUILayout.Button(choice.Text)) m_Runner.Choose(choice.Index);
                    break;
                default:
                    GUILayout.Label(m_Runner.State == DialogueRunnerState.Ended ? "(conversation ended — pick one above)" : "(pick a conversation above)");
                    break;
            }
            GUILayout.EndVertical();
            DrawEvents();
        }

        private void DrawEvents()
        {
            if (m_Events.Count > 0)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label("<b>Events</b>", RichLabel());
                foreach (var e in m_Events) GUILayout.Label("⚡ " + e);
                GUILayout.EndVertical();
            }
        }

        private void DrawState()
        {
            if (Time.unscaledTime >= m_NextStateRefresh) RefreshState();
            GUILayout.Label("<b>World & characters</b>", RichLabel());
            m_StateScroll = GUILayout.BeginScrollView(m_StateScroll);
            foreach (var (n, v) in m_State)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(n, GUILayout.Width(170));
                GUILayout.Label(v);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
        }

        /// <summary>Reads every schema variable and quest (twice a second, not every frame).</summary>
        private void RefreshState()
        {
            m_NextStateRefresh = Time.unscaledTime + 0.5f;
            m_State.Clear();
            var schema = m_Database.Schema;
            foreach (var n in schema.VariableNames.OrderBy(n => n))
                if (m_Context.TryGetVariable(n, out var value)) m_State.Add((n, value.ToString()));
            foreach (var q in schema.QuestNames.OrderBy(q => q))
                if (m_Context.TryGetQuestState(q, out var state)) m_State.Add(("quest " + q, state.ToString()));
        }

        private static GUIStyle s_Rich;
        private static GUIStyle RichLabel() => s_Rich ??= new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true };
    }
}
