using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace ND_BehaviorTree.Editor.CustomGraph
{
    public class ND_BTSearchProvider : ScriptableObject, ISearchWindowProvider
    {
        private ND_BTGraphCanvas m_Canvas;
        private Vector2 m_LocalMousePosition;
        private ND_BTNodeView m_TargetNode;
        private bool m_IsServiceSearch;

        public void InitializeNodeSearch(ND_BTGraphCanvas canvas, Vector2 localMousePos)
        {
            m_Canvas = canvas;
            m_LocalMousePosition = localMousePos;
            m_IsServiceSearch = false;
            m_TargetNode = null;
        }

        public void InitializeServiceSearch(ND_BTGraphCanvas canvas, ND_BTNodeView targetNode)
        {
            m_Canvas = canvas;
            m_TargetNode = targetNode;
            m_IsServiceSearch = true;
        }

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            var tree = new List<SearchTreeEntry>
            {
                new SearchTreeGroupEntry(new GUIContent(m_IsServiceSearch ? "Add Service" : "Create Node"), 0)
            };

            var nodeTypes = GetFilteredNodeTypes();
            PopulateSearchTree(tree, nodeTypes);

            return tree;
        }

        private List<Type> GetFilteredNodeTypes()
        {
            return TypeCache.GetTypesDerivedFrom<Node>()
                .Where(type => !type.IsAbstract && !type.IsGenericType)
                .Where(type => 
                {
                    var attr = type.GetCustomAttribute<NodeInfoAttribute>();
                    bool isChildOnly = attr != null && attr.isChildOnly;
                    
                    return m_IsServiceSearch ? isChildOnly : !isChildOnly;
                })
                .ToList();
        }

        private void PopulateSearchTree(List<SearchTreeEntry> tree, List<Type> types)
        {
            var groups = new List<string>();

            var sortedTypes = types.Select(t => new 
            { 
                Type = t, 
                Attr = t.GetCustomAttribute<NodeInfoAttribute>() 
            })
            .OrderBy(x => x.Attr?.menuItem ?? x.Type.Name)
            .ToList();

            foreach (var item in sortedTypes)
            {
                string menuItem = item.Attr?.menuItem;
                string title = item.Attr?.title ?? item.Type.Name;
                Texture2D icon = null;

                if (item.Attr != null && !string.IsNullOrEmpty(item.Attr.iconPath))
                {
                    icon = AssetDatabase.LoadAssetAtPath<Texture2D>(item.Attr.iconPath);
                }

                string[] paths = string.IsNullOrEmpty(menuItem) ? new[] { title } : menuItem.Split('/');
                
                for (int i = 0; i < paths.Length - 1; i++)
                {
                    string groupName = paths[i];
                    if (i >= groups.Count)
                    {
                        groups.Add(groupName);
                        tree.Add(new SearchTreeGroupEntry(new GUIContent(groupName), i + 1));
                    }
                    else if (groups[i] != groupName)
                    {
                        groups.RemoveRange(i, groups.Count - i);
                        groups.Add(groupName);
                        tree.Add(new SearchTreeGroupEntry(new GUIContent(groupName), i + 1));
                    }
                }

                var entry = new SearchTreeEntry(new GUIContent(paths.Last(), icon))
                {
                    level = paths.Length,
                    userData = item.Type
                };
                
                tree.Add(entry);
            }
        }

        public bool OnSelectEntry(SearchTreeEntry searchTreeEntry, SearchWindowContext context)
        {
            var selectedType = (Type)searchTreeEntry.userData;

            if (m_IsServiceSearch && m_TargetNode != null)
            {
                m_Canvas.AddServiceToNode(selectedType, m_TargetNode);
            }
            else
            {
                m_Canvas.AddNewNodeFromSearch(selectedType, m_LocalMousePosition);
            }

            return true;
        }
    }
}