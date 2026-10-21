using UnityEngine;

namespace ND.Dialogue
{
    /// <summary>
    /// Base of anything that shows a running conversation. The manager attaches the runner when a conversation starts
    /// and detaches it when it ends; the view subscribes to the runner's events in between.
    /// </summary>
    public abstract class DialogueViewBase : MonoBehaviour
    {
        protected DialogueRunner AttachedRunner { get; private set; }

        public void Attach(DialogueRunner runner)
        {
            if (AttachedRunner == runner) return;
            Detach();
            AttachedRunner = runner;
            if (runner == null) return;
            runner.LineStarted += OnLine;
            runner.ChoicesPresented += OnChoices;
            OnAttached();
        }

        public void Detach()
        {
            if (AttachedRunner == null) return;
            AttachedRunner.LineStarted -= OnLine;
            AttachedRunner.ChoicesPresented -= OnChoices;
            AttachedRunner = null;
            OnDetached();
        }

        protected abstract void OnAttached();
        protected abstract void OnDetached();
        protected abstract void OnLine(DialogueLine line);
        protected abstract void OnChoices(System.Collections.Generic.IReadOnlyList<DialogueChoice> choices);
    }
}
