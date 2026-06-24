using System;
using System.Collections.Generic;
using System.Linq;
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
                Debug.LogError($"Failed to create view for {nodeType.Name}. Using default. Error: {e.Message}");
                return new ND_BTNodeView(nodeData, new ViewThemeData());
            }
        }

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
                Debug.LogWarning($"[ThemeDebug] Factory: Active config is missing. Fallback to default.");
                return entry;
            }

            Type currentType = nodeType;
            NodeEditorMapping winningMapping = null;

            while (currentType != null && winningMapping == null)
            {
                // SỬA Ở ĐÂY: So sánh m.nodeTypeFullName với currentType.Name
                winningMapping = activeConfig.mappings.FirstOrDefault(m => m.nodeTypeFullName == currentType.Name);
                if (winningMapping == null) currentType = currentType.BaseType;
            }

            if (winningMapping != null)
            {
                Debug.Log($"[ThemeDebug] Factory: Found mapping for {nodeType.Name}. UXML Key: '{winningMapping.uxmlKey}', USS Key: '{winningMapping.styleSheetKey}'");

                if (winningMapping.customViewScript != null)
                {
                    Type customViewType = winningMapping.customViewScript.GetClass();
                    if (customViewType != null && typeof(ND_BTNodeView).IsAssignableFrom(customViewType))
                    {
                        entry.ViewType = customViewType;
                    }
                }

                entry.ThemeData.CustomStyle = activeConfig.GetStyleSheet(winningMapping.styleSheetKey);
                entry.ThemeData.CustomUXML = activeConfig.GetUXML(winningMapping.uxmlKey);

                Debug.Log($"[ThemeDebug] Factory: Assets extracted -> UXML Asset: {(entry.ThemeData.CustomUXML != null ? "OK" : "NULL")}, USS Asset: {(entry.ThemeData.CustomStyle != null ? "OK" : "NULL")}");
            }
            else
            {
                Debug.Log($"[ThemeDebug] Factory: NO mapping found for {nodeType.Name}. Using default theme.");
            }

            return entry;
        }
    }
}