using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ND.Localization.Editor
{
    /// <summary>
    /// Gives the localization tables their own icons ("&lt;Type&gt; Icon.png" next to this file). The icon is set on the
    /// script (saved in its .meta), and assets that still show an old cached icon are reimported once, so existing
    /// tables change too.
    /// </summary>
    [InitializeOnLoad]
    public static class LocalizationIcons
    {
        private static readonly Type[] s_Types = { typeof(StringTable), typeof(AssetTable), typeof(ColorTable) };

        static LocalizationIcons() => EditorApplication.delayCall += () => ScriptIcons.Apply(s_Types);
    }

    /// <summary>Shared by the ND plugins: script icon from "&lt;Type&gt; Icon.png", then refresh stale cached asset icons.</summary>
    public static class ScriptIcons
    {
        public static void Apply(IEnumerable<Type> types)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            foreach (var type in types)
            {
                var icon = FindIcon(type.Name + " Icon");
                var script = FindScript(type);
                if (icon == null || script == null)
                {
                    Debug.LogWarning($"[ND Icons] Icon for {type.Name} not applied: icon={(icon != null ? icon.name : "missing")}, script={(script != null ? script.name : "missing")}.");
                    continue;
                }
                if (EditorGUIUtility.GetIconForObject(script) != icon) EditorGUIUtility.SetIconForObject(script, icon);

                foreach (var guid in AssetDatabase.FindAssets("t:" + type.Name))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (AssetDatabase.GetMainAssetTypeAtPath(path) != type) continue;
                    if (AssetDatabase.GetCachedIcon(path) != icon) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                }
            }
        }

        private static Texture2D FindIcon(string fileName) =>
            AssetDatabase.FindAssets($"\"{fileName}\" t:Texture2D").Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => System.IO.Path.GetFileNameWithoutExtension(p) == fileName)
                .Select(AssetDatabase.LoadAssetAtPath<Texture2D>).FirstOrDefault(t => t != null);

        private static MonoScript FindScript(Type type) =>
            AssetDatabase.FindAssets($"{type.Name} t:MonoScript").Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<MonoScript>).FirstOrDefault(s => s != null && s.GetClass() == type);
    }
}
