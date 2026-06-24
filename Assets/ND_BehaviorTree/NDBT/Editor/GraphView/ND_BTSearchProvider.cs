using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace ND_BehaviorTree.Editor.CustomGraph
{
    public class ND_BTSearchProvider : ScriptableObject, ISearchWindowProvider
    {
        private ND_BTGraphCanvas m_Canvas;
        private Vector2 m_LocalPos;
        private ND_BTNodeView m_TargetComposite;
        private bool m_IsServiceSearch;

        public void InitializeNodeSearch(ND_BTGraphCanvas canvas, Vector2 localPos)
        {
            m_Canvas = canvas;
            m_LocalPos = localPos;
            m_IsServiceSearch = false;
        }

        public void InitializeServiceSearch(ND_BTGraphCanvas canvas, ND_BTNodeView targetComposite)
        {
            m_Canvas = canvas;
            m_TargetComposite = targetComposite;
            m_IsServiceSearch = true;
        }

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            var tree = new List<SearchTreeEntry> { new SearchTreeGroupEntry(new GUIContent(m_IsServiceSearch ? "Add Service" : "Add Node"), 0) };
            
            Type baseType = m_IsServiceSearch ? typeof(ServiceNode) : typeof(Node);
            var types = TypeCache.GetTypesDerivedFrom(baseType).Where(t => !t.IsAbstract && (m_IsServiceSearch || t != typeof(ServiceNode)));

            foreach (var t in types)
            {
                var attr = t.GetCustomAttributes(typeof(NodeInfoAttribute), false).FirstOrDefault() as NodeInfoAttribute;
                string path = attr?.title ?? (m_IsServiceSearch ? "Services/" : "Nodes/") + t.Name;
                
                string[] parts = path.Split('/');
                for (int i = 0; i < parts.Length - 1; i++)
                {
                    string groupName = parts[i];
                    if (!tree.Any(x => x.content.text == groupName && x.level == i + 1))
                        tree.Add(new SearchTreeGroupEntry(new GUIContent(groupName), i + 1));
                }
                
                tree.Add(new SearchTreeEntry(new GUIContent(parts.Last())) { level = parts.Length, userData = t });
            }
            return tree;
        }

        public bool OnSelectEntry(SearchTreeEntry searchTreeEntry, SearchWindowContext context)
        {
            Type type = searchTreeEntry.userData as Type;
            if (type == null) return false;

            if (m_IsServiceSearch) m_Canvas.AddServiceToNode(type, m_TargetComposite);
            else m_Canvas.AddNewNodeFromSearch(type, m_LocalPos);
            return true;
        }
    }
}