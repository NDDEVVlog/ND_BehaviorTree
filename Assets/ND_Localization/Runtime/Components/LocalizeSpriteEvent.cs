using System;
using UnityEngine;

namespace ND.Localization
{
    [ExecuteAlways]
    [AddComponentMenu("ND Localization/Localize Sprite Event")]
    public class LocalizeSpriteEvent : LocalizeEvent<Sprite>
    {
        [SerializeField] private LocalizedSprite m_Sprite = new LocalizedSprite();

        public LocalizedSprite SpriteReference => m_Sprite;

        protected override void Subscribe(Action<Sprite> handler) => m_Sprite.Changed += handler;
        protected override void Unsubscribe(Action<Sprite> handler) => m_Sprite.Changed -= handler;
    }
}
