using System;
using System.Collections.Generic;
using System.Linq;
using ND.GraphUI;
using ND_BehaviorTree.Editor.CustomGraph;
using UnityEditor;

namespace ND_BehaviorTree.Editor
{
    /// <summary>Builds the entries for the "Add New Key" search popup.</summary>
    public static class BlackboardKeySearchProvider
    {
        public static List<GraphSearchPopup.Entry> BuildEntries()
        {
            // Create a user-friendly name, e.g., "Vector3Key" becomes "Vector3"
            return TypeCache.GetTypesDerivedFrom<Key>()
                .Where(t => !t.IsAbstract)
                .OrderBy(t => t.Name)
                .Select(t => new GraphSearchPopup.Entry(t.Name.Replace("Key", "").Replace("_", ""), null, t))
                .ToList();
        }
    }
}
