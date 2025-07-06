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
            // --- CRITICAL FIX ---
            // Reset the context to null. This prevents the filter from the 
            // "Add Service" action from leaking into the general "Create Node" action.
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

                            var nodeInstanceForSearch = Activator.CreateInstance(type);
                            elements.Add(new SearchContextElement(nodeInstanceForSearch, attribute.menuItem));
                        }
                    }
                }
                catch { /* Ignore assemblies that cause errors */ }
            }

            // Sorting logic from original
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

            // Tree building logic
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