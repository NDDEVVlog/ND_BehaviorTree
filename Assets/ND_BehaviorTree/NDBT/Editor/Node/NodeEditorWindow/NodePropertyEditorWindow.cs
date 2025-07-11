
using UnityEditor;
using UnityEngine;
using System.Collections.Generic; 
using System.Linq; 

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
            // Use the more specific node type for a better window title
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
                // Create the SerializedObject from the target node instance.
                // This correctly captures the entire object, including derived class fields.
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

            // Always update from the source object at the beginning of OnGUI
            _serializedNodeObject.Update();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            // A field for the custom name/title of the node
            EditorGUILayout.LabelField("Node Settings", EditorStyles.boldLabel);
            SerializedProperty typeNameProp = _serializedNodeObject.FindProperty("typeName");
            if(typeNameProp != null)
            {
                EditorGUILayout.PropertyField(typeNameProp, new GUIContent("Display Name"));
            }
            EditorGUILayout.Space(10);


            // --- THE CORE FIX: Iterate and draw all visible properties ---
            EditorGUILayout.LabelField("Node-Specific Properties", EditorStyles.boldLabel);

            // Get an iterator for the serialized object
            SerializedProperty property = _serializedNodeObject.GetIterator();
            // The first call to NextVisible moves to the first property (usually "m_Script")
            property.NextVisible(true); 

            // Loop through all visible properties
            do
            {
                // These are the built-in fields from the base Node class that we don't want to show here.
                if (property.name == "m_Script" || 
                    property.name == "m_guid" || 
                    property.name == "m_position" || 
                    property.name == "typeName")
                {
                    continue; // Skip these fields
                }

                // Draw the property field for all other properties.
                // This will automatically find 'interval', 'rotationSpeed', 'agentKey', etc.
                EditorGUILayout.PropertyField(property, true);

            } while (property.NextVisible(false)); // Move to the next property

            EditorGUILayout.EndScrollView();

            // Apply any changes made in the GUI back to the serialized object
            if (_serializedNodeObject.ApplyModifiedProperties())
            {
                // If anything changed, update the visual node in the graph
                if (_nodeEditorVisual != null)
                {
                    _nodeEditorVisual.UpdateNode();
                }
                
                // Mark the node asset as dirty so Unity saves the changes
                EditorUtility.SetDirty(_targetNode);
            }
        }
    }
}