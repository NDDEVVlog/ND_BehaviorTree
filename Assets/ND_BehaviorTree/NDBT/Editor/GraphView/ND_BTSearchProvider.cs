// --- FILE: ND_BTSearchProvider.cs (CORRECTED) ---

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
    // SearchContextElement is modified to hold a Type instead of an object instance.
    public struct SearchContextElement
    {
        public Type targetType { get; private set; }
        public string title { get; private set; }

        public SearchContextElement(Type type, string title)
        {
            this.targetType = type;
            this.title = title;
        }
    }

    public class ND_BTSearchProvider : ScriptableObject, ISearchWindowProvider
    {
        public ND_BehaviorTreeView view;
        public static List<SearchContextElement> elements;

        private Type m_filterType;
        private CompositeNode m_parentCompositeNode;

        public void Initialize(ND_BehaviorTreeView graphView, CompositeNode parent, Type filterType)
        {
            this.view = graphView;
            this.m_parentCompositeNode = parent;
            this.m_filterType = filterType;
        }

        public void Initialize(ND_BehaviorTreeView graphView)
        {
            this.view = graphView;
            this.m_parentCompositeNode = null;
            this.m_filterType = null;
        }

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            List<SearchTreeEntry> tree = new List<SearchTreeEntry>();
            string title = m_filterType == null ? "Create Node" : $"Add {m_filterType.Name.Replace("Node", "")}";
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
                        
                        if (m_filterType != null && !m_filterType.IsAssignableFrom(type))
                            continue;
                        
                        var attribute = type.GetCustomAttribute<NodeInfoAttribute>();
                        if (attribute != null)
                        {
                            if (m_filterType == null && attribute.isChildOnly) 
                                continue;

                            if (string.IsNullOrEmpty(attribute.menuItem)) 
                                continue;
                            
                            // --- FIX ---
                            // Removed the problematic instantiation: `Activator.CreateInstance(type)`
                            // We now pass the 'type' directly into the SearchContextElement.
                            elements.Add(new SearchContextElement(type, attribute.menuItem));
                        }
                    }
                }
                catch { /* Ignore assemblies that cause errors */ }
            }

            // Sorting logic remains the same.
            elements.Sort((a, b) =>
            {
                var aSplits = a.title.Split('/');
                var bSplits = b.title.Split('/');
                for (var i = 0; i < aSplits.Length; i++)
                {
                    if (i >= bSplits.Length)
                        return 1;
                    var result = string.Compare(aSplits[i], bSplits[i], StringComparison.Ordinal);
                    if (result != 0)
                    {
                        if (aSplits.Length != bSplits.Length && (i == aSplits.Length - 1 || i == bSplits.Length - 1))
                            return bSplits.Length.CompareTo(aSplits.Length);
                        return result;
                    }
                }
                return 0;
            });

            // Tree building logic remains the same.
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
            
            // --- FIX ---
            // Retrieve the type directly from our modified SearchContextElement.
            Type nodeDataType = searchElement.targetType;

             if (m_parentCompositeNode != null && typeof(ServiceNode).IsAssignableFrom(nodeDataType))
            {
                Undo.RecordObject(view.BTree, "Add Service");
                
                // Use the retrieved type to correctly create a ScriptableObject instance.
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
                Vector2 windowLocalMousePosition = context.screenMousePosition - view.EditorWindow.position.position;
                Vector2 graphMousePosition = view.contentViewContainer.WorldToLocal(windowLocalMousePosition);
                
                // Use the retrieved type to correctly create a ScriptableObject instance.
                Node nodeData = (ND_BehaviorTree.Node)ScriptableObject.CreateInstance(nodeDataType); 
                nodeData.SetPosition(new Rect(graphMousePosition, Vector2.zero));
                Debug.Log("AddNode as Position:" + graphMousePosition);
                view.AddNewNodeFromSearch(nodeData);
            }

            return true;
        }
    }
}