using System;
using System.Collections.Generic;
using UnityEngine;

namespace ND_BehaviorTree.Editor.CustomGraph
{
    public static class ND_BTViewFactory
    {
        private class CacheEntry
        {
            public Type ViewType;
            public ViewThemeData ThemeData;
        }

        private static Dictionary<Type, CacheEntry> s_ViewCache = new Dictionary<Type, CacheEntry>();

        public static void InvalidateCache()
        {
            s_ViewCache.Clear();
        }

        public static ND_BTNodeView CreateNodeView(Node nodeData)
        {
            Type nodeType = nodeData.GetType();
            if (nodeData is RerouteNode) return new ND_BTRerouteView(nodeData, new ViewThemeData()); // fixed look, not themed

            if (!s_ViewCache.TryGetValue(nodeType, out CacheEntry entry))
            {
                entry = ResolveConfigForType(nodeType);
                s_ViewCache[nodeType] = entry;
            }

            try
            {
                return (ND_BTNodeView)Activator.CreateInstance(entry.ViewType, nodeData, entry.ThemeData);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ND_BTViewFactory] Failed to create view '{entry.ViewType.FullName}' for {nodeType.FullName}. Using the default view. Error: {e}");
                return new ND_BTNodeView(nodeData, new ViewThemeData());
            }
        }

        /// <summary>
        /// Finds the mapping for the node type, walking up base classes (most specific wins).
        /// Mappings match by exact Type (from the MonoScript) and fall back to the stored type name
        /// (full name, or the legacy short name written by older versions).
        /// </summary>
        private static CacheEntry ResolveConfigForType(Type nodeType)
        {
            var entry = new CacheEntry
            {
                ViewType = typeof(ND_BTNodeView),
                ThemeData = new ViewThemeData()
            };

            var activeConfig = ND_BehaviorTreeSetting.Instance.activeConfig;
            if (activeConfig == null || activeConfig.mappings == null)
            {
                Debug.LogWarning("[ND_BTViewFactory] ND_BehaviorTreeSetting.activeConfig is not assigned. Nodes use the default view and theme.");
                entry.ViewType = FindAttributeView(nodeType) ?? entry.ViewType;
                return entry;
            }

            NodeEditorMapping winningMapping = null;
            for (Type currentType = nodeType; currentType != null && winningMapping == null; currentType = currentType.BaseType)
            {
                foreach (var mapping in activeConfig.mappings)
                {
                    if (Matches(mapping, currentType)) { winningMapping = mapping; break; }
                }
            }

            if (winningMapping == null || winningMapping.customViewScript == null)
            {
                var attributeView = FindAttributeView(nodeType);
                if (attributeView != null) entry.ViewType = attributeView;
            }

            if (winningMapping == null) return entry;

            if (winningMapping.customViewScript != null)
            {
                Type customViewType = winningMapping.customViewScript.GetClass();
                if (customViewType != null && typeof(ND_BTNodeView).IsAssignableFrom(customViewType))
                    entry.ViewType = customViewType;
                else
                    Debug.LogError($"[ND_BTViewFactory] Mapping for {nodeType.FullName}: customViewScript '{winningMapping.customViewScript.name}' is not a {nameof(ND_BTNodeView)} subclass. Using the default view.");
            }

            entry.ThemeData.CustomStyle = activeConfig.GetStyleSheet(winningMapping.styleSheetKey);
            entry.ThemeData.CustomUXML = activeConfig.GetUXML(winningMapping.uxmlKey);
            return entry;
        }

        /// <summary>The [CustomNodeView] view registered for the closest type in <paramref name="nodeType"/>'s hierarchy.</summary>
        private static Type FindAttributeView(Type nodeType)
        {
            var views = UnityEditor.TypeCache.GetTypesWithAttribute<CustomNodeViewAttribute>();
            for (var current = nodeType; current != null && current != typeof(Node); current = current.BaseType)
            {
                foreach (var view in views)
                {
                    if (view.IsAbstract || !typeof(ND_BTNodeView).IsAssignableFrom(view)) continue;
                    foreach (var attribute in (CustomNodeViewAttribute[])view.GetCustomAttributes(typeof(CustomNodeViewAttribute), false))
                        if (attribute.NodeType == current) return view;
                }
            }
            return null;
        }

        private static bool Matches(NodeEditorMapping mapping, Type type)
        {
            if (mapping == null) return false;
            if (mapping.nodeScript != null) return mapping.nodeScript.GetClass() == type;
            return !string.IsNullOrEmpty(mapping.nodeTypeFullName)
                && (mapping.nodeTypeFullName == type.FullName || mapping.nodeTypeFullName == type.Name);
        }
    }
}
