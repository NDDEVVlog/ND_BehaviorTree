using System.Collections.Generic;
using System.IO;
using System.Linq;
using ND.Dialogue.Editor.Plugins;
using ND_BehaviorTree;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND.Dialogue.NDBT.Editor
{
    /// <summary>
    /// Dialogue editor plugin: a "World (Blackboard)" sidebar tab to make a behavior-tree blackboard the dialogue's
    /// world state in one click, and a validation rule for a misconfigured source.
    /// </summary>
    public class BlackboardWorldPlugin : DialoguePlugin
    {
        public const string TabTitle = "World (Blackboard)";

        public override string DisplayName => "Behavior Tree World";
        public override string Description => "Use an ND_BehaviorTree blackboard as the dialogue's variables and quests.";

        public override void Register(DialoguePluginContext context)
        {
            context.AddDatabaseTab(TabTitle, BuildTab);
            context.AddValidationRule("blackboard-source", Check);
        }

        private static IEnumerable<string> Check(DialogueDatabase database)
        {
            if (database.schemaSource is BlackboardSchemaSource source)
            {
                if (source.blackboard == null)
                {
                    yield return $"Schema Source '{source.name}' has no blackboard: no variable or quest exists.";
                    yield break;
                }
                // With a blackboard world, a plain character's attributes are world keys named "Actor.attribute".
                foreach (var actor in database.actors)
                {
                    if (actor == null || actor is BlackboardActor) continue;
                    foreach (var attribute in actor.AttributeDefinitions)
                    {
                        if (attribute == null || string.IsNullOrEmpty(attribute.name)) continue;
                        string keyName = actor.AttributeVariable(attribute.name);
                        if (!source.blackboard.TryGetKey(keyName, out _))
                            yield return $"Character '{actor.name}': attribute '{attribute.name}' needs the key '{keyName}' on '{source.blackboard.name}' (or make it a Blackboard Actor).";
                    }
                }
            }
            foreach (var actor in database.actors)
                if (actor is BlackboardActor ba && ba.blackboard == null)
                    yield return $"Character '{actor.name}' is a Blackboard Actor without a blackboard: it has no attributes.";
        }

        private static VisualElement BuildTab(DialoguePluginArgs args)
        {
            var root = new VisualElement();
            var database = args.Database;
            var current = database.schemaSource as BlackboardSchemaSource;

            root.Add(new HelpBox("Variables are blackboard keys (bool / int / float / string / Vector2 / Vector3 / Color / object, parents included). " +
                                 "A quest is an int key named 'quest.<name>'. Agents whose blackboard has this one as parent share the values at runtime.",
                HelpBoxMessageType.None));

            var picker = new ObjectField("World blackboard") { objectType = typeof(Blackboard), allowSceneObjects = false, value = current != null ? current.blackboard : null };
            root.Add(picker);
            var apply = new Button(() => Use(args, picker.value as Blackboard)) { text = current != null ? "Change world blackboard" : "Use as world state" };
            root.Add(apply);

            if (database.schemaSource != null && current == null)
                root.Add(new HelpBox($"The database already uses another schema source ('{database.schemaSource.name}'); this replaces it.", HelpBoxMessageType.Warning));

            if (current != null)
            {
                root.Add(new Button(() => args.Edit("Stop Using Blackboard", () => database.schemaSource = null)) { text = "Stop using the blackboard" });
                if (current.blackboard != null) root.Add(KeyList(current));
            }
            return root;
        }

        private static VisualElement KeyList(BlackboardSchemaSource source)
        {
            var box = new VisualElement { style = { marginTop = 8 } };
            var schema = (BlackboardDialogueContext)source.Schema;
            box.Add(new Label("Variables") { style = { unityFontStyleAndWeight = FontStyle.Bold } });
            foreach (var name in schema.VariableNames.OrderBy(n => n))
            {
                schema.TryGetVariableType(name, out var type);
                box.Add(new Label($"•  {name}   ({type})"));
            }
            box.Add(new Label("Quests") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 4 } });
            var quests = schema.QuestNames.OrderBy(n => n).ToList();
            if (quests.Count == 0) box.Add(new Label($"(none — add int keys named '{source.questKeyPrefix}<name>')"));
            foreach (var name in quests) box.Add(new Label($"•  {name}"));
            return box;
        }

        /// <summary>Points the database at <paramref name="blackboard"/>, creating the source asset next to the database if needed.</summary>
        public static void Use(DialoguePluginArgs args, Blackboard blackboard)
        {
            if (blackboard == null)
            {
                Debug.LogWarning("[Behavior Tree World] Pick a blackboard first.");
                return;
            }
            var database = args.Database;
            var source = database.schemaSource as BlackboardSchemaSource;
            if (source == null)
            {
                source = ScriptableObject.CreateInstance<BlackboardSchemaSource>();
                source.blackboard = blackboard;
                string folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(database))?.Replace('\\', '/');
                if (!string.IsNullOrEmpty(folder))
                    AssetDatabase.CreateAsset(source, AssetDatabase.GenerateUniqueAssetPath($"{folder}/{database.name}_World.asset"));
                args.Edit("Use Blackboard As World", () => database.schemaSource = source);
                return;
            }
            Undo.RecordObject(source, "Change World Blackboard");
            source.blackboard = blackboard;
            EditorUtility.SetDirty(source);
            args.Edit("Change World Blackboard", () => { });
        }
    }
}
