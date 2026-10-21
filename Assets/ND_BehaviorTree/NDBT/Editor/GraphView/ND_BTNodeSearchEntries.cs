using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ND.GraphUI;
using UnityEditor;
using UnityEngine;

namespace ND_BehaviorTree.Editor.CustomGraph
{
    public enum NodeSearchKind { GraphNode, Service, Decorator }

    /// <summary>
    /// Builds the entries for the node / service / decorator search popup from [NodeInfo] attributes (discovered via TypeCache).
    /// </summary>
    public static class ND_BTSearchProvider
    {
        public static List<GraphSearchPopup.Entry> BuildNodeEntries(NodeSearchKind kind)
        {
            return TypeCache.GetTypesDerivedFrom<Node>()
                .Where(type => !type.IsAbstract && !type.IsGenericType)
                .Where(type => KindOf(type) == kind)
                .Select(type => (type, attr: type.GetCustomAttribute<NodeInfoAttribute>()))
                .Where(t => kind != NodeSearchKind.GraphNode || !(t.attr?.isChildOnly ?? false))
                .Select(t => new GraphSearchPopup.Entry(
                    string.IsNullOrEmpty(t.attr?.menuItem) ? (t.attr?.title ?? t.type.Name) : t.attr.menuItem,
                    LoadIcon(t.attr),
                    t.type,
                    Graph.WeightGraph.DescriptionOf(t.type)?.Summary))
                .OrderBy(e => e.Path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static NodeSearchKind? KindOf(Type type)
        {
            if (typeof(RootNode).IsAssignableFrom(type)) return null;
            if (typeof(ServiceNode).IsAssignableFrom(type)) return NodeSearchKind.Service;
            if (typeof(DecoratorNode).IsAssignableFrom(type)) return NodeSearchKind.Decorator;
            return NodeSearchKind.GraphNode;
        }

        private static Texture2D LoadIcon(NodeInfoAttribute attr)
        {
            return attr != null && !string.IsNullOrEmpty(attr.iconPath) ? AssetDatabase.LoadAssetAtPath<Texture2D>(attr.iconPath) : null;
        }
    }
}
