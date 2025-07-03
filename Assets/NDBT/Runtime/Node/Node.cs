// --- START OF FILE Node.cs ---

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ND_BehaviorTree
{
    public abstract class Node: ScriptableObject
    {   
        public enum Status { Success, Failure, Running }

        // Runtime state. [NonSerialized] ensures it's not saved to the asset file.
        // MODIFIED: Changed from a protected field to a public property with a protected setter.
         public Status status { get; protected set; } = Status.Failure;
        [NonSerialized] private bool _isProcessing = false;

        // Editor data
        [SerializeField] private string m_guid;
        [SerializeField] private Rect m_position;
        public string typeName;

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

            // MODIFIED: Use the 'status' property
            status = OnProcess();

            // MODIFIED: Use the 'status' property
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
            // MODIFIED: Use the 'status' property
            status = Status.Failure;
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
        /// <summary>
        /// Adds a child node. Overridden by nodes that can have children.
        /// </summary>
        public virtual void AddChild(Node child) { }

        /// <summary>
        /// Removes a child node. Overridden by nodes that can have children.
        /// </summary>
        public virtual void RemoveChild(Node child) { }

        /// <summary>
        /// Gets all child nodes. Overridden by nodes that can have children.
        /// </summary>
        public virtual List<Node> GetChildren() => new List<Node>();


        // --- Editor-only methods ---
        public void SetPosition(Rect newPosition) => m_position = newPosition;
        public void SetNewID(string newID) => m_guid = newID;
    }
}
// --- END OF FILE Node.cs ---