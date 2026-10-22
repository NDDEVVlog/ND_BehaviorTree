using System;
using System.Collections.Generic;
using UnityEngine;

namespace ND_BehaviorTree
{
    public abstract class Node : ScriptableObject
    {
        #region Core Properties & State

        /// <summary>
        /// A reference to the BehaviorTree instance that owns this node.
        /// This is set at runtime when the tree is cloned. It is not serialized.
        /// </summary>
        [NonSerialized] public BehaviorTree ownerTree;

        /// <summary>
        /// A convenience property to get the Blackboard associated with this node's owner tree.
        /// Returns null if the node is not part of a tree at runtime.
        /// </summary>
        public Blackboard blackboard => ownerTree?.blackboard;

        /// <summary>
        /// Delegate for custom interruption logic. Can be subscribed to from other systems.
        /// </summary>
        public Action InteruptAction;

        /// <summary>
        /// Describes the possible states of a node during its execution.
        /// </summary>
        public enum Status { Success, Failure, Running, None }

        /// <summary>
        /// The current status of the node. This is updated at runtime.
        /// </summary>
        public Status status { get; protected set; } = Status.None;

        [NonSerialized] private bool _isProcessing = false;

#if UNITY_EDITOR
        /// <summary>Editor live-debug only: Time.frameCount of the last Process() call (-1 = never).</summary>
        [NonSerialized] public int debugLastFrame = -1;
#endif

        #endregion

        #region Editor Data

        [SerializeField] private string m_guid;
        [SerializeField] private Rect m_position;
        public string typeName;

        /// <summary>
        /// Weight of this node inside a parent that chooses by weight (<see cref="IWeightedComposite"/>: Random Rate
        /// Selector, Utility Selector). Ignored by every other parent, which run children left to right.
        /// Attached <see cref="IWeightModifier"/> decorators (e.g. Weight From Key) adjust it at runtime.
        /// </summary>
        [UnityEngine.Serialization.FormerlySerializedAs("priority")]
        [Tooltip("Only used when the parent picks children by weight (Random Rate Selector, Utility Selector).")]
        public float weight = 1f;

        /// <summary>Editor only: points the edge from this node's parent bends through (double-click the edge to add one).</summary>
        [HideInInspector] public List<Vector2> parentEdgeBends = new List<Vector2>();

        public string id => m_guid;
        public Rect position { get => m_position; set => m_position = value; }

        #endregion

        #region Attached Decorators

        /// <summary>
        /// Decorators attached to this node, evaluated top (index 0, outermost) to bottom.
        /// Stored as sub-assets and NOT listed in BehaviorTree.nodes. Their 'child' stays null in the asset;
        /// BehaviorTree.Clone() builds the runtime chain: parent -> decorators[0] -> ... -> decorators[n-1] -> this node.
        /// </summary>
        [HideInInspector] public List<DecoratorNode> decorators = new List<DecoratorNode>();

        /// <summary>
        /// Services attached to this node. They tick (on their own interval) every frame this node is processed,
        /// and are reset when this node finishes, so 'runOnEnter' fires again on the next activation.
        /// Stored as sub-assets and NOT listed in BehaviorTree.nodes.
        /// </summary>
        [HideInInspector] public List<ServiceNode> services = new List<ServiceNode>();

        public void AddService(ServiceNode service)
        {
            if (!services.Contains(service)) services.Add(service);
        }

        public void RemoveService(ServiceNode service) => services.Remove(service);

        #endregion

        #region Lifecycle & Execution Flow

        public Node()
        {
            if (string.IsNullOrEmpty(m_guid))
            {
                m_guid = Guid.NewGuid().ToString();
            }
        }

        /// <summary>
        /// Processes the node. This is the main update tick called by the parent node or the tree itself.
        /// </summary>
        /// <returns>The current status of the node after processing.</returns>
        public Status Process()
        {
#if UNITY_EDITOR
            debugLastFrame = Time.frameCount;
#endif
            if (!_isProcessing)
            {
                OnEnter();
                _isProcessing = true;
            }

            TickServices();
            status = OnProcess();

            if (status != Status.Running)
            {
                OnExit();
                _isProcessing = false;
                ResetServices();
            }
            return status;
        }

        private void TickServices()
        {
            for (int i = 0; i < services.Count; i++)
            {
                if (services[i] != null) services[i].Process();
            }
        }

        private void ResetServices()
        {
            for (int i = 0; i < services.Count; i++)
            {
                if (services[i] != null) services[i].Reset();
            }
        }

        /// <summary>
        /// Called once when the node's Process() method is first called. Use for initialization.
        /// </summary>
        protected virtual void OnEnter() { }

        /// <summary>
        /// Called once when the node's status is no longer 'Running'. Use for cleanup.
        /// </summary>
        protected virtual void OnExit() { }

        /// <summary>
        /// Called every frame while the node is 'Running'. This is where the node's main logic goes.
        /// </summary>
        protected abstract Status OnProcess();

        #endregion

        #region Public Runtime API

        /// <summary>
        /// Checks if the node is currently in the 'Running' state.
        /// </summary>
        public bool IsRunning()
        {
            return status == Status.Running;
        }

        /// <summary>
        /// Resets the internal state of the node, calling OnExit() if it was running.
        /// Use this to prepare the node for re-execution.
        /// </summary>
        public virtual void Reset()
        {
            if (_isProcessing)
            {
                OnExit();
                _isProcessing = false;
            }
            status = Status.None;
            ResetServices();
        }

        /// <summary>
        /// Forcefully stops the execution of this node if it is currently running.
        /// It calls OnExit() for cleanup and sets the status to Failure.
        /// Composite nodes should override this to abort their running children.
        /// </summary>
        public virtual void Abort()
        {
            if (IsRunning())
            {
                OnExit();
                _isProcessing = false;
                status = Status.Failure;
            }
        }

        /// <summary>
        /// Invokes the InteruptAction delegate, allowing external systems to trigger custom interruption logic.
        /// </summary>
        public void Interrupt()
        {
            InteruptAction?.Invoke();
        }

        #endregion

        #region Child Management (For Composites/Decorators)

        public virtual void AddChild(Node child) { }
        public virtual void RemoveChild(Node child) { }
        public virtual List<Node> GetChildren() => new List<Node>();

        #endregion

        #region Cloning

        public virtual Node Clone()
        {
            Node clone = Instantiate(this);
            clone.name = this.name;
            return clone;
        }

        #endregion

        #region Context & Owner

        public void SetOwnerTree(BehaviorTree tree)
        {
            ownerTree = tree;
        }

        public BehaviorTree GetOwnerTree() => ownerTree;
        
        public GameObject GetOwnerTreeGameObject() => ownerTree?.Self.gameObject;

        #endregion
        
        #region Editor-Only Methods

        public void SetPosition(Rect newPosition) => m_position = newPosition;
        
        public void SetNewID(string newID) => m_guid = newID;

        #endregion
    }
}