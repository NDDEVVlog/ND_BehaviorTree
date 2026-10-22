using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace ND.Dialogue
{
    /// <summary>What a command handler gets: the command, the line it belongs to, and target lookup.</summary>
    public sealed class SequenceCall
    {
        public SequenceCommand Command;
        public DialogueLine Line;
        public DialogueSequencer Sequencer;

        public string Arg(int index) => Command.Arg(index);

        /// <summary>The scene object named by argument <paramref name="index"/> (see <see cref="DialogueSequencer.Resolve"/>); logged when missing.</summary>
        public GameObject Target(int index) => Sequencer.Resolve(Arg(index), Line, Command);
    }

    /// <summary>Runs a command. Returns the seconds it keeps the sequence busy (0 = instant), or a negative number when it failed (log why).</summary>
    public delegate float SequenceCommandHandler(SequenceCall call);

    /// <summary>
    /// Plays each line's sequence (<see cref="DialogueEntry.sequence"/>) while the conversation runs: camera cuts,
    /// animations, sounds, objects on/off, messages — at their <c>@seconds</c>. The conversation never waits for it;
    /// <c>Continue()</c> moves the conversation on by itself (a cut-scene line).
    /// <para>Built-in commands: <c>Wait(s)</c>, <c>Anim(target, trigger or state)</c>, <c>Audio(target)</c>,
    /// <c>SetActive(target, true|false)</c>, <c>Camera(target)</c> (the camera takes the target's position and
    /// rotation), <c>LookAt(target, at)</c>, <c>SendMessage(target, method, argument?)</c>, <c>Continue()</c>. Add your
    /// own with <see cref="RegisterCommand"/>.</para>
    /// <para>Targets: <c>speaker</c> (who says the line), a name from Targets, or a character's name (its
    /// <see cref="DialogueActorObject"/>).</para>
    /// </summary>
    [AddComponentMenu("ND Dialogue/Dialogue Sequencer")]
    public class DialogueSequencer : MonoBehaviour
    {
        private const string LogTag = "[DialogueSequencer]";

        [Serializable]
        public class Target
        {
            public string name;
            public GameObject gameObject;
        }

        [SerializeField] private DialogueManager m_Manager;
        [Tooltip("Objects sequences may name (besides characters, which use their Dialogue Actor Object).")]
        [SerializeField] private List<Target> m_Targets = new List<Target>();
        [Tooltip("Moved by Camera(target).")]
        [SerializeField] private Transform m_Camera;

        private static readonly Dictionary<string, SequenceCommandHandler> s_Custom = new Dictionary<string, SequenceCommandHandler>(StringComparer.OrdinalIgnoreCase);

        private readonly List<SequenceCommand> m_Pending = new List<SequenceCommand>();
        private DialogueLine m_Line;
        private float m_Time;
        private float m_BusyUntil;
        private bool m_Playing;

        public DialogueManager Manager { get => m_Manager; set { Unbind(); m_Manager = value; if (isActiveAndEnabled) Bind(); } }
        public List<Target> Targets => m_Targets;
        public Transform CameraTransform { get => m_Camera; set => m_Camera = value; }
        public bool IsPlaying => m_Playing;

        /// <summary>(command) A command ran (also custom ones).</summary>
        public event Action<SequenceCommand> CommandRun;
        /// <summary>The sequence of the current line is done (every command ran and the busy time passed).</summary>
        public event Action Finished;

        /// <summary>Adds a command for every sequencer (e.g. <c>Shake</c>). A built-in name cannot be replaced.</summary>
        public static bool RegisterCommand(string name, SequenceCommandHandler handler)
        {
            if (string.IsNullOrEmpty(name) || handler == null || IsBuiltIn(name))
            {
                Debug.LogError($"{LogTag} {nameof(RegisterCommand)} rejected: name='{name}', handler={(handler != null)}, builtIn={IsBuiltIn(name)}.");
                return false;
            }
            s_Custom[name] = handler;
            return true;
        }

        public static void UnregisterCommand(string name)
        {
            if (!string.IsNullOrEmpty(name)) s_Custom.Remove(name);
        }

        private static readonly string[] s_BuiltIns = { "Wait", "Anim", "Audio", "SetActive", "Camera", "LookAt", "SendMessage", "Continue" };

        public static bool IsBuiltIn(string name) => Array.FindIndex(s_BuiltIns, n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase)) >= 0;
        public static bool IsKnown(string name) => IsBuiltIn(name) || (name != null && s_Custom.ContainsKey(name));

        private void OnEnable() => Bind();
        private void OnDisable() => Unbind();

        private void Bind()
        {
            if (m_Manager == null) return;
            Unbind(); // never twice
            m_Manager.LineStarted += OnLineStarted;
            m_Manager.ConversationEnded += OnConversationEnded;
        }

        private void Unbind()
        {
            if (m_Manager == null) return;
            m_Manager.LineStarted -= OnLineStarted;
            m_Manager.ConversationEnded -= OnConversationEnded;
        }

        private void OnLineStarted(DialogueLine line)
        {
            if (line.LineIndex != 0) return; // a sequence belongs to the entry: it starts with its first line
            Play(line.Entry?.sequence, line);
        }

        private void OnConversationEnded(Conversation conversation) => Stop();

        /// <summary>Starts <paramref name="sequence"/> for <paramref name="line"/> (stopping the one playing). Malformed commands are logged and skipped.</summary>
        public void Play(string sequence, DialogueLine line)
        {
            Stop();
            if (string.IsNullOrWhiteSpace(sequence)) return;
            var errors = new List<string>();
            var commands = DialogueSequence.Parse(sequence, errors);
            foreach (var error in errors) Debug.LogError($"{LogTag} Entry {line.Entry?.id} of '{line.Conversation?.title}': {error}", this);
            if (commands.Count == 0) return;
            m_Pending.AddRange(StableByTime(commands)); // by time; equal times keep the written order
            m_Line = line;
            m_Time = 0f;
            m_BusyUntil = 0f;
            m_Playing = true;
            // Runs from the next Update: never inside the runner's LineStarted, so every listener sees this line first.
        }

        private static List<SequenceCommand> StableByTime(List<SequenceCommand> commands)
        {
            var indexed = new List<(SequenceCommand command, int index)>();
            for (int i = 0; i < commands.Count; i++) indexed.Add((commands[i], i));
            indexed.Sort((a, b) => a.command.At != b.command.At ? a.command.At.CompareTo(b.command.At) : a.index.CompareTo(b.index));
            return indexed.ConvertAll(x => x.command);
        }

        /// <summary>Drops the commands not run yet (a new line, or the conversation ended).</summary>
        public void Stop()
        {
            m_Pending.Clear();
            m_Playing = false;
        }

        private void Update()
        {
            if (m_Playing) Advance(Time.deltaTime);
        }

        /// <summary>Moves the sequence <paramref name="seconds"/> on, running the commands that are due (Update calls it; tests too).</summary>
        public void Advance(float seconds)
        {
            if (!m_Playing) return;
            m_Time += seconds;
            while (m_Playing && m_Pending.Count > 0 && m_Pending[0].At <= m_Time)
            {
                var command = m_Pending[0];
                m_Pending.RemoveAt(0);
                float busy = Run(command);
                if (busy > 0f) m_BusyUntil = Mathf.Max(m_BusyUntil, command.At + busy);
            }
            if (m_Playing && m_Pending.Count == 0 && m_Time >= m_BusyUntil)
            {
                m_Playing = false;
                Finished?.Invoke();
            }
        }

        private float Run(SequenceCommand command)
        {
            var call = new SequenceCall { Command = command, Line = m_Line, Sequencer = this };
            float busy;
            if (s_Custom.TryGetValue(command.Name, out var handler))
            {
                try { busy = handler(call); }
                catch (Exception e) { Debug.LogException(e, this); busy = -1f; }
            }
            else busy = RunBuiltIn(call);
            CommandRun?.Invoke(command);
            return busy;
        }

        private float RunBuiltIn(SequenceCall call)
        {
            var c = call.Command;
            switch (c.Name.ToLowerInvariant())
            {
                case "wait":
                    return TryFloat(c, 0, out float wait) ? wait : -1f;
                case "anim":
                {
                    var animator = Component<Animator>(call, 0);
                    string state = c.Arg(1);
                    if (animator == null || string.IsNullOrEmpty(state)) return Fail(c, "needs (target, trigger or state) and an Animator on the target");
                    foreach (var parameter in animator.parameters)
                        if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == state) { animator.SetTrigger(state); return 0f; }
                    animator.Play(state);
                    return 0f;
                }
                case "audio":
                {
                    var source = Component<AudioSource>(call, 0);
                    if (source == null) return Fail(c, "needs an AudioSource on the target");
                    source.Play();
                    return source.clip != null ? source.clip.length : 0f;
                }
                case "setactive":
                {
                    var target = call.Target(0);
                    if (target == null || !bool.TryParse(c.Arg(1), out bool active)) return Fail(c, "needs (target, true|false)");
                    target.SetActive(active);
                    return 0f;
                }
                case "camera":
                {
                    var target = call.Target(0);
                    if (target == null || m_Camera == null) return Fail(c, $"needs a target and the sequencer's Camera (camera assigned: {m_Camera != null})");
                    m_Camera.SetPositionAndRotation(target.transform.position, target.transform.rotation);
                    return 0f;
                }
                case "lookat":
                {
                    var target = call.Target(0);
                    var at = call.Target(1);
                    if (target == null || at == null) return Fail(c, "needs (target, what to look at)");
                    target.transform.LookAt(at.transform);
                    return 0f;
                }
                case "sendmessage":
                {
                    var target = call.Target(0);
                    string method = c.Arg(1);
                    if (target == null || string.IsNullOrEmpty(method)) return Fail(c, "needs (target, method, argument?)");
                    if (c.Args.Length > 2) target.SendMessage(method, c.Arg(2), SendMessageOptions.RequireReceiver);
                    else target.SendMessage(method, SendMessageOptions.RequireReceiver);
                    return 0f;
                }
                case "continue":
                {
                    var runner = m_Manager != null ? m_Manager.Runner : null;
                    if (runner == null || runner.State != DialogueRunnerState.ShowingLine || runner.CurrentLine.Entry != m_Line.Entry)
                        return Fail(c, $"the line is no longer shown (state={runner?.State.ToString() ?? "no runner"})");
                    m_Pending.Clear(); // the next line starts its own sequence
                    var entry = m_Line.Entry; // Continue may start the next line's sequence, which replaces m_Line
                    while (runner.State == DialogueRunnerState.ShowingLine && runner.CurrentLine.Entry == entry) runner.Continue();
                    return 0f;
                }
                default:
                    return Fail(c, "unknown command (register it with DialogueSequencer.RegisterCommand)");
            }
        }

        private T Component<T>(SequenceCall call, int index) where T : Component
        {
            var target = call.Target(index);
            return target != null && target.TryGetComponent(out T component) ? component : null;
        }

        private bool TryFloat(SequenceCommand c, int index, out float value)
        {
            if (float.TryParse(c.Arg(index), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && value >= 0f) return true;
            Fail(c, $"'{c.Arg(index)}' is not a number of seconds");
            return false;
        }

        private float Fail(SequenceCommand c, string reason)
        {
            Debug.LogError($"{LogTag} {c} (entry {m_Line.Entry?.id} of '{m_Line.Conversation?.title}'): {reason}.", this);
            return -1f;
        }

        /// <summary>
        /// The object a sequence names: <c>speaker</c> (who says the line), an entry of Targets, or the
        /// <see cref="DialogueActorObject"/> playing that character. Logged when nothing matches.
        /// </summary>
        public GameObject Resolve(string targetName, DialogueLine line, SequenceCommand command = null)
        {
            if (string.IsNullOrEmpty(targetName))
            {
                Debug.LogError($"{LogTag} {command}: no target given.", this);
                return null;
            }
            if (string.Equals(targetName, "speaker", StringComparison.OrdinalIgnoreCase))
                targetName = line.Speaker != null ? line.Speaker.name : null;
            foreach (var target in m_Targets)
                if (target != null && target.gameObject != null && target.name == targetName) return target.gameObject;
            var actor = DialogueActorObject.Find(targetName);
            if (actor != null) return actor.gameObject;
            Debug.LogError($"{LogTag} {command}: no target '{targetName}' (add it to Targets, or put a Dialogue Actor Object on the character).", this);
            return null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying && m_Manager == null) Debug.LogWarning($"{LogTag} '{name}': assign the Dialogue Manager whose lines it follows.", this);
        }
#endif
    }
}
