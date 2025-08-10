

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ND_BehaviorTree
{
    public abstract class Node : ScriptableObject
    {
        
        /// <summary>
        /// A reference to the BehaviorTree instance that owns this node.
        /// This is set at runtime when the tree is cloned. It is not serialized.
        /// </summary>
        [System.NonSerialized] public BehaviorTree ownerTree;

        /// <summary>
        /// A convenience property to get the Blackboard associated with this node's owner tree.
        /// Returns null if the node is not part of a tree at runtime.
        /// </summary>
        public Blackboard blackboard => ownerTree?.blackboard;
      

        public enum Status { Success, Failure, Running,None }

        // Runtime state. [NonSerialized] ensures it's not saved to the asset file.
        public Status status { get; protected set; } = Status.Failure;
        [NonSerialized] private bool _isProcessing = false;

        // Editor data
        [SerializeField] private string m_guid;
        [SerializeField] private Rect m_position;
        public string typeName;

        public  int priority = 0;

        public string id => m_guid;
        public Rect position { get => m_position; set => m_position = value; }

        public Node()
        {
            if (string.IsNullOrEmpty(m_guid))
            {
                m_guid = Guid.NewGuid().ToString();
            }
        }

        public Status Process()
        {
            if (!_isProcessing)
            {
                OnEnter();
                _isProcessing = true;
            }

            status = OnProcess();

            if (status != Status.Running)
            {
                OnExit();
                _isProcessing = false;
            }
            return status;
        }

        public virtual void Reset()
        {
            _isProcessing = false;
            status = Status.None;
        }

        public virtual Node Clone()
        {
            Node clone = Instantiate(this);
            clone.name = this.name;
            return clone;
        }

        protected virtual void OnEnter() { }
        protected virtual void OnExit() { }
        protected abstract Status OnProcess();

        // --- Child Management ---
        public virtual void AddChild(Node child) { }
        public virtual void RemoveChild(Node child) { }
        public virtual List<Node> GetChildren() => new List<Node>();

        // --- Editor-only methods ---

        public void SetPosition(Rect newPosition) => m_position = newPosition;
        public void SetNewID(string newID) => m_guid = newID;

        public void SetOwwnerTree(BehaviorTree tree)
        {
            ownerTree = tree;
        }
        public BehaviorTree GetOwwnerTree() =>ownerTree;
        public GameObject GetOwnerTreeGameObject() => ownerTree?.Self.gameObject;

        
        public Action InteruptAction;
    }
}