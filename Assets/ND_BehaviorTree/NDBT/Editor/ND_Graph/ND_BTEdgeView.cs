using ND.GraphUI;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND_BehaviorTree.Editor.CustomGraph
{
    /// <summary>Parent → child edge: live-debug colouring, and the weight / utility-score pill of weighted composites.</summary>
    public class ND_BTEdgeView : GraphEdgeView
    {
        public new ND_BTPortView OutputPort => (ND_BTPortView)base.OutputPort;
        public new ND_BTPortView InputPort => (ND_BTPortView)base.InputPort;

        private Graph.LiveState m_LiveState = Graph.LiveState.Inactive;

        private static readonly Color RunningColor = new Color(1f, 0.8f, 0.2f, 1f);
        private static readonly Color SuccessColor = new Color(0.35f, 0.9f, 0.35f, 1f);
        private static readonly Color FailureColor = new Color(0.95f, 0.35f, 0.35f, 1f);

        public ND_BTEdgeView(GraphPortView outputPort) : base(outputPort, ND_BehaviorTreeSetting.Instance.edgeTheme)
        {
            EdgeLabel.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                evt.menu.AppendAction("Edit Text", a => Text = EditorInputDialog.Show("Edit Label", "Enter text:", Text));
            }));
        }

        /// <summary>Live debug: colours the edge by the child's state; running edges animate flowing dots.</summary>
        public void SetLiveState(Graph.LiveState state)
        {
            m_LiveState = state;
            Color? color = state switch
            {
                Graph.LiveState.Running => RunningColor,
                Graph.LiveState.Succeeded => SuccessColor,
                Graph.LiveState.Failed => FailureColor,
                _ => (Color?)null
            };
            SetHighlight(color, state != Graph.LiveState.Inactive, state == Graph.LiveState.Running);
        }

        public Graph.LiveState LiveState => m_LiveState;

        private Label m_WeightLabel;
        private string m_StaticWeightText;
        private int m_LiveScoreKey = int.MinValue;

        /// <summary>Weight/chance pill near the child end (null hides it). Set by the canvas for weighted parents.</summary>
        public void SetWeightLabel(string text, string labelTooltip)
        {
            m_StaticWeightText = text;
            m_LiveScoreKey = int.MinValue;
            if (string.IsNullOrEmpty(text))
            {
                if (m_WeightLabel != null) m_WeightLabel.style.display = DisplayStyle.None;
                return;
            }
            EnsureWeightLabel();
            m_WeightLabel.style.display = DisplayStyle.Flex;
            m_WeightLabel.text = text;
            m_WeightLabel.tooltip = labelTooltip;
            m_WeightLabel.RemoveFromClassList("nd-edge-weight--chosen");
            MarkDirtyRepaint();
        }

        /// <summary>Live Utility score; the text is rebuilt only when the rounded score or the chosen state changes.</summary>
        public void SetLiveScore(float score, bool chosen)
        {
            if (m_WeightLabel == null) return;
            int key = float.IsNaN(score) ? int.MaxValue : Mathf.RoundToInt(score * 100f) * 2 + (chosen ? 1 : 0);
            if (key == m_LiveScoreKey) return;
            m_LiveScoreKey = key;
            m_WeightLabel.text = float.IsNaN(score) ? m_StaticWeightText : (chosen ? "▶ " : string.Empty) + score.ToString("0.##");
            m_WeightLabel.EnableInClassList("nd-edge-weight--chosen", chosen);
        }

        private void EnsureWeightLabel()
        {
            if (m_WeightLabel != null) return;
            m_WeightLabel = new Label { pickingMode = PickingMode.Position };
            m_WeightLabel.AddToClassList("nd-edge-weight");
            m_WeightLabel.style.position = Position.Absolute;
            ND_BTStyles.Apply(m_WeightLabel, ND_BTStyles.NodeAttachments);
            Add(m_WeightLabel);
        }

        protected override void OnPathDrawn(Vector2 start, Vector2 end)
        {
            if (m_WeightLabel == null || m_WeightLabel.resolvedStyle.display == DisplayStyle.None) return;
            Vector2 at = PointAt(0.72f, start, end);
            m_WeightLabel.style.left = at.x - m_WeightLabel.layout.width / 2f;
            m_WeightLabel.style.top = at.y - m_WeightLabel.layout.height / 2f;
        }
    }
}
