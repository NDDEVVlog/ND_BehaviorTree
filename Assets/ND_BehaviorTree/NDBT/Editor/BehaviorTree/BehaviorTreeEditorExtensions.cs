using UnityEditor;
using UnityEngine;
using System.IO;

namespace ND_BehaviorTree.Editor
{
    public static class BehaviorTreeEditorExtensions
    {
        // Base path to the templates relative to the Assets folder
        private const string TEMPLATE_BASE_PATH = "Assets/ND_BehaviorTree/NDBT/Templates/";

        // Template file names
        private const string ACTION_TEMPLATE = "81-C# Action Node Script-NewActionNode.cs.txt";
        private const string COMPOSITE_TEMPLATE = "82-C# Composite Node Script-NewCompositeNode.cs.txt";
        private const string DECORATOR_TEMPLATE = "83-C# Decorator Node Script-NewDecoratorNode.cs.txt";
        private const string SERVICE_TEMPLATE = "84-C# Service Node Script-NewServiceNode.cs.txt";
        
        [MenuItem("Assets/Create/ND_BehaviorTree/Action Node", false, 81)]
        public static void CreateActionNodeScript()
        {
            CreateScriptFromTemplate("NewActionNode.cs", ACTION_TEMPLATE);
        }

        [MenuItem("Assets/Create/ND_BehaviorTree/Composite Node", false, 82)]
        public static void CreateCompositeNodeScript()
        {
            CreateScriptFromTemplate("NewCompositeNode.cs", COMPOSITE_TEMPLATE);
        }

        [MenuItem("Assets/Create/ND_BehaviorTree/Decorator Node", false, 83)]
        public static void CreateDecoratorNodeScript()
        {
            CreateScriptFromTemplate("NewDecoratorNode.cs", DECORATOR_TEMPLATE);
        }
        
        [MenuItem("Assets/Create/ND_BehaviorTree/Service Node", false, 84)]
        public static void CreateServiceNodeScript()
        {
            CreateScriptFromTemplate("NewServiceNode.cs", SERVICE_TEMPLATE);
        }

        private static void CreateScriptFromTemplate(string defaultFileName, string templateFileName)
        {
            string templatePath = FindTemplate(templateFileName);
            if (templatePath == null)
            {
                Debug.LogError($"[BehaviorTreeEditorExtensions] Template '{templateFileName}' was not found (expected under {TEMPLATE_BASE_PATH}, or anywhere in the project if the package was moved).");
                return;
            }

            // This is a built-in Unity Editor utility that creates a new script asset from a template,
            // starting the "rename" process for the user in the Project window.
            ProjectWindowUtil.CreateScriptAssetFromTemplateFile(templatePath, defaultFileName);
        }

        /// <summary>The template's asset path: the default folder first, else searched by file name (package moved / renamed).</summary>
        private static string FindTemplate(string templateFileName)
        {
            string defaultPath = TEMPLATE_BASE_PATH + templateFileName;
            if (File.Exists(defaultPath)) return defaultPath;
            string stem = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(templateFileName)); // "...-NewActionNode"
            stem = stem.Substring(stem.LastIndexOf('-') + 1);
            foreach (var guid in AssetDatabase.FindAssets(stem))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileName(path) == templateFileName) return path;
            }
            return null;
        }
    }
}