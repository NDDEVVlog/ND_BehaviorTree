// File: NodePropertyEditorWindow.cs (in an Editor folder)
using UnityEditor;
using UnityEngine;
using System.Collections.Generic; // For Dictionary

namespace ND_BehaviorTree.Editor
{
    public class NodePropertyEditorWindow : EditorWindow
    {
        private Node _targetNode;
        private SerializedObject _serializedNodeObject;
        private Vector2 _scrollPosition; // For scrollbar if content is too long

        // Static dictionary to keep track of open windows per node ID
        private static Dictionary<string, NodePropertyEditorWindow> _openWindows = new Dictionary<string, NodePropertyEditorWindow>();

        public static void Open(Node nodeToEdit)
        {
            if (nodeToEdit == null)
            {
                Debug.LogError("NodePropertyEditorWindow.Open: nodeToEdit is null.");
                return;
            }

            // Try to focus existing window for this node
            if (_openWindows.TryGetValue(nodeToEdit.id, out NodePropertyEditorWindow existingWindow) && existingWindow != null)
            {
                existingWindow.Focus();
                return;
            }

            // Create new window
            NodePropertyEditorWindow window = GetWindow<NodePropertyEditorWindow>(false, "Node Properties", true); // false = not utility, true = focus on open
            window.titleContent = new GUIContent($"{nodeToEdit.name} ({nodeToEdit.GetType().Name})");
            window.SetNode(nodeToEdit);
            window.minSize = new Vector2(300, 250); // Set a minimum size
            window.Show(); // Or ShowUtility() if you prefer

            _openWindows[nodeToEdit.id] = window; // Track it
        }

        private void SetNode(Node node)
        {
            _targetNode = node;
            if (_targetNode != null)
            {
                _serializedNodeObject = new SerializedObject(_targetNode);
            }
            else
            {
                _serializedNodeObject = null;
                Debug.LogError("NodePropertyEditorWindow.SetNode: Node is null.");
            }
        }

        private void OnEnable()
        {
            // This is called when the window is enabled/reloaded
            // Ensure serialized object is valid if target node exists
            if (_targetNode != null && (_serializedNodeObject == null || _serializedNodeObject.targetObject == null))
            {
                _serializedNodeObject = new SerializedObject(_targetNode);
            }
        }

        private void OnDestroy()
        {
            // Remove from tracking when window is closed
            if (_targetNode != null && _openWindows.ContainsKey(_targetNode.id))
            {
                if (_openWindows[_targetNode.id] == this) // Make sure we are removing the correct instance
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

            _serializedNodeObject.Update(); // Read latest data from the target Node

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition); // Start scroll view

            // --- Read-only Metadata Section ---
            EditorGUILayout.LabelField("Node Info", EditorStyles.boldLabel);
            EditorGUI.BeginDisabledGroup(true); // Read-only group
            {
                EditorGUILayout.TextField("Node Name", _targetNode.name); // 'name' is from ScriptableObject
                EditorGUILayout.TextField("Script Type", _targetNode.GetType().Name);
                EditorGUILayout.TextField("GUID", _targetNode.id);
                EditorGUILayout.RectField("Graph Position", _targetNode.position);
                if (!string.IsNullOrEmpty(_targetNode.typeName)) // Only show if populated
                {
                    EditorGUILayout.TextField("Serialized TypeName", _targetNode.typeName);
                }
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.Space(10);

            // --- Editable Properties Section ---
            EditorGUILayout.LabelField("Editable Properties", EditorStyles.boldLabel);

            SerializedProperty iterator = _serializedNodeObject.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                string propName = iterator.name;

                // Skip system fields and fields already shown as metadata
                if (propName == "m_Script" ||
                    propName == "m_guid" ||    // Backing field for id
                    propName == "m_position" || // Backing field for position
                    propName == "typeName")     // Already shown if populated
                {
                    continue;
                }
                EditorGUILayout.PropertyField(iterator, true); // Draw the property field
            }

            EditorGUILayout.EndScrollView(); // End scroll view

            if (_serializedNodeObject.ApplyModifiedProperties()) // If any value changed
            {
                EditorUtility.SetDirty(_targetNode); // Mark the Node SO as dirty to ensure it's saved
                // Optionally, if changes in this window should repaint the graph view node:
                // FindObjectOfType<ND_DrawTrelloView>()?.GetEditorNode(_targetNode.id)?.MarkDirtyRepaint();
            }
        }
    }
}