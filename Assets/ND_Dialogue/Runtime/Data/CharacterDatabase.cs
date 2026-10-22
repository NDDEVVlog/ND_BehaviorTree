using System;
using System.Collections.Generic;
using ND.Localization;
using UnityEngine;

namespace ND.Dialogue
{
    /// <summary>
    /// Characters shared by any number of Dialogue Databases: a character keeps the same id (and name) everywhere,
    /// while each Dialogue Database may change how it looks there (<see cref="ActorLook"/>: other portraits for an
    /// age, an outfit…).
    /// <para>A Character Database is also a template: the attributes in <see cref="templateAttributes"/> are given to
    /// every character whose <see cref="Actor.template"/> is this database. Those characters cannot remove them
    /// and follow this database when it changes; characters add their own attributes next to them.</para>
    /// <para>Ids are large random numbers so characters from different Character Databases never collide (Validate
    /// reports it if they ever do); characters kept directly in a Dialogue Database use small ids.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterDatabase", menuName = "ND Dialogue/Character Database", order = 1)]
    public class CharacterDatabase : ScriptableObject
    {
        /// <summary>Ids from a Character Database are at least this (local Dialogue Database ids stay below it).</summary>
        public const int SharedIdBase = 1 << 28;

        [SerializeReference] public List<Actor> characters = new List<Actor>();

        [Tooltip("Attributes every character using this database as its template gets (they cannot remove them; each may set its own starting value).")]
        public List<VariableDefinition> templateAttributes = new List<VariableDefinition>();

        public Actor FindCharacter(int id)
        {
            foreach (var c in characters) if (c != null && c.id == id) return c;
            return null;
        }

        public Actor FindCharacter(string characterName)
        {
            foreach (var c in characters) if (c != null && c.name == characterName) return c;
            return null;
        }

        public VariableDefinition FindTemplateAttribute(string attribute)
        {
            foreach (var a in templateAttributes) if (a != null && a.name == attribute) return a;
            return null;
        }

        public Actor AddCharacter(string characterName, bool isPlayer = false) => AddCharacter(new Actor { name = characterName, isPlayer = isPlayer });

        /// <summary>Adds a character (any Actor subclass) with a new shared id. Null (logged) when the name is empty, taken or contains '.'.</summary>
        public T AddCharacter<T>(T character) where T : Actor
        {
            if (character == null || string.IsNullOrEmpty(character.name) || FindCharacter(character.name) != null)
            {
                Debug.LogError($"[CharacterDatabase] {name}: character '{character?.name}' is unnamed or already exists.", this);
                return null;
            }
            if (character.name.IndexOf(Actor.AttributeSeparator) >= 0)
            {
                Debug.LogError($"[CharacterDatabase] {name}: character name '{character.name}' contains '{Actor.AttributeSeparator}', which separates attributes.", this);
                return null;
            }
            character.id = NewId();
            characters.Add(character);
            return character;
        }

        /// <summary>Puts an existing character (e.g. moved from a Dialogue Database) here, keeping its id.</summary>
        public bool AdoptCharacter(Actor character)
        {
            if (character == null || FindCharacter(character.id) != null || FindCharacter(character.name) != null)
            {
                Debug.LogError($"[CharacterDatabase] {name}: cannot adopt '{character?.name}' (#{character?.id}): its id or name is already used here.", this);
                return false;
            }
            characters.Add(character);
            return true;
        }

        private int NewId()
        {
            while (true)
            {
                int id = SharedIdBase | (Guid.NewGuid().GetHashCode() & (SharedIdBase - 1));
                if (FindCharacter(id) == null) return id;
            }
        }

        public List<string> Validate()
        {
            var issues = new List<string>();
            var ids = new HashSet<int>();
            var names = new HashSet<string>();
            foreach (var c in characters)
            {
                if (c == null) { issues.Add($"{name}: empty character slot."); continue; }
                if (!ids.Add(c.id)) issues.Add($"{name}: duplicate character id {c.id} ('{c.name}').");
                if (string.IsNullOrEmpty(c.name) || !names.Add(c.name)) issues.Add($"{name}: empty or duplicate character name '{c.name}'.");
            }
            var attributes = new HashSet<string>();
            foreach (var a in templateAttributes)
                if (a == null || string.IsNullOrEmpty(a.name) || !attributes.Add(a.name)) issues.Add($"{name}: empty or duplicate template attribute '{a?.name}'.");
            return issues;
        }
    }

    /// <summary>
    /// How a character looks in one Dialogue Database: emotions listed here replace the character's emotion of the
    /// same id (other emotions stay the character's own), and an optional display name replaces its name.
    /// </summary>
    [Serializable]
    public class ActorLook
    {
        public int actorId;
        [Tooltip("Optional: the name shown in this database (e.g. 'Young Draco'). Empty = the character's.")]
        public LocalizableString displayName = new LocalizableString();
        [Tooltip("Emotions that look different here (other portrait / tint). Same id as the character's emotion.")]
        public List<ActorEmotion> emotions = new List<ActorEmotion>();

        public ActorEmotion FindEmotion(string emotionId)
        {
            foreach (var e in emotions) if (e != null && e.id == emotionId) return e;
            return null;
        }
    }
}
