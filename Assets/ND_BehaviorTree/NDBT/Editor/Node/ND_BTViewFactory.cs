using System;
using System.Collections.Generic;
using ND.GraphUI;
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
            NodeAppearanceRegistry.Invalidate();
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
        /// Finds the mapping for the node type among the active Node Appearance Packs (see
        /// <see cref="NodeAppearanceRegistry.TryResolve(Type, out NodeAppearancePack, out NodeEditorMapping)"/>).
        /// </summary>
        private static CacheEntry ResolveConfigForType(Type nodeType)
        {
            var entry = new CacheEntry
            {
                ViewType = typeof(ND_BTNodeView),
                ThemeData = new ViewThemeData()
            };

            NodeAppearanceRegistry.TryResolve(nodeType, out var pack, out var winningMapping);

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

            entry.ThemeData.CustomStyle = pack.GetStyleSheet(winningMapping.styleSheetKey);
            entry.ThemeData.CustomUXML = pack.GetUXML(winningMapping.uxmlKey);
            if (winningMapping.overridePorts) entry.ThemeData.PortTheme = winningMapping.ports;
            if (!string.IsNullOrEmpty(winningMapping.styleSheetKey) && entry.ThemeData.CustomStyle == null)
                Debug.LogError($"[ND_BTViewFactory] Pack '{pack.name}': the mapping for {nodeType.FullName} uses stylesheet key '{winningMapping.styleSheetKey}', which the pack does not define. Using the default style.", pack);
            if (!string.IsNullOrEmpty(winningMapping.uxmlKey) && entry.ThemeData.CustomUXML == null)
                Debug.LogError($"[ND_BTViewFactory] Pack '{pack.name}': the mapping for {nodeType.FullName} uses UXML key '{winningMapping.uxmlKey}', which the pack does not define. Using the default UXML.", pack);
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
    }
}
