using System.Collections.Generic;
using System.Reflection;
using ND_BehaviorTree.Editor.Graph;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND_BehaviorTree.Editor.CustomGraph
{
    /// <summary>
    /// A decorator or service as a flush, full-width row of its node (styled by ND_NodeAttachments.uss):
    ///   ┃ ◆ [icon] Title                       [STATUS]
    ///   ┃          summary of its settings
    ///   ┃          key slots
    ///   ┃          ▬▬▬▬ progress (TimeLimit/Cooldown, service tick)
    /// Live updates only touch the UI when the state or (rounded) progress changes.
    /// </summary>
    public class ND_BTAttachmentView : VisualElement
    {
        public Node Attached { get; }
        public bool IsService { get; }

        private readonly Label m_Summary;
        private readonly Label m_Pill;
        private readonly VisualElement m_Progress;
        private readonly VisualElement m_ProgressFill;
        private readonly ND_BTKeySlotView m_Slots;
        private readonly bool m_HasProgress;

        private LiveState m_ShownState = LiveState.Inactive;
        private int m_ShownProgress = -1; // percent, -1 = hidden

        private static readonly Dictionary<System.Type, FieldInfo[]> s_SummaryFields = new Dictionary<System.Type, FieldInfo[]>();

        public ND_BTAttachmentView(Node attached, bool isService, int index)
        {
            Attached = attached;
            IsService = isService;
            m_HasProgress = ProgressReader.HasProgress(attached.GetType());

            var info = attached.GetType().GetCustomAttribute<NodeInfoAttribute>();
            string title = info?.title ?? ObjectNames.NicifyVariableName(attached.GetType().Name);

            AddToClassList("nd-attachment");
            AddToClassList(isService ? "nd-attachment--service" : "nd-attachment--decorator");
            if (index > 0) AddToClassList("nd-attachment--divider");
            tooltip = (isService ? "Service — ticks while its node runs." : $"Decorator #{index + 1} — evaluated top to bottom before its node.") +
                      "\nDouble-click to edit, right-click for options.";

            var header = new VisualElement { pickingMode = PickingMode.Ignore };
            header.AddToClassList("nd-attachment__header");
            header.Add(MakeLabel(isService ? "⟳" : "◆", "nd-attachment__glyph"));

            if (info != null && !string.IsNullOrEmpty(info.iconPath))
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(info.iconPath);
                if (tex != null)
                {
                    var icon = new Image { image = tex, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
                    icon.AddToClassList("nd-attachment__icon");
                    header.Add(icon);
                }
            }

            var texts = new VisualElement { pickingMode = PickingMode.Ignore };
            texts.AddToClassList("nd-attachment__texts");
            texts.Add(MakeLabel(title, "nd-attachment__title"));
            m_Summary = MakeLabel(string.Empty, "nd-attachment__summary");
            m_Summary.style.display = DisplayStyle.None; // shown by RefreshInfo when there is a summary
            texts.Add(m_Summary);
            header.Add(texts);

            m_Pill = MakeLabel(string.Empty, "nd-attachment__pill");
            header.Add(m_Pill);
            Add(header);

            m_Slots = new ND_BTKeySlotView(attached, compact: true);
            Add(m_Slots);

            m_Progress = new VisualElement { pickingMode = PickingMode.Ignore };
            m_Progress.AddToClassList("nd-progress");
            m_ProgressFill = new VisualElement { pickingMode = PickingMode.Ignore };
            m_ProgressFill.AddToClassList("nd-progress__fill");
            m_Progress.Add(m_ProgressFill);
            Add(m_Progress);

            RefreshInfo();
        }

        private static Label MakeLabel(string text, string cssClass)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList(cssClass);
            return label;
        }

        /// <summary>Re-reads settings and key slots (called periodically; writes only what changed).</summary>
        public void RefreshInfo()
        {
            string summary = BuildSummary(Attached);
            if (m_Summary.text != summary)
            {
                m_Summary.text = summary;
                m_Summary.style.display = string.IsNullOrEmpty(summary) ? DisplayStyle.None : DisplayStyle.Flex;
            }
            m_Slots.Refresh();
        }

        public void SetLive(LiveState state, Node runtime)
        {
            if (state != m_ShownState)
            {
                m_ShownState = state;
                EnableInClassList("nd-attachment--running", state == LiveState.Running);
                EnableInClassList("nd-attachment--done", state == LiveState.Succeeded);
                EnableInClassList("nd-attachment--failed", state == LiveState.Failed);
                m_Pill.EnableInClassList("nd-pill--running", state == LiveState.Running);
                m_Pill.EnableInClassList("nd-pill--done", state == LiveState.Succeeded);
                m_Pill.EnableInClassList("nd-pill--failed", state == LiveState.Failed);
                m_Pill.text = state switch
                {
                    LiveState.Running => "RUNNING",
                    LiveState.Succeeded => "✔ DONE",
                    LiveState.Failed => "✖ FAILED",
                    _ => string.Empty
                };
                m_ProgressFill.EnableInClassList("nd-progress__fill--running", state == LiveState.Running);
            }

            int percent = -1;
            if (m_HasProgress && runtime != null && (state == LiveState.Running || (IsService && state != LiveState.Inactive))
                && ProgressReader.TryGetProgress(runtime, out float progress))
            {
                percent = Mathf.RoundToInt(progress * 100f);
            }

            if (percent != m_ShownProgress)
            {
                m_ShownProgress = percent;
                m_Progress.EnableInClassList("nd-progress--visible", percent >= 0);
                if (percent >= 0) m_ProgressFill.style.width = Length.Percent(percent);
            }
        }

        public void ClearLive() => SetLive(LiveState.Inactive, null);

        /// <summary>"Time Limit 1  ·  …" from simple serialized fields (keys are shown as slots instead).</summary>
        private static string BuildSummary(Node node)
        {
            var parts = new List<string>(3);
            if (node is ServiceNode service) parts.Add(service.interval <= 0f ? "every frame" : $"every {Format(service.interval)}s");

            foreach (var field in GetSummaryFields(node.GetType()))
            {
                if (parts.Count >= 3) break;
                string value = FormatValue(field.GetValue(node));
                if (value != null) parts.Add($"{ObjectNames.NicifyVariableName(field.Name)} {value}");
            }
            return string.Join("  ·  ", parts);
        }

        private static FieldInfo[] GetSummaryFields(System.Type type)
        {
            if (s_SummaryFields.TryGetValue(type, out var cached)) return cached;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            var chain = new List<System.Type>();
            for (var t = type; t != null && t != typeof(DecoratorNode) && t != typeof(ServiceNode) && t != typeof(AuxiliaryNode) && t != typeof(Node); t = t.BaseType) chain.Add(t);
            chain.Reverse();

            var fields = new List<FieldInfo>();
            foreach (var t in chain)
            {
                foreach (var field in t.GetFields(flags))
                {
                    if (field.IsStatic || field.IsNotSerialized || field.GetCustomAttribute<HideInInspector>() != null) continue;
                    if (!field.IsPublic && field.GetCustomAttribute<SerializeField>() == null) continue;
                    if (typeof(Key).IsAssignableFrom(field.FieldType) || typeof(Node).IsAssignableFrom(field.FieldType)) continue;
                    fields.Add(field);
                }
            }
            var result = fields.ToArray();
            s_SummaryFields[type] = result;
            return result;
        }

        private static string FormatValue(object value)
        {
            switch (value)
            {
                case null: return null;
                case float f: return Format(f);
                case int i: return i.ToString();
                case bool b: return b ? "✓" : "✗";
                case string s: return string.IsNullOrEmpty(s) ? null : $"\"{s}\"";
                case System.Enum e: return ObjectNames.NicifyVariableName(e.ToString());
                case Object o: return o != null ? o.name : null;
                default: return null;
            }
        }

        private static string Format(float f) => f.ToString("0.##");
    }
}
