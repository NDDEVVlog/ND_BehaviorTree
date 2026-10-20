using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using ND_BehaviorTree.Editor.CustomGraph;

namespace ND_BehaviorTree.Editor
{
    public class NodePropertyEditorWindow : EditorWindow
    {
        private Node _targetNode;
        private ND_BTNodeView _nodeEditorVisual;
        private SerializedObject _serializedNodeObject;
        private Vector2 _scrollPosition;

        private static readonly Dictionary<string, NodePropertyEditorWindow> _openWindows = new Dictionary<string, NodePropertyEditorWindow>();

        public static void Open(Node nodeToEdit, ND_BTNodeView nodeEditorVisual)
        {
            if (nodeToEdit == null) return;

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

        private void SetNode(Node node, ND_BTNodeView nodeEditorVisual)
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

            DrawDescription();

            EditorGUILayout.LabelField("Node Settings", EditorStyles.boldLabel);
            DrawBuiltInProperties();
            
            EditorGUILayout.Space(15);
            
            EditorGUILayout.LabelField("Node-Specific Properties", EditorStyles.boldLabel);
            DrawNodeSpecificProperties();
            
            EditorGUILayout.Space(20);
            DrawScriptButton();

            EditorGUILayout.EndScrollView();

            if (_serializedNodeObject.hasModifiedProperties)
            {
                // Recorded in the tree's graph history (Ctrl+Z in the Behavior Tree window); consecutive edits of
                // this node within a second merge into one step (e.g. dragging a value).
                var tree = FindOwnerTree(_targetNode);
                if (tree != null)
                {
                    using (new Graph.GraphUndoScope(tree, $"Edit {_targetNode.name}", $"props:{_targetNode.GetInstanceID()}"))
                        _serializedNodeObject.ApplyModifiedPropertiesWithoutUndo();
                    ND_BehaviorTreeEditorWindow.NotifyTreeEdited(tree);
                }
                else
                {
                    _serializedNodeObject.ApplyModifiedProperties();
                }
                EditorUtility.SetDirty(_targetNode);
            }
        }

        /// <summary>The BehaviorTree asset that contains this node (nodes and attachments are sub-assets of their tree).</summary>
        internal static BehaviorTree FindOwnerTree(Node node)
        {
            if (node == null || !EditorUtility.IsPersistent(node)) return null;
            return AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GetAssetPath(node)) as BehaviorTree;
        }

        private void DrawBuiltInProperties()
        {
            SerializedProperty typeNameProp = _serializedNodeObject.FindProperty("typeName");
            if (typeNameProp != null)
            {
                EditorGUILayout.PropertyField(typeNameProp, new GUIContent("Display Name"));
            }

            DrawWeight();
        }

        private static GUIStyle s_DetailsStyle;

        /// <summary>[NodeDescription]: the summary (also the graph tooltip) and the detailed explanation.</summary>
        private void DrawDescription()
        {
            var description = Graph.WeightGraph.DescriptionOf(_targetNode.GetType());
            if (description == null) return;

            s_DetailsStyle ??= new GUIStyle(EditorStyles.wordWrappedLabel) { richText = true };
            EditorGUILayout.LabelField("What it does", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(description.Summary, MessageType.None);
            if (!string.IsNullOrEmpty(description.Details))
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    EditorGUILayout.LabelField(description.Details, s_DetailsStyle);
            }
            EditorGUILayout.Space(10);
        }

        /// <summary>Weight is shown only when the parent picks children by weight; elsewhere it would do nothing.</summary>
        private void DrawWeight()
        {
            SerializedProperty weightProp = _serializedNodeObject.FindProperty("weight");
            if (weightProp == null) return;

            var tree = FindOwnerTree(_targetNode);
            var parent = Graph.WeightGraph.FindLogicalParent(tree, _targetNode, out _);
            if (parent is IWeightedComposite weighted)
            {
                string usage = weighted.WeightUsage == WeightUsage.Chance ? "chance to be picked" : "utility score";
                EditorGUILayout.PropertyField(weightProp, new GUIContent("Weight", $"Base {usage} in '{Graph.WeightGraph.TitleOf(parent)}'. Attach 'Weight From Key' to drive it from the blackboard."));
                string modifiers = Graph.WeightGraph.ModifierText(_targetNode);
                if (modifiers != null) EditorGUILayout.LabelField(" ", $"Runtime: weight {modifiers}", EditorStyles.miniLabel);
            }
            else if (!Graph.WeightGraph.IsUnsetWeight(weightProp.floatValue))
            {
                EditorGUILayout.HelpBox($"Weight {weightProp.floatValue:0.##} is ignored: the parent ('{Graph.WeightGraph.TitleOf(parent)}') runs its children left to right. " +
                                        "Only Random Rate Selector and Utility Selector use weights.", MessageType.Warning);
                if (GUILayout.Button("Reset Weight to 1")) weightProp.floatValue = 1f;
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
                    property.name == "weight")
                {
                    continue;
                }
                
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