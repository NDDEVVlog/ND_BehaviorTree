using System;
using UnityEngine;

namespace ND.Localization
{
    [ExecuteAlways]
    [AddComponentMenu("ND Localization/Localize String Event")]
    public class LocalizeStringEvent : LocalizeEvent<string>
    {
        [SerializeField] private LocalizedString m_String = new LocalizedString();

        public LocalizedString StringReference => m_String;

        protected override void Subscribe(Action<string> handler) => m_String.Changed += handler;
        protected override void Unsubscribe(Action<string> handler) => m_String.Changed -= handler;
    }
}
