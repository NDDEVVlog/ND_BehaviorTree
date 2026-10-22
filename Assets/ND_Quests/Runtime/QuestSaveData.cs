using System;
using System.Collections.Generic;

namespace ND.Quests
{
    /// <summary>What a <see cref="QuestRuntime"/> saves: each quest's state and progress, and the tracked quests (JsonUtility-friendly).</summary>
    [Serializable]
    public class QuestSaveData
    {
        [Serializable]
        public struct Quest
        {
            public string name;
            public QuestState state;
            public int progress;
        }

        public List<Quest> quests = new List<Quest>();
        public List<string> tracked = new List<string>();
    }
}
