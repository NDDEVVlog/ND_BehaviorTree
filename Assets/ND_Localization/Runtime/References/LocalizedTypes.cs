using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ND.Localization
{
    /// <summary>Localized text. Supports <c>string.Format</c> placeholders through <see cref="Arguments"/>.</summary>
    [Serializable]
    public class LocalizedString : LocalizedReference<StringTable, string, string>
    {
        [NonSerialized] private object[] m_Arguments;

        /// <summary>Values for {0}, {1}… in the text. Assigning re-sends the text to subscribers.</summary>
        public object[] Arguments
        {
            get => m_Arguments;
            set
            {
                m_Arguments = value;
                Refresh();
            }
        }

        protected override bool TryConvert(string raw, out string result)
        {
            result = LocalizationService.Format(raw, m_Arguments);
            return true;
        }
    }

    /// <summary>Localized Unity object stored in an <see cref="AssetTable"/>, cast to <typeparamref name="TObject"/>.</summary>
    [Serializable]
    public abstract class LocalizedAsset<TObject> : LocalizedReference<AssetTable, Object, TObject> where TObject : Object
    {
        protected override bool TryConvert(Object raw, out TObject result)
        {
            result = raw as TObject;
            if (raw == null || result != null) return true;
            Debug.LogError($"[Localization] {this} holds a {raw.GetType().Name}, expected {typeof(TObject).Name}.", raw);
            return false;
        }
    }

    [Serializable] public class LocalizedSprite : LocalizedAsset<Sprite> { }
    [Serializable] public class LocalizedTexture : LocalizedAsset<Texture2D> { }
    [Serializable] public class LocalizedAudioClip : LocalizedAsset<AudioClip> { }
    [Serializable] public class LocalizedGameObject : LocalizedAsset<GameObject> { }

    [Serializable]
    public class LocalizedColor : LocalizedReference<ColorTable, Color, Color>
    {
        protected override bool TryConvert(Color raw, out Color result)
        {
            result = raw;
            return true;
        }
    }
}
