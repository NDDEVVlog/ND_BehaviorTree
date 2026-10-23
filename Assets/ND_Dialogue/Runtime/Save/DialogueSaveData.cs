using System;
using System.Collections.Generic;
using ND.Quests;

namespace ND.Dialogue
{
    /// <summary>Where a runner was: which line of which entry, or waiting for the player's choice after it.</summary>
    [Serializable]
    public class DialoguePosition
    {
        public bool active;
        public int conversationId;
        public int entryId;
        public int lineIndex;
        public bool waitingForChoice;
    }

    /// <summary>
    /// Everything dialogue needs to continue later, as plain data (JsonUtility friendly): variables and character
    /// attributes (as text, typed), quest states, the entries already seen, and the running conversation's position.
    /// Object variables are not saved (an asset reference cannot live in a save file).
    /// </summary>
    [Serializable]
    public class DialogueSaveData
    {
        public const int CurrentVersion = 1;

        [Serializable] public struct Variable { public string name; public VariableType type; public string value; }
        [Serializable] public struct Quest { public string name; public QuestState state; public int progress; }
        [Serializable] public struct Seen { public int conversation; public int entry; }
        /// <summary>Route state of an entry that picks by Cycle (visits) or Shuffle (targets left, as (conversation, entry) keys).</summary>
        [Serializable] public struct Route { public int conversation; public int entry; public int cycle; public List<long> bag; }

        public int version = CurrentVersion;
        /// <summary>Name of the database the save was made with (a check, not a lookup).</summary>
        public string database;
        /// <summary>UTC time of the save, ISO 8601.</summary>
        public string savedAt;
        public List<Variable> variables = new List<Variable>();
        public List<Quest> quests = new List<Quest>();
        public List<Seen> seen = new List<Seen>();
        public List<Route> routes = new List<Route>();
        /// <summary>Quests shown in the on-screen tracker.</summary>
        public List<string> trackedQuests = new List<string>();
        public DialoguePosition position = new DialoguePosition();
    }
}
