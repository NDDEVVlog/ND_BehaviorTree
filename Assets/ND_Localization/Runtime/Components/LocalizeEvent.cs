using System;
using UnityEngine;
using UnityEngine.Events;

namespace ND.Localization
{
    /// <summary>Forwards a localized value to a UnityEvent whenever it is resolved (on enable and on every locale change).</summary>
    public abstract class LocalizeEvent<TResult> : MonoBehaviour
    {
        [SerializeField] private UnityEvent<TResult> m_OnUpdate = new UnityEvent<TResult>();

        private Action<TResult> m_Handler;

        public UnityEvent<TResult> OnUpdate => m_OnUpdate;

        protected abstract void Subscribe(Action<TResult> handler);
        protected abstract void Unsubscribe(Action<TResult> handler);

        protected virtual void OnEnable()
        {
            m_Handler ??= value => m_OnUpdate.Invoke(value);
            Subscribe(m_Handler);
        }

        protected virtual void OnDisable()
        {
            if (m_Handler != null) Unsubscribe(m_Handler);
        }
    }
}
