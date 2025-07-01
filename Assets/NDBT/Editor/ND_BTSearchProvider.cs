// File: ND_DrawTrelloSearchProvider.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;

namespace ND_BehaviorTree.Editor
{
    public struct SearchContextElement
    {
        public object target { get; private set; }
        public string title { get; private set; }

        public SearchContextElement(object target, string title)
        {
            this.target = target;
            this.title = title;
        }
    }

    public class ND_BTSearchProvider : ScriptableObject, ISearchWindowProvider
    {
        public ND_BehaviorTreeView view; // This is assigned when the provider is created by the view
        // public VisualElement target; // 'target' was for a different context, not needed here for node placement

        // 'elements' list should not be static if each search provider instance might deal with different sets,
        // or if it's populated fresh each time CreateSearchTree is called (which it is).
        // Making it non-static or local to CreateSearchTree is safer.
        // For now, keeping it static as per your original for simplicity of this fix, but consider refactoring.
        public static List<SearchContextElement> elements;


        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            List<SearchTreeEntry> tree = new List<SearchTreeEntry>();
            tree.Add(new SearchTreeGroupEntry(new GUIContent("Nodes"), 0));

            elements = new List<SearchContextElement>(); // Initialize fresh

            // Consider caching these if assemblies/types don't change often during an editor session
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (Assembly assembly in assemblies)
            {
                try // Add try-catch for safety when iterating assembly types
                {
                    foreach (Type type in assembly.GetTypes())
                    {
                        // Ensure the type is a subclass of your base ND_DrawTrello.Node
                        if (!typeof(ND_BehaviorTree.Node).IsAssignableFrom(type) || type.IsAbstract)
                            continue;

                        var attribute = type.GetCustomAttribute<NodeInfoAttribute>();
                        if (attribute != null)
                        {
                            // NodeInfoAttribute att = (NodeInfoAttribute)attribute; // Not strictly needed to cast again
                            if (string.IsNullOrEmpty(attribute.menuItem)) continue;

                            // Create a dummy instance just to store its type and info in SearchContextElement
                            // We will create a *new* instance in OnSelectEntry
                            var nodeInstanceForSearch = Activator.CreateInstance(type);
                            elements.Add(new SearchContextElement(nodeInstanceForSearch, attribute.menuItem));
                        }
                    }
                }
                catch (ReflectionTypeLoadException ex)
                {
                    // Handle cases where an assembly can't be fully loaded, common in Unity
                    // Debug.LogWarning($"Could not load types from assembly {assembly.FullName}: {ex.Message}");
                    foreach (var loaderException in ex.LoaderExceptions)
                    {
                        // Debug.LogWarning($"LoaderException: {loaderException.Message}");
                    }
                }
                
            }

            // Sort by name (your existing sort logic seems fine)
            elements.Sort((entry1, entry2) =>
            {
                string[] splits1 = entry1.title.Split('/');
                string[] splits2 = entry2.title.Split('/');
                for (int i = 0; i < splits1.Length; i++)
                {
                    if (i >= splits2.Length) return 1;
                    int value = splits1[i].CompareTo(splits2[i]);
                    if (value != 0)
                    {
                        if (splits1.Length != splits2.Length && (i == splits1.Length - 1 || i == splits2.Length - 1))
                            return splits1.Length < splits2.Length ? 1 : -1;
                        return value;
                    }
                }
                return 0;
            });

            // Add group entries (your existing group logic seems fine)
            List<string> groups = new List<string>();
            foreach (SearchContextElement element in elements)
            {
                string[] entryTitle = element.title.Split('/');
                string groupName = "";
                for (int i = 0; i < entryTitle.Length - 1; i++)
                {
                    groupName += entryTitle[i];
                    if (!groups.Contains(groupName))
                    {
                        tree.Add(new SearchTreeGroupEntry(new GUIContent(entryTitle[i]), i + 1));
                        groups.Add(groupName);
                    }
                    groupName += "/";
                }
                SearchTreeEntry entry = new SearchTreeEntry(new GUIContent(entryTitle.Last()));
                entry.level = entryTitle.Length;
                // Store the SearchContextElement itself. The 'target' inside it is the prototype.
                entry.userData = element; 
                tree.Add(entry);
            }
            return tree;
        }

        public bool OnSelectEntry(SearchTreeEntry searchTreeEntry, SearchWindowContext context)
        {
            if (view == null || view.EditorWindow == null) // Check if view and its window are valid
            {
                Debug.LogError("SearchProvider's view or its EditorWindow is null. Cannot place node.");
                return false;
            }

            // Convert screen mouse position to local graph position
            // screenMousePosition is absolute. EditorWindow.position is the window's screen rect.
            Vector2 windowLocalMousePosition = context.screenMousePosition - view.EditorWindow.position.position; // Use view.EditorWindow
            Vector2 graphMousePosition = view.contentViewContainer.WorldToLocal(windowLocalMousePosition);

            // Retrieve the selected search element
            SearchContextElement searchElement = (SearchContextElement)searchTreeEntry.userData;

            if (searchElement.target == null || !(searchElement.target is ND_BehaviorTree.Node))
            {
                Debug.LogError("Search element target is null or not a valid Node type.");
                return false;
            }
            
            // Instantiate a NEW instance of the node data type.
            // searchElement.target was a prototype instance used for type info.
            Type nodeDataType = searchElement.target.GetType();
            Node nodeData = (ND_BehaviorTree.Node)ScriptableObject.CreateInstance(nodeDataType); 
            
            // Set its initial position (and a default size)
            // Use a default size, e.g., 150x100
            nodeData.SetPosition(new Rect(graphMousePosition, new Vector2(150, 100)));

            // Add node to the graph using the correct method in ND_DrawTrelloView
            view.AddNewNodeFromSearch(nodeData); // Use view.AddNewNodeFromSearch

            return true;
        }
    }
}