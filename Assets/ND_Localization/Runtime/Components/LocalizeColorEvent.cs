using System;
using UnityEngine;

namespace ND.Localization
{
    [ExecuteAlways]
    [AddComponentMenu("ND Localization/Localize Color Event")]
    public class LocalizeColorEvent : LocalizeEvent<Color>
    {
        [SerializeField] private LocalizedColor m_Color = new LocalizedColor();

        public LocalizedColor ColorReference => m_Color;

        protected override void Subscribe(Action<Color> handler) => m_Color.Changed += handler;
        protected override void Unsubscribe(Action<Color> handler) => m_Color.Changed -= handler;
    }
}
