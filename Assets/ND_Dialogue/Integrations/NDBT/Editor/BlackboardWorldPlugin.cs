using System.Collections.Generic;
using System.IO;
using System.Linq;
using ND.Dialogue.Editor.Plugins;
using ND_BehaviorTree;
using ND_BehaviorTree.Editor.Graph;
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
            context.AddVariableColors(KeyColor);
            // The test chat's "Reset world" starts from the blackboard assets again (drop the shared runtime copies).
            ND.Dialogue.Editor.DialogueTestSession.WorldResetting -= SharedBlackboards.Clear;
            ND.Dialogue.Editor.DialogueTestSession.WorldResetting += SharedBlackboards.Clear;
            context.AddSchemaEditor<BlackboardSchemaSource>(
                args => BlackboardKeysEditor.Variables(Source(args)?.blackboard, Source(args)?.questKeyPrefix, () => Changed(args)),
                args => BlackboardKeysEditor.Quests(Source(args)?.blackboard, Source(args)?.questKeyPrefix ?? BlackboardDialogueContext.DefaultQuestPrefix, () => Changed(args)));
        }

        private static BlackboardSchemaSource Source(DialoguePluginArgs args) => args.Database != null ? args.Database.schemaSource as BlackboardSchemaSource : null;

        /// <summary>A key edit changes what pickers offer: redraw the editor (keys live in the blackboard asset, not the database).</summary>
        private static void Changed(DialoguePluginArgs args)
        {
            if (args.Window != null) args.Window.NotifyDatabaseChanged();
        }

        /// <summary>The colour of the blackboard key behind a variable (world key, or a Blackboard Actor's key).</summary>
        public static Color? KeyColor(DialogueDatabase database, string variable)
        {
            if (database == null || string.IsNullOrEmpty(variable)) return null;
            if (database.TryResolveAttribute(variable, out var actor, out var attribute) && actor is BlackboardActor ba && ba.blackboard != null
                && ba.blackboard.TryGetKey(attribute.name, out var actorKey))
                return KeySlotScanner.KeyColor(actorKey.GetType());
            if (database.schemaSource is BlackboardSchemaSource source && source.blackboard != null && source.blackboard.TryGetKey(variable, out var key))
                return KeySlotScanner.KeyColor(key.GetType());
            return null;
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
                foreach (var actor in database.AllActors)
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
            foreach (var actor in database.AllActors)
                if (actor is BlackboardActor ba && ba.blackboard == null)
                    yield return $"Character '{actor.name}' is a Blackboard Actor without a blackboard: it has no attributes.";
        }

        private static VisualElement BuildTab(DialoguePluginArgs args)
        {
            var root = new VisualElement();
            var database = args.Database;
            var current = database.schemaSource as BlackboardSchemaSource;

            root.Add(new Label("The world state lives in a behavior-tree blackboard: its keys are the dialogue's variables (with the same colours), " +
                               $"int keys named '{current?.questKeyPrefix ?? BlackboardDialogueContext.DefaultQuestPrefix}<name>' are quests. " +
                               "Agents whose blackboard has it as parent share the values at runtime.")
                { style = { whiteSpace = WhiteSpace.Normal, color = new Color(0.63f, 0.65f, 0.7f), marginBottom = 6 } });

            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            var picker = new ObjectField("World blackboard") { objectType = typeof(Blackboard), allowSceneObjects = false, value = current != null ? current.blackboard : null, style = { flexGrow = 1 } };
            row.Add(picker);
            row.Add(new Button(() => Use(args, picker.value as Blackboard)) { text = current != null ? "Change" : "Use as world state" });
            if (current != null) row.Add(new Button(() => args.Edit("Stop Using Blackboard", () => database.schemaSource = null)) { text = "Stop using", tooltip = "Back to the database's own variables and quests" });
            root.Add(row);

            if (database.schemaSource != null && current == null)
                root.Add(new HelpBox($"The database already uses another schema source ('{database.schemaSource.name}'); this replaces it.", HelpBoxMessageType.Warning));

            if (current != null && current.blackboard != null)
            {
                root.Add(Heading("Variables"));
                root.Add(BlackboardKeysEditor.Variables(current.blackboard, current.questKeyPrefix, () => Changed(args)));
                root.Add(Heading("Quests"));
                root.Add(BlackboardKeysEditor.Quests(current.blackboard, current.questKeyPrefix, () => Changed(args)));
            }
            return root;
        }

        private static Label Heading(string text) => new Label(text) { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 13, marginTop = 10, marginBottom = 4 } };

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
                string assetPath = AssetDatabase.GetAssetPath(database);
                string folder = string.IsNullOrEmpty(assetPath) ? null : Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
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
