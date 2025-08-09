
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System.Reflection;
using UnityEditor.Experimental.GraphView;

namespace ND_BehaviorTree.Editor
{
    public class BlackboardView : VisualElement
    {
        private SerializedObject m_treeSerializer;
        private BehaviorTree m_BTree;
        private Color m_defaultKeyColor = Color.grey;

        // --- NEW: Add a field for our search provider ---
        private BlackboardKeySearchProvider m_keySearchProvider;

        public BlackboardView(SerializedObject treeSerializer)
        {
            this.m_treeSerializer = treeSerializer;
            this.m_BTree = treeSerializer.targetObject as BehaviorTree;

            // --- NEW: Create an instance of the provider and initialize it ---
            m_keySearchProvider = ScriptableObject.CreateInstance<BlackboardKeySearchProvider>();
            m_keySearchProvider.Initialize(this);

            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/ND_BehaviorTree/NDBT/Editor/Resources/Styles/BlackBoard/BlackboardView.uxml");
            visualTree.CloneTree(this);

            this.Q("header").AddManipulator(new Dragger { clampToParentEdges = true });

            var addKeyButton = this.Q<Button>("add-key-button");
            addKeyButton.clicked += OnAddKeyClicked;

            this.Q<Label>("title-label").text = m_BTree.name;

            PopulateView();
        }

        private void OnAddKeyClicked()
        {
            if (m_BTree.blackboard == null)
            {
                if (EditorUtility.DisplayDialog("Create Blackboard", "This Behavior Tree does not have a Blackboard asset. Would you like to create one?", "Create", "Cancel"))
                {
                    CreateAndAssignBlackboard();
                }
                return;
            }

            var screenMousePosition = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
            SearchWindow.Open(new SearchWindowContext(screenMousePosition), m_keySearchProvider);
        }
        
        private void CreateAndAssignBlackboard()
        {
            var blackboard = ScriptableObject.CreateInstance<Blackboard>();
            blackboard.name = $"{m_BTree.name}_Blackboard";
            string treePath = AssetDatabase.GetAssetPath(m_BTree);
            string directory = System.IO.Path.GetDirectoryName(treePath);
            string blackboardPath = AssetDatabase.GenerateUniqueAssetPath($"{directory}/{blackboard.name}.asset");
            AssetDatabase.CreateAsset(blackboard, blackboardPath);
            AssetDatabase.SaveAssets();
            m_treeSerializer.FindProperty("blackboard").objectReferenceValue = blackboard;
            m_treeSerializer.ApplyModifiedProperties();
            EditorUtility.SetDirty(m_BTree);
            PopulateView();
        }

        public void AddKey(Type keyType)
        {
            Undo.RecordObject(m_BTree.blackboard, "Add Blackboard Key");
            var newKey = ScriptableObject.CreateInstance(keyType) as Key;
            newKey.name = keyType.Name;
            var baseName = keyType.Name.Replace("Key_", "");
            var existingKeys = m_BTree.blackboard.keys.Select(k => k.keyName);
            newKey.keyName = ObjectNames.GetUniqueName(existingKeys.ToArray(), $"New {baseName}");
            AssetDatabase.AddObjectToAsset(newKey, m_BTree.blackboard);
            m_BTree.blackboard.keys.Add(newKey);
            EditorUtility.SetDirty(m_BTree.blackboard);
            AssetDatabase.SaveAssets();
            PopulateView();
        }

        public void PopulateView()
        {
            var foldout = this.Q<Foldout>("blackboard-foldout");
            var keysContainer = this.Q("keys-container");
            keysContainer.Clear();

            if (m_BTree.blackboard == null)
            {
                foldout.style.display = DisplayStyle.None; 
                var helpLabel = new Label("No Blackboard found. Please assign a Blackboard asset to the BehaviorTree.");
                helpLabel.style.unityTextAlign = TextAnchor.MiddleCenter; 
                helpLabel.style.paddingTop = 5;
                helpLabel.style.paddingBottom = 5;
                keysContainer.Add(helpLabel);
                return;
            }

            foldout.style.display = DisplayStyle.Flex; 
            var blackboardSerializer = new SerializedObject(m_BTree.blackboard);
            var keysProperty = blackboardSerializer.FindProperty("keys");

            for (int i = 0; i < m_BTree.blackboard.keys.Count; i++)
            {
                Key key = m_BTree.blackboard.keys[i];
                if (key == null) continue;

                var keyProperty = keysProperty.GetArrayElementAtIndex(i);
                var keyRow = new VisualElement();
                keyRow.AddToClassList("key-row");

                var colorIndicator = new VisualElement();
                colorIndicator.AddToClassList("key-color-indicator");
                var keyColorAttr = key.GetType().GetCustomAttribute<KeyColorAttribute>();
                colorIndicator.style.backgroundColor = keyColorAttr != null ? keyColorAttr.Color : m_defaultKeyColor;
                keyRow.Add(colorIndicator);
                
                var keyNameField = new TextField { value = key.keyName };
                keyNameField.AddToClassList("key-row-field-name");
                keyNameField.RegisterValueChangedCallback(evt =>
                {
                    Undo.RecordObject(key, "Rename Blackboard Key");
                    key.keyName = evt.newValue;
                    EditorUtility.SetDirty(key);
                });

                var valueProperty = keyProperty.FindPropertyRelative("value");
                var propertyField = new PropertyField(valueProperty, "");
                propertyField.AddToClassList("key-row-field-value");
                propertyField.Bind(keyProperty.serializedObject);

                var deleteButton = new Button(() => DeleteKey(key)) { text = "X" };
                deleteButton.AddToClassList("key-row-delete-button");

                keyRow.Add(keyNameField);
                keyRow.Add(propertyField);
                keyRow.Add(deleteButton);
                keysContainer.Add(keyRow);
            }
        }

        private void DeleteKey(Key keyToDelete)
        {
            Undo.RecordObject(m_BTree.blackboard, "Remove Blackboard Key");
            m_BTree.blackboard.keys.Remove(keyToDelete);
            Undo.DestroyObjectImmediate(keyToDelete);
            EditorUtility.SetDirty(m_BTree.blackboard);
            AssetDatabase.SaveAssets();
            PopulateView();
        }
    }
}