using System;
using System.Collections.Generic;
using ND.Localization;
using UnityEngine;

namespace ND.Dialogue
{
    /// <summary>
    /// One look of an actor (Neutral, Angry, Happy…). <see cref="portrait"/> / <see cref="tint"/> are the defaults;
    /// the localized fields, when assigned, replace them per language — e.g. a red tint only where red reads as joy.
    /// </summary>
    [Serializable]
    public class ActorEmotion
    {
        public string id = "Neutral";
        public Sprite portrait;
        public Color tint = Color.white;

        [Tooltip("Optional: per-locale portrait (Asset Table). Overrides Portrait when set.")]
        public LocalizedSprite localizedPortrait = new LocalizedSprite();
        [Tooltip("Optional: per-locale tint (Color Table). Overrides Tint when set.")]
        public LocalizedColor localizedTint = new LocalizedColor();

        /// <summary>The localized portrait when one is assigned, else <see cref="portrait"/>.</summary>
        public Sprite ResolvePortrait() => localizedPortrait != null && !localizedPortrait.IsEmpty ? localizedPortrait.Resolve() : portrait;

        /// <summary>The localized tint when one is assigned and resolves, else <see cref="tint"/>.</summary>
        public Color ResolveTint() => localizedTint != null && !localizedTint.IsEmpty && localizedTint.TryGetValue(out var c) ? c : tint;
    }

    /// <summary>
    /// The base setup every character shares: identity, display name, player flag, emotions, and attributes (state that
    /// changes during play: trust, mood…). Derive from it to give a kind of character its own fields
    /// (<c>class Merchant : Actor { public float priceMultiplier; }</c>): the database stores actors polymorphically and
    /// the editor's "+ Character" menu lists every subclass. Keep subclasses [Serializable] with a parameterless constructor.
    /// <para>Attributes are dialogue variables named <c>&lt;actor name&gt;.&lt;attribute&gt;</c> (e.g. <c>Guard.trust</c>);
    /// a subclass can supply them from elsewhere by overriding <see cref="AttributeDefinitions"/>.</para>
    /// </summary>
    [Serializable]
    public class Actor
    {
        public const char AttributeSeparator = '.';

        public int id;
        [Tooltip("Identifier used by importers, conditions and code. Unique in the database; no '.' (it separates attributes).")]
        public string name;
        [Tooltip("Name shown to players. Write it here; connect it to a table later to translate it.")]
        public LocalizableString displayName = new LocalizableString();
        [Tooltip("Lines of the player are offered as choices instead of being spoken automatically.")]
        public bool isPlayer;
        public List<ActorEmotion> emotions = new List<ActorEmotion>();
        [Tooltip("Emotion used by lines that do not name one.")]
        public string defaultEmotion = "Neutral";

        [Tooltip("State of this character that changes during play (trust, mood, met…). Conditions/actions use them as '<name>.<attribute>'.")]
        public List<VariableDefinition> attributes = new List<VariableDefinition>();

        /// <summary>This character's attributes with their initial values. Default: <see cref="attributes"/>.</summary>
        public virtual IEnumerable<VariableDefinition> AttributeDefinitions => attributes;

        /// <summary>Shown above the character in the editor when the subclass works differently (e.g. where attributes come from).</summary>
        public virtual string EditorNote => null;

        /// <summary>The dialogue variable name of one of this actor's attributes.</summary>
        public string AttributeVariable(string attribute) => name + AttributeSeparator + attribute;

        public VariableDefinition FindAttribute(string attribute)
        {
            foreach (var a in AttributeDefinitions)
                if (a != null && a.name == attribute) return a;
            return null;
        }

        /// <summary>
        /// The emotion with this id; an empty id means <see cref="defaultEmotion"/>. Logs and returns null when the
        /// actor has no such emotion.
        /// </summary>
        public ActorEmotion FindEmotion(string emotionId)
        {
            string wanted = string.IsNullOrEmpty(emotionId) ? defaultEmotion : emotionId;
            foreach (var emotion in emotions)
                if (emotion != null && emotion.id == wanted) return emotion;
            if (emotions.Count > 0 || !string.IsNullOrEmpty(emotionId))
                Debug.LogError($"[Dialogue] Actor '{name}' has no emotion '{wanted}'.");
            return null;
        }

        /// <summary>The display name (plain or translated), or <see cref="name"/> when none is written.</summary>
        public string ResolveDisplayName() => displayName != null && displayName.HasContent ? displayName.GetText() : name;
    }
}
