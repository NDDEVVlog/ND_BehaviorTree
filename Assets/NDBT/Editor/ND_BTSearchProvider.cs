// --- MODIFIED FILE: ND_BTSearchProvider.cs ---
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
    // SearchContextElement remains the same
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
        public ND_BehaviorTreeView view;
        public static List<SearchContextElement> elements;

        // --- NEW CONTEXT FIELDS ---
        // These fields will be set by the GraphView before the search window is opened.
        private Type m_filterType; // e.g., typeof(DecoratorNode) or typeof(ServiceNode)
        private CompositeNode m_parentCompositeNode; // The node to which a child will be added.

        /// <summary>
        /// Initializes the provider with a specific context for adding child nodes.
        /// </summary>
        public void Initialize(ND_BehaviorTreeView graphView, CompositeNode parent, Type filterType)
        {
            this.view = graphView;
            this.m_parentCompositeNode = parent;
            this.m_filterType = filterType;
        }

        /// <summary>
        /// Initializes the provider for general node creation.
        /// </summary>
        public void Initialize(ND_BehaviorTreeView graphView)
        {
            this.view = graphView;
            this.m_parentCompositeNode = null; // Ensure context is cleared
            this.m_filterType = null;      // Ensure context is cleared
        }

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            List<SearchTreeEntry> tree = new List<SearchTreeEntry>();
            string title = m_filterType == null ? "Nodes" : m_filterType.Name.Replace("Node", "s");
            tree.Add(new SearchTreeGroupEntry(new GUIContent(title), 0));

            elements = new List<SearchContextElement>();
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (Assembly assembly in assemblies)
            {
                try
                {
                    foreach (Type type in assembly.GetTypes())
                    {
                        if (!typeof(ND_BehaviorTree.Node).IsAssignableFrom(type) || type.IsAbstract)
                            continue;
                        
                        // --- FILTERING LOGIC ---
                        // If a filter is active, only include types that match the filter.
                        if (m_filterType != null && !m_filterType.IsAssignableFrom(type))
                            continue;
                        
                        var attribute = type.GetCustomAttribute<NodeInfoAttribute>();
                        if (attribute != null)
                        {
                            // If NOT in filter mode, skip child-only nodes. In filter mode, we WANT them.
                            if (m_filterType == null && attribute.isChildOnly) 
                                continue;

                            if (string.IsNullOrEmpty(attribute.menuItem)) 
                                continue;

                            var nodeInstanceForSearch = Activator.CreateInstance(type);
                            elements.Add(new SearchContextElement(nodeInstanceForSearch, attribute.menuItem));
                        }
                    }
                }
                catch { /* Ignore assemblies that cause errors */ }
            }

            // Sorting logic remains the same
            //elements.Sort((entry1, entry2) => { /* ... your sorting logic ... */ });

            // Tree building logic remains mostly the same
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
                entry.userData = element; 
                tree.Add(entry);
            }
            return tree;
        }

        public bool OnSelectEntry(SearchTreeEntry searchTreeEntry, SearchWindowContext context)
        {
            if (view == null) return false;
            
            SearchContextElement searchElement = (SearchContextElement)searchTreeEntry.userData;
            Type nodeDataType = searchElement.target.GetType();

            // --- CONTEXT-AWARE NODE CREATION ---
             if (m_parentCompositeNode != null && typeof(ServiceNode).IsAssignableFrom(nodeDataType))
            {
                Undo.RecordObject(view.BTree, "Add Service");
                
                ServiceNode service = (ServiceNode)ScriptableObject.CreateInstance(nodeDataType);
                service.name = nodeDataType.Name;
                AssetDatabase.AddObjectToAsset(service, view.BTree);
                view.BTree.nodes.Add(service);
                
                view.AddServiceToNode(m_parentCompositeNode, service);
                
                EditorUtility.SetDirty(view.BTree);
                AssetDatabase.SaveAssets();
            }
            else
            {
                // We are in "Normal" mode. Create a node at the mouse position.
                Vector2 windowLocalMousePosition = context.screenMousePosition - view.EditorWindow.position.position;
                Vector2 graphMousePosition = view.contentViewContainer.WorldToLocal(windowLocalMousePosition);
                
                Node nodeData = (ND_BehaviorTree.Node)ScriptableObject.CreateInstance(nodeDataType); 
                nodeData.SetPosition(new Rect(graphMousePosition, new Vector2(150, 100)));
                view.AddNewNodeFromSearch(nodeData);
            }

            return true;
        }
    }
}