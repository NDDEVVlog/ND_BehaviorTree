using System;
using System.Collections.Generic;
using UnityEngine;

namespace ND_BehaviorTree.Editor
{
    /// <summary>
    /// Manages the <see cref="NodeAppearancePack"/>s the graph uses: packs found in the project, packs loaded here,
    /// and packs registered by plugins. Any of them can be switched off here.
    /// </summary>
    [CreateAssetMenu(fileName = "NodeEditorConfig", menuName = "ND_BehaviorTree/Node Editor Config")]
    public class NodeEditorConfig : ScriptableObject
    {
        [Serializable]
        public class PackEntry
        {
            public NodeAppearancePack pack;
            public bool enabled = true;
        }

        [Tooltip("Use every Node Appearance Pack in the project. Off: only the packs listed below and the packs plugins register.")]
        public bool autoDiscoverPacks = true;

        [Tooltip("Packs loaded explicitly, and the on/off state of any pack (found in the project or registered by a plugin).")]
        public List<PackEntry> packs = new List<PackEntry>();

        public bool IsEnabled(NodeAppearancePack pack)
        {
            foreach (var entry in packs)
            {
                if (entry.pack == pack) return entry.enabled;
            }
            return true;
        }

        public void SetEnabled(NodeAppearancePack pack, bool enabled)
        {
            foreach (var entry in packs)
            {
                if (entry.pack != pack) continue;
                entry.enabled = enabled;
                return;
            }
            packs.Add(new PackEntry { pack = pack, enabled = enabled });
        }
    }
}
