
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND_BehaviorTree.Editor
{
    public partial class ND_BehaviorTreeView
    {
        [Serializable]
        private class ClipboardData
        {
            public List<string> nodeJsonData = new List<string>();
            public List<string> nodeTypes = new List<string>();
            public List<EdgeData> edgeData = new List<EdgeData>();
        }

        [Serializable]
        private struct EdgeData
        {
            public string outputNodeId;
            public string inputNodeId;
        }

        private void RegisterCopyPasteCallbacks()
        {
            this.RegisterCallback<ExecuteCommandEvent>(OnExecuteCommand);
            this.RegisterCallback<ValidateCommandEvent>(OnValidateCommand);
        }

        private void OnValidateCommand(ValidateCommandEvent evt)
        {
            if (evt.commandName == "Copy" || evt.commandName == "Paste")
            {
                //Debug.Log($"[Keyboard] ValidateCommand received: {evt.commandName}");
                evt.StopPropagation(); // let ExecuteCommandEvent trigger
            }
        }
        private void OnExecuteCommand(ExecuteCommandEvent evt)
        {
            // --- DEBUG LOG FOR KEYBOARD SHORTCUTS ---
            //Debug.Log($"[Keyboard] Event received: {evt.commandName}");

            if (evt.commandName == "Copy")
            {
                //Debug.Log("CTRL + C Enter");
                CopySelectionToClipboard();
                evt.StopPropagation();
            }
            else if (evt.commandName == "Paste")
            {
                //Debug.Log("CTRL + V Enter");
                PasteFromClipboard(contentViewContainer.WorldToLocal(new Vector2(contentViewContainer.worldBound.center.x, contentViewContainer.worldBound.center.y)));
                evt.StopPropagation();
            }
        }
        
        private void CopySelectionToClipboard()
        {
            var selectedEditorNodes = selection.OfType<ND_NodeEditor>().ToList();
            if (!selectedEditorNodes.Any())
            {
                return;
            }
            
            var clipboardData = new ClipboardData();
            var selectedNodeIds = new HashSet<string>(selectedEditorNodes.Select(n => n.node.id));

            foreach (var editorNode in selectedEditorNodes)
            {
                clipboardData.nodeJsonData.Add(EditorJsonUtility.ToJson(editorNode.node));
                clipboardData.nodeTypes.Add(editorNode.node.GetType().AssemblyQualifiedName);

                List<Node> children = editorNode.node.GetChildren();
                if (children != null)
                {
                    foreach (var childNode in children)
                    {
                        if (childNode != null && selectedNodeIds.Contains(childNode.id))
                        {
                            clipboardData.edgeData.Add(new EdgeData
                            {
                                outputNodeId = editorNode.node.id,
                                inputNodeId = childNode.id
                            });
                        }
                    }
                }
            }

            string jsonForClipboard = JsonUtility.ToJson(clipboardData);
            EditorGUIUtility.systemCopyBuffer = jsonForClipboard;
        }

        /// <summary>
        /// Performs a deep copy on a node's ScriptableObject fields that are sub-assets of the tree.
        /// This prevents pasted nodes from sharing data with the original nodes.
        /// </summary>
        private void DeepCloneNodeSubAssets(Node node, BehaviorTree targetTree)
        {
            // Use reflection to find all fields on the node that are ScriptableObjects
            FieldInfo[] fields = node.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            
            foreach (var field in fields)
            {
                // We only care about fields that derive from ScriptableObject
                if (field.FieldType.IsSubclassOf(typeof(ScriptableObject)))
                {
                    // Get the value of the field (the shared ScriptableObject)
                    var sharedSubObject = field.GetValue(node) as ScriptableObject;
                    if (sharedSubObject == null) continue;

                    // IMPORTANT: We only want to clone objects that are sub-assets of our BehaviorTree.
                    // This prevents us from accidentally cloning shared assets like Materials, etc.
                    if (AssetDatabase.GetAssetPath(sharedSubObject) == AssetDatabase.GetAssetPath(targetTree))
                    {
                        // 1. Serialize the shared sub-object to JSON
                        string subObjectJson = EditorJsonUtility.ToJson(sharedSubObject);
                        
                        // 2. Create a new instance of that sub-object
                        var newSubObject = ScriptableObject.CreateInstance(sharedSubObject.GetType());
                        newSubObject.name = sharedSubObject.name;
                        Undo.RegisterCreatedObjectUndo(newSubObject, "Paste Sub-Asset");
                        
                        // 3. Overwrite the new instance with the serialized data
                        EditorJsonUtility.FromJsonOverwrite(subObjectJson, newSubObject);
                        
                        // 4. Add the new sub-object to our main BehaviorTree asset
                        AssetDatabase.AddObjectToAsset(newSubObject, targetTree);
                        
                        // 5. Update the field on our new node to point to our new sub-object
                        field.SetValue(node, newSubObject);
                    }
                }
            }
        }
        
        private void PasteFromClipboard(Vector2 pastePosition)
        {
            try
            {
                var clipboardContent = EditorGUIUtility.systemCopyBuffer;
                if (string.IsNullOrEmpty(clipboardContent)) return;

                var data = JsonUtility.FromJson<ClipboardData>(clipboardContent);
                if (data == null || data.nodeJsonData.Count == 0) return;

                ClearSelection();
                Undo.RecordObject(m_BTree, "Paste Nodes");

                var oldIdToNewNodeMap = new Dictionary<string, Node>();
                Vector2 averagePosition = Vector2.zero;
                var tempNodes = new List<Node>();

                for (int i = 0; i < data.nodeJsonData.Count; i++)
                {
                    var json = data.nodeJsonData[i];
                    var type = Type.GetType(data.nodeTypes[i]);
                    if (type == null) continue;

                    Node newNode = (Node)ScriptableObject.CreateInstance(type);
                    Undo.RegisterCreatedObjectUndo(newNode, "Paste Nodes");
                    
                    // This performs the initial shallow copy
                    EditorJsonUtility.FromJsonOverwrite(json, newNode);
                    
                    // *** THIS IS THE FIX ***
                    // Now, perform a deep copy for any ScriptableObject fields
                    DeepCloneNodeSubAssets(newNode, m_BTree);

                    // Clear children copied from the original node, as they reference old objects
                    if (newNode.GetChildren() != null)
                    {
                        foreach (var oldChild in newNode.GetChildren().ToList()) newNode.RemoveChild(oldChild);
                    }

                    string oldId = newNode.id;
                    averagePosition += newNode.position.position;
                    newNode.SetNewID(Guid.NewGuid().ToString());
                    
                    AssetDatabase.AddObjectToAsset(newNode, m_BTree);
                    m_BTree.nodes.Add(newNode);
                    oldIdToNewNodeMap.Add(oldId, newNode);
                    tempNodes.Add(newNode);
                }
                
                if (tempNodes.Any()) {
                    averagePosition /= tempNodes.Count;
                }
                var positionOffset = pastePosition - averagePosition;
                
                foreach (var newNode in tempNodes)
                {
                    Rect newRect = newNode.position;
                    newRect.position += positionOffset;
                    newNode.position = newRect;
                    
                    AddNodeVisuals(newNode, animate: true); 
                    AddToSelection(GetEditorNode(newNode.id));
                }
                
                foreach (var edgeData in data.edgeData)
                {
                    if (oldIdToNewNodeMap.TryGetValue(edgeData.outputNodeId, out Node parentNode) &&
                        oldIdToNewNodeMap.TryGetValue(edgeData.inputNodeId, out Node childNode))
                    {
                        var parentEditorNode = GetEditorNode(parentNode.id);
                        var childEditorNode = GetEditorNode(childNode.id);

                        if (parentEditorNode != null && childEditorNode != null)
                        {
                            AddEdgeToData(parentEditorNode.m_OutputPort, childEditorNode.m_InputPort);
                        }
                    }
                }
                
                EditorUtility.SetDirty(m_BTree);
                AssetDatabase.SaveAssets(); 
                if (m_editorWindow != null) m_editorWindow.SetUnsavedChanges(true);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Paste] An exception occurred during the paste operation. Error: {e.Message}\n{e.StackTrace}");
            }
        }
    }
}
