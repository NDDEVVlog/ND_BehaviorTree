using UnityEngine;

namespace ND_BehaviorTree.Demo.Xeno
{
    /// <summary>
    /// Drops the alien out of the vent nearest the director's hint (or a Vector3 key). Used by the Init branch for the
    /// alien's first appearance. Succeeds immediately if it is not in a vent.
    /// </summary>
    [NodeInfo("Emerge From Vent", "Action/Xeno/Emerge From Vent", true, false, iconPath: "Assets/ND_BehaviorTree/NDBT/Icons/Wait.png")]
    public class EmergeFromVentNode : ActionNode
    {
        [BlackboardKeyType(typeof(XenoBody))] public Key bodyKey;
        [Tooltip("Unlinked = ask the director for its hint (the blackboard copy may not be written yet during Init).")]
        [BlackboardKeyType(typeof(Vector3))] public Key nearKey;

        [Tooltip("Never drop out of a vent closer than this to the player.")]
        public float minDistanceFromPlayer = 10f;

        protected override Status OnProcess()
        {
            var body = KeyResolver.Resolve<XenoBody>(this, bodyKey)?.GetValue();
            if (body == null) return Status.Failure;
            if (!body.InVent) return Status.Success;

            Vector3 near;
            if (nearKey != null)
            {
                var key = KeyResolver.Resolve<Vector3>(this, nearKey);
                if (key == null) return Status.Failure;
                near = key.GetValue();
            }
            else if (body.Director != null)
            {
                near = body.Director.Hint;
            }
            else
            {
                Debug.LogError($"[EmergeFromVentNode] '{name}': no near key linked and the body has no director.", this);
                return Status.Failure;
            }

            var vent = XenoVent.NearestAwayFrom(near, body.Player.transform.position, minDistanceFromPlayer);
            if (vent == null) return Status.Failure;
            body.EmergeAt(vent);
            return Status.Success;
        }
    }
}
