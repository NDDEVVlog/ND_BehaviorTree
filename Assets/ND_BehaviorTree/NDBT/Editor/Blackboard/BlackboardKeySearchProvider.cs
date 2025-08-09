

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace ND_BehaviorTree.Editor
{
    public class BlackboardKeySearchProvider : ScriptableObject, ISearchWindowProvider
    {
        private BlackboardView m_blackboardView;

        public void Initialize(BlackboardView blackboardView)
        {
            m_blackboardView = blackboardView;
        }

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            var tree = new List<SearchTreeEntry>();
            tree.Add(new SearchTreeGroupEntry(new GUIContent("Add New Key"), 0));

            // Find all non-abstract types that derive from the base Key class
            var keyTypes = TypeCache.GetTypesDerivedFrom<Key>()
                .Where(t => !t.IsAbstract)
                .OrderBy(t => t.Name);

            foreach (var type in keyTypes)
            {
                // Create a user-friendly name, e.g., "Vector3Key" becomes "Vector3"
                string entryName = type.Name.Replace("Key", "").Replace("_", "");
                var entry = new SearchTreeEntry(new GUIContent(entryName))
                {
                    level = 1,
                    userData = type // Store the Type itself in the entry's user data
                };
                tree.Add(entry);
            }
            return tree;
        }

        public bool OnSelectEntry(SearchTreeEntry searchTreeEntry, SearchWindowContext context)
        {
            // Get the Type we stored in userData
            var keyType = searchTreeEntry.userData as Type;
            if (keyType != null && m_blackboardView != null)
            {
                // Call the public method on BlackboardView to create the key
                m_blackboardView.AddKey(keyType);
                return true;
            }
            return false;
        }
    }
}