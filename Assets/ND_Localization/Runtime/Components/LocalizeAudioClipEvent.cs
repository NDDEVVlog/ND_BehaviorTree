using System;
using UnityEngine;

namespace ND.Localization
{
    [ExecuteAlways]
    [AddComponentMenu("ND Localization/Localize AudioClip Event")]
    public class LocalizeAudioClipEvent : LocalizeEvent<AudioClip>
    {
        [SerializeField] private LocalizedAudioClip m_AudioClip = new LocalizedAudioClip();

        public LocalizedAudioClip AudioClipReference => m_AudioClip;

        protected override void Subscribe(Action<AudioClip> handler) => m_AudioClip.Changed += handler;
        protected override void Unsubscribe(Action<AudioClip> handler) => m_AudioClip.Changed -= handler;
    }
}
