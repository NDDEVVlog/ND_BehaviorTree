// --- START OF FILE Node.cs ---

using System;
using UnityEngine;

namespace ND_BehaviorTree
{
    public abstract class Node: ScriptableObject
    {   
        public enum Status { Success, Failure, Running }

        // Runtime state. [NonSerialized] ensures it's not saved to the asset file.
        [NonSerialized] protected Status _status = Status.Failure;
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
        
        /// <summary>
        /// This is the main execution function. It's a template method that handles state transitions.
        /// </summary>
        public Status Process()
        {
            if (!_isProcessing)
            {
                OnEnter();
                _isProcessing = true;
            }

            _status = OnProcess();

            if (_status != Status.Running)
            {
                OnExit();
                _isProcessing = false;
            }
            return _status;
        }

        /// <summary>
        /// Resets the node and its children to their initial state for a new execution run.
        /// </summary>
        public virtual void Reset()
        {
            _isProcessing = false;
            _status = Status.Failure;
        }
        
        /// <summary>
        /// Clones the node. Essential for creating runtime instances from ScriptableObject assets.
        /// </summary>
        public virtual Node Clone()
        {
            Node clone = Instantiate(this);
            clone.name = this.name;
            return clone;
        }

        // Methods for derived classes to override with their specific logic.
        protected virtual void OnEnter() { }
        protected virtual void OnExit() { }
        protected abstract Status OnProcess();

        // --- Editor-only methods ---
        public void SetPosition(Rect newPosition) => m_position = newPosition;
        public void SetNewID(string newID) => m_guid = newID;
    }
}
// --- END OF FILE Node.cs ---