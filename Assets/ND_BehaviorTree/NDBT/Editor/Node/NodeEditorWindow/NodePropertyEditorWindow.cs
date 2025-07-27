// --- MODIFIED FILE: NodePropertyEditorWindow.cs ---

using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ND_BehaviorTree.Editor
{
    public class NodePropertyEditorWindow : EditorWindow
    {
        private Node _targetNode;
        private ND_NodeEditor _nodeEditorVisual;
        private SerializedObject _serializedNodeObject;
        private Vector2 _scrollPosition;

        private static Dictionary<string, NodePropertyEditorWindow> _openWindows = new Dictionary<string, NodePropertyEditorWindow>();

        public static void Open(Node nodeToEdit, ND_NodeEditor nodeEditorVisual)
        {
            if (nodeToEdit == null)
            {
                Debug.LogError("NodePropertyEditorWindow.Open: nodeToEdit is null.");
                return;
            }

            if (_openWindows.TryGetValue(nodeToEdit.id, out NodePropertyEditorWindow existingWindow) && existingWindow != null)
            {
                existingWindow.Focus();
                return;
            }

            NodePropertyEditorWindow window = GetWindow<NodePropertyEditorWindow>(false, "Node Properties", true);
            window.titleContent = new GUIContent($"{nodeToEdit.GetType().Name}");
            window.SetNode(nodeToEdit, nodeEditorVisual);
            window.minSize = new Vector2(300, 250);
            window.Show();

            _openWindows[nodeToEdit.id] = window;
        }

        private void SetNode(Node node, ND_NodeEditor nodeEditorVisual)
        {
            _targetNode = node;
            _nodeEditorVisual = nodeEditorVisual;
            if (_targetNode != null)
            {
                _serializedNodeObject = new SerializedObject(_targetNode);
            }
            else
            {
                _serializedNodeObject = null;
            }
        }

        private void OnEnable()
        {
            if (_targetNode != null && (_serializedNodeObject == null || _serializedNodeObject.targetObject == null))
            {
                _serializedNodeObject = new SerializedObject(_targetNode);
            }
        }

        private void OnDestroy()
        {
            if (_targetNode != null && _openWindows.ContainsKey(_targetNode.id))
            {
                if (_openWindows[_targetNode.id] == this)
                {
                    _openWindows.Remove(_targetNode.id);
                }
            }
        }

        private void OnGUI()
        {
            if (_targetNode == null || _serializedNodeObject == null || _serializedNodeObject.targetObject == null)
            {
                EditorGUILayout.HelpBox("No node selected or node data is invalid. Please close this window.", MessageType.Error);
                if (GUILayout.Button("Close Window")) this.Close();
                return;
            }

            _serializedNodeObject.Update();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            EditorGUILayout.LabelField("Node Settings", EditorStyles.boldLabel);
            SerializedProperty typeNameProp = _serializedNodeObject.FindProperty("typeName");
            if (typeNameProp != null)
            {
                EditorGUILayout.PropertyField(typeNameProp, new GUIContent("Display Name"));
            }
            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Node-Specific Properties", EditorStyles.boldLabel);

            SerializedProperty property = _serializedNodeObject.GetIterator();
            property.NextVisible(true);

            do
            {
                if (property.name == "m_Script" ||
                    property.name == "m_guid" ||
                    property.name == "m_position" ||
                    property.name == "typeName")
                {
                    continue;
                }
                
                var fieldInfo = _targetNode.GetType().GetField(property.name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (fieldInfo != null && typeof(Key).IsAssignableFrom(fieldInfo.FieldType))
                {
                    DrawKeySelector(property, fieldInfo); // Pass fieldInfo to the drawing method
                }
                else
                {
                    EditorGUILayout.PropertyField(property, true);
                }

            } while (property.NextVisible(false));

            EditorGUILayout.EndScrollView();

            if (_serializedNodeObject.ApplyModifiedProperties())
            {
                if (_nodeEditorVisual != null)
                {
                    _nodeEditorVisual.UpdateNode();
                }
                EditorUtility.SetDirty(_targetNode);
            }
        }
        
        /// <summary>
        /// Draws a custom dropdown for a 'Key' field, filtering by type if a BlackboardKeyTypeAttribute is present.
        /// </summary>
        private void DrawKeySelector(SerializedProperty property, FieldInfo fieldInfo)
        {
            // 1. Find the BehaviorTree and Blackboard.
            BehaviorTree tree = null;
            if (_targetNode != null)
            {
                string assetPath = AssetDatabase.GetAssetPath(_targetNode);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    tree = AssetDatabase.LoadMainAssetAtPath(assetPath) as BehaviorTree;
                }
            }

            if (tree == null || tree.blackboard == null)
            {
                EditorGUILayout.PropertyField(property, new GUIContent(property.displayName));
                EditorGUILayout.HelpBox("No Blackboard found on the parent Behavior Tree asset.", MessageType.Warning);
                return;
            }

            // 2. Check for the attribute and filter the keys.
            var keyTypeAttribute = fieldInfo.GetCustomAttribute<BlackboardKeyTypeAttribute>();
            System.Type requiredType = keyTypeAttribute?.RequiredType;

            var allKeys = tree.blackboard.keys.Where(k => k != null).ToList();
            List<Key> validKeys;

            if (requiredType != null)
            {
                // Filter keys where the key's value type matches the required type.
                validKeys = allKeys.Where(k => k.GetValueType() == requiredType).ToList();
            }
            else
            {
                // No attribute, so show all keys.
                validKeys = allKeys;
            }

            // Show a warning if no suitable keys are found.
            if (validKeys.Count == 0)
            {
                EditorGUILayout.PropertyField(property, new GUIContent(property.displayName));
                string typeName = requiredType != null ? requiredType.Name : "any";
                EditorGUILayout.HelpBox($"No keys of type '{typeName}' found in the Blackboard.", MessageType.Warning);
                return;
            }

            // 3. Prepare data for the popup.
            List<string> keyNames = validKeys.Select(k => k.keyName).ToList();
            keyNames.Insert(0, "[None]");

            // 4. Find the index of the currently selected key.
            Key currentKey = property.objectReferenceValue as Key;
            int currentIndex = 0;
            if (currentKey != null)
            {
                int foundIndex = validKeys.FindIndex(k => k == currentKey);
                if (foundIndex != -1)
                {
                    currentIndex = foundIndex + 1;
                }
            }

            // 5. Draw the popup field.
            GUIContent label = new GUIContent(property.displayName, property.tooltip);
            int newIndex = EditorGUILayout.Popup(label, currentIndex, keyNames.ToArray());

            // 6. Update the property if the selection changed.
            if (newIndex != currentIndex)
            {
                property.objectReferenceValue = (newIndex == 0) ? null : validKeys[newIndex - 1];
            }
        }
    }
}