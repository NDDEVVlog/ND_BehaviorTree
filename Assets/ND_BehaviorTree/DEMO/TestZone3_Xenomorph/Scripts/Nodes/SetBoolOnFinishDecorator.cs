using UnityEngine;

namespace ND_BehaviorTree.Demo.Xeno
{
    /// <summary>
    /// Sub-node: when the decorated node finishes, writes a bool key (typically clearing the flag that started the
    /// branch, e.g. HeardNoise = false). Not written when the node is interrupted, so an aborted branch resumes later.
    /// Replaces a trailing "Set Bool" action in a sequence. Passes the node's status through.
    /// </summary>
    [NodeDescription("Sub-node: writes a bool key when its node finishes (e.g. clears the flag that started the branch).", "Not written when the node is interrupted, so an aborted branch resumes later. 'When' picks success, failure or any result.")]
    [NodeInfo("Set Bool On Finish", "Decorator/Set Bool On Finish", true, true, iconPath: "Assets/ND_BehaviorTree/NDBT/Icons/boolean.png")]
    public class SetBoolOnFinishDecorator : DecoratorNode
    {
        public enum Trigger { AnyResult, OnSuccess, OnFailure }

        [BlackboardKeyType(typeof(bool))] public Key key;
        public bool value;
        [Tooltip("AnyResult also covers failure, so a branch that cannot complete does not retry forever.")]
        public Trigger when = Trigger.AnyResult;

        protected override Status OnProcess()
        {
            var status = child != null ? child.Process() : Status.Success;
            if (status == Status.Running) return status;

            bool write = when == Trigger.AnyResult
                         || (when == Trigger.OnSuccess && status == Status.Success)
                         || (when == Trigger.OnFailure && status == Status.Failure);
            if (write)
            {
                if (key == null || blackboard == null) Debug.LogError($"[SetBoolOnFinishDecorator] '{name}': the key slot is not linked.", this);
                else blackboard.SetValue(key.keyName, value);
            }
            return status;
        }
    }
}
