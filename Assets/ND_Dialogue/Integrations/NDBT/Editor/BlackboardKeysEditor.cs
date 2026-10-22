using System;
using System.Collections.Generic;
using System.Linq;
using ND.Dialogue.Editor;
using ND.GraphUI;
using ND_BehaviorTree;
using ND_BehaviorTree.Editor;
using ND_BehaviorTree.Editor.Graph;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND.Dialogue.NDBT.Editor
{
    /// <summary>
    /// Edits a blackboard asset's keys in place, as dialogue variables or quests: one row per key with the key type's
    /// colour (the same as in the Behavior Tree blackboard), name, type and value; "+" adds a key of a type dialogue
    /// can use. Parent keys are listed read-only. Every change is undoable and saved with the asset.
    /// </summary>
    public static class BlackboardKeysEditor
    {
        /// <summary>Dialogue variables: every key except quest keys.</summary>
        public static VisualElement Variables(Blackboard blackboard, string questPrefix, Action changed)
        {
            var root = new VisualElement();
            if (blackboard == null)
            {
                root.Add(new HelpBox("No world blackboard: pick one in the World (Blackboard) section.", HelpBoxMessageType.Warning));
                return root;
            }
            root.Add(Caption($"Keys of '{blackboard.name}'", blackboard));
            bool IsQuest(Key k) => !string.IsNullOrEmpty(questPrefix) && k.keyName != null && k.keyName.StartsWith(questPrefix, StringComparison.Ordinal);

            var own = blackboard.keys.Where(k => k != null && !IsQuest(k)).OrderBy(k => k.keyName).ToList();
            if (own.Count == 0) root.Add(Hint("No variables yet."));
            foreach (var key in own) root.Add(KeyRow(blackboard, key, changed));

            var inherited = blackboard.AllKeys.Where(k => k != null && !blackboard.keys.Contains(k) && !IsQuest(k)).OrderBy(k => k.keyName).ToList();
            if (inherited.Count > 0)
            {
                root.Add(Hint($"From parent blackboards (edit them there):"));
                foreach (var key in inherited) root.Add(ReadOnlyRow(key));
            }

            var add = new Button { text = "+ Add variable", tooltip = "Add a key (bool, int, float, string, Vector2/3, Color, object…)" };
            add.AddToClassList("dlg-inspector__add");
            add.clicked += () =>
            {
                var window = EditorWindow.focusedWindow;
                Vector2 screen = window != null ? window.position.position + new Vector2(add.worldBound.xMin, add.worldBound.yMax) : GUIUtility.GUIToScreenPoint(Vector2.zero);
                GraphSearchPopup.Show(screen, "Add Variable", DialogueKeyEntries(), data =>
                {
                    AddKey(blackboard, (Type)data, null);
                    changed?.Invoke();
                });
            };
            root.Add(add);
            return root;
        }

        /// <summary>Quests: the int keys named <c>&lt;prefix&gt;&lt;quest&gt;</c>, shown by quest name with their starting state.</summary>
        public static VisualElement Quests(Blackboard blackboard, string questPrefix, Action changed)
        {
            var root = new VisualElement();
            if (blackboard == null)
            {
                root.Add(new HelpBox("No world blackboard: pick one in the World (Blackboard) section.", HelpBoxMessageType.Warning));
                return root;
            }
            root.Add(Caption($"Quest keys of '{blackboard.name}' (int keys named '{questPrefix}<quest>')", blackboard));
            var quests = blackboard.keys.Where(k => k is Key<int> && k.keyName != null && k.keyName.StartsWith(questPrefix, StringComparison.Ordinal))
                .OrderBy(k => k.keyName).ToList();
            if (quests.Count == 0) root.Add(Hint("No quests yet."));
            foreach (var key in quests) root.Add(QuestRow(blackboard, (Key<int>)key, questPrefix, changed));

            var addRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 4 } };
            var name = new TextField { style = { flexGrow = 1 } };
            name.textEdition.placeholder = "new quest id";
            var add = new Button { text = "+ Add quest" };
            add.clicked += () =>
            {
                string quest = name.value?.Trim();
                if (string.IsNullOrEmpty(quest)) return;
                if (blackboard.TryGetKey(questPrefix + quest, out _))
                {
                    Debug.LogWarning($"[Behavior Tree World] Quest '{quest}' already exists on '{blackboard.name}'.");
                    return;
                }
                var intKeyType = TypeCache.GetTypesDerivedFrom<Key<int>>().FirstOrDefault(t => !t.IsAbstract);
                if (intKeyType == null)
                {
                    Debug.LogError("[Behavior Tree World] No int key type (IntKey) found.");
                    return;
                }
                AddKey(blackboard, intKeyType, questPrefix + quest);
                changed?.Invoke();
            };
            addRow.Add(name);
            addRow.Add(add);
            root.Add(addRow);
            return root;
        }

        /// <summary>Key types dialogue can read/write (their value type maps to a dialogue variable type), for the "+" search.</summary>
        public static List<GraphSearchPopup.Entry> DialogueKeyEntries() =>
            TypeCache.GetTypesDerivedFrom<Key>()
                .Where(t => !t.IsAbstract && ValueTypeOf(t) is Type v && BlackboardDialogueContext.TryMapType(v, out _))
                .OrderBy(t => t.Name)
                .Select(t => new GraphSearchPopup.Entry(KeySlotScanner.FriendlyTypeName(ValueTypeOf(t)), null, t, t.Name, KeySlotScanner.KeyColor(t)))
                .ToList();

        public static Type ValueTypeOf(Type keyType)
        {
            for (var t = keyType; t != null; t = t.BaseType)
                if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Key<>)) return t.GetGenericArguments()[0];
            return null;
        }

        public static Key AddKey(Blackboard blackboard, Type keyType, string keyName)
        {
            Undo.RecordObject(blackboard, "Add Key");
            var key = (Key)ScriptableObject.CreateInstance(keyType);
            key.name = keyType.Name;
            key.keyName = keyName ?? ObjectNames.GetUniqueName(blackboard.keys.Where(k => k != null).Select(k => k.keyName).ToArray(),
                $"new_{KeySlotScanner.FriendlyTypeName(ValueTypeOf(keyType)).ToLowerInvariant()}");
            AssetDatabase.AddObjectToAsset(key, blackboard);
            Undo.RegisterCreatedObjectUndo(key, "Add Key");
            blackboard.keys.Add(key);
            EditorUtility.SetDirty(blackboard);
            AssetDatabase.SaveAssetIfDirty(blackboard);
            return key;
        }

        private static VisualElement KeyRow(Blackboard blackboard, Key key, Action changed)
        {
            var color = KeySlotScanner.KeyColor(key.GetType());
            var row = Row(color, KeySlotScanner.FriendlyTypeName(key.GetValueType()));
            var name = new TextField { value = key.keyName, isDelayed = true, tooltip = "Key name = the variable name in conditions and actions" };
            name.AddToClassList("dlg-keyrow__name");
            name.RegisterValueChangedCallback(evt =>
            {
                string renamed = evt.newValue?.Trim();
                if (string.IsNullOrEmpty(renamed) || renamed == key.keyName) return;
                if (blackboard.keys.Any(k => k != null && k != key && k.keyName == renamed))
                {
                    Debug.LogWarning($"[Behavior Tree World] '{blackboard.name}' already has a key named '{renamed}'.");
                    name.SetValueWithoutNotify(key.keyName);
                    return;
                }
                Undo.RecordObject(key, "Rename Key");
                key.keyName = renamed;
                EditorUtility.SetDirty(key);
                AssetDatabase.SaveAssetIfDirty(blackboard);
                changed?.Invoke();
            });
            row.Insert(1, name);
            row.Insert(2, ValueEditor(key));
            row.Add(DeleteButton(blackboard, key, changed));
            return row;
        }

        private static VisualElement QuestRow(Blackboard blackboard, Key<int> key, string prefix, Action changed)
        {
            var row = Row(DialogueVariableStyle.QuestColor, "quest");
            row.Insert(1, new Label(key.keyName.Substring(prefix.Length)) { style = { flexGrow = 1, unityTextAlign = TextAnchor.MiddleLeft } });
            var state = new EnumField((QuestState)Mathf.Clamp(key.GetValue(), 0, Enum.GetValues(typeof(QuestState)).Length - 1)) { tooltip = "State at the start of play", style = { width = 130 } };
            state.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(key, "Change Quest Start State");
                key.SetValue((int)(QuestState)evt.newValue);
                EditorUtility.SetDirty(key);
                AssetDatabase.SaveAssetIfDirty(blackboard);
            });
            row.Insert(2, new Label("starts") { style = { unityTextAlign = TextAnchor.MiddleLeft, color = new Color(0.6f, 0.62f, 0.68f), marginRight = 4 } });
            row.Insert(3, state);
            row.Add(DeleteButton(blackboard, key, changed));
            return row;
        }

        private static VisualElement ReadOnlyRow(Key key)
        {
            var row = Row(KeySlotScanner.KeyColor(key.GetType()), KeySlotScanner.FriendlyTypeName(key.GetValueType()));
            row.Insert(1, new Label(key.keyName) { style = { flexGrow = 1, unityTextAlign = TextAnchor.MiddleLeft, color = new Color(0.7f, 0.72f, 0.78f) } });
            row.SetEnabled(false);
            return row;
        }

        /// <summary><c>● … type</c> — callers insert the name/value between the dot and the type.</summary>
        private static VisualElement Row(Color color, string typeName)
        {
            var row = new VisualElement();
            row.AddToClassList("dlg-varrow");
            row.AddToClassList("dlg-keyrow");
            var dot = new VisualElement { style = { backgroundColor = color } };
            dot.AddToClassList("dlg-varrow__dot");
            row.Add(dot);
            var type = new Label(typeName) { style = { color = color, width = 70, unityTextAlign = TextAnchor.MiddleRight } };
            type.AddToClassList("dlg-varrow__type");
            row.Add(type);
            return row;
        }

        private static VisualElement ValueEditor(Key key)
        {
            var keyObject = new SerializedObject(key); // each key is its own ScriptableObject
            var valueProp = keyObject.FindProperty("value");
            if (valueProp == null) return new Label("(set at runtime)") { style = { flexGrow = 1 } };
            var field = new PropertyField(valueProp, string.Empty) { style = { flexGrow = 1, flexBasis = 0 } };
            field.AddToClassList("dlg-keyrow__value");
            field.Bind(keyObject);
            return field;
        }

        private static Button DeleteButton(Blackboard blackboard, Key key, Action changed)
        {
            var delete = new Button(() =>
            {
                if (!EditorUtility.DisplayDialog("Delete key", $"Delete '{key.keyName}' from '{blackboard.name}'? Behavior trees and dialogue lines using it will lose it.", "Delete", "Cancel"))
                    return;
                Undo.RecordObject(blackboard, "Delete Key");
                blackboard.keys.Remove(key);
                Undo.DestroyObjectImmediate(key);
                EditorUtility.SetDirty(blackboard);
                AssetDatabase.SaveAssetIfDirty(blackboard);
                changed?.Invoke();
            }) { text = "✕", tooltip = "Delete key" };
            delete.AddToClassList("dlg-inspector__remove");
            return delete;
        }

        private static VisualElement Caption(string text, Blackboard blackboard)
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 4 } };
            row.Add(new Label(text) { style = { flexGrow = 1, color = new Color(0.65f, 0.68f, 0.75f) } });
            row.Add(new Button(() => { Selection.activeObject = blackboard; EditorGUIUtility.PingObject(blackboard); }) { text = "Select asset" });
            return row;
        }

        private static Label Hint(string text) => new Label(text) { style = { color = new Color(0.6f, 0.62f, 0.68f), marginTop = 4, marginBottom = 2 } };
    }
}
