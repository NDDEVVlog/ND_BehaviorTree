// --- FULL CODE: NodePropertyEditorWindow.cs ---

using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;

namespace ND_BehaviorTree.Editor
{
    public class NodePropertyEditorWindow : EditorWindow
    {
        private Node _targetNode;
        private ND_NodeEditor _nodeEditorVisual;
        private SerializedObject _serializedNodeObject;
        private Vector2 _scrollPosition;

        private static readonly Dictionary<string, NodePropertyEditorWindow> _openWindows = new Dictionary<string, NodePropertyEditorWindow>();

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
            window.minSize = new Vector2(350, 300);
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
            DrawBuiltInProperties();
            
            EditorGUILayout.Space(15);
            
            EditorGUILayout.LabelField("Node-Specific Properties", EditorStyles.boldLabel);
            DrawNodeSpecificProperties();
            
            EditorGUILayout.Space(20);
            DrawScriptButton();

            EditorGUILayout.EndScrollView();

            if (_serializedNodeObject.ApplyModifiedProperties())
            {
                if (_nodeEditorVisual != null) _nodeEditorVisual.UpdateNode();
                EditorUtility.SetDirty(_targetNode);
            }
        }

        private void DrawBuiltInProperties()
        {
            SerializedProperty typeNameProp = _serializedNodeObject.FindProperty("typeName");
            if (typeNameProp != null)
            {
                EditorGUILayout.PropertyField(typeNameProp, new GUIContent("Display Name"));
            }

            SerializedProperty priorityProp = _serializedNodeObject.FindProperty("priority");
            if (priorityProp != null)
            {
                EditorGUILayout.PropertyField(priorityProp);
            }
        }

        private void DrawNodeSpecificProperties()
        {
            SerializedProperty property = _serializedNodeObject.GetIterator();
            property.NextVisible(true); 

            do
            {
                if (property.name == "m_Script" ||
                    property.name == "m_guid" ||
                    property.name == "m_position" ||
                    property.name == "typeName" ||
                    property.name == "priority")
                {
                    continue;
                }
                
                // This single line handles everything thanks to our custom drawers.
                EditorGUILayout.PropertyField(property, true);

            } while (property.NextVisible(false));
        }

        private void DrawScriptButton()
        {
            MonoScript script = MonoScript.FromScriptableObject(_targetNode);
            if (script != null)
            {
                EditorGUI.BeginDisabledGroup(script == null);
                if (GUILayout.Button("Open Script"))
                {
                    AssetDatabase.OpenAsset(script);
                }
                EditorGUI.EndDisabledGroup();
            }
        }
    }
}