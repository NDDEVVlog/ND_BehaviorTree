using System;
using System.Collections.Generic;
using System.Linq;
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
                Debug.Log($"[Keyboard] ValidateCommand received: {evt.commandName}");
                evt.StopPropagation(); // let ExecuteCommandEvent trigger
            }
        }
        private void OnExecuteCommand(ExecuteCommandEvent evt)
        {
            // --- DEBUG LOG FOR KEYBOARD SHORTCUTS ---
            Debug.Log($"[Keyboard] Event received: {evt.commandName}");

            if (evt.commandName == "Copy")
            {
                Debug.Log("CTRL + C Enter");
                CopySelectionToClipboard();
                evt.StopPropagation();
            }
            else if (evt.commandName == "Paste")
            {
                Debug.Log("CTRL + V Enter");
                PasteFromClipboard(contentViewContainer.WorldToLocal(new Vector2(contentViewContainer.worldBound.center.x, contentViewContainer.worldBound.center.y)));
                evt.StopPropagation();
            }
        }
        
        private void CopySelectionToClipboard()
        {
            var selectedEditorNodes = selection.OfType<ND_NodeEditor>().ToList();
            if (!selectedEditorNodes.Any())
            {
                Debug.LogError("[Copy] EXIT: Copy command executed, but no nodes were selected.");
                return;
            }

            Debug.Log($"[Copy] Step 1: Found {selectedEditorNodes.Count} node(s) to copy.");

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
            Debug.Log("[Copy] Step 2: Serialized data container to JSON.");

            EditorGUIUtility.systemCopyBuffer = jsonForClipboard;
            Debug.Log("[Copy] Step 3: Successfully assigned data to system clipboard.");
        }

        private void PasteFromClipboard(Vector2 pastePosition)
        {
            Debug.Log("[Paste] Paste command received and executed.");

            try
            {
                var clipboardContent = EditorGUIUtility.systemCopyBuffer;
                if (string.IsNullOrEmpty(clipboardContent))
                {
                     Debug.LogError("[Paste] EXIT: Paste was called, but clipboard is empty.");
                    return;
                }

                var data = JsonUtility.FromJson<ClipboardData>(clipboardContent);
                if (data == null || data.nodeJsonData.Count == 0)
                {
                    Debug.LogError("[Paste] EXIT: Clipboard data could not be deserialized or contained no nodes.");
                    return;
                }

                Debug.Log($"[Paste] Successfully deserialized data. Pasting {data.nodeJsonData.Count} node(s).");

                ClearSelection();
                Undo.RecordObject(m_BTree, "Paste Nodes");

                var oldIdToNewNodeMap = new Dictionary<string, Node>();
                Vector2 averagePosition = Vector2.zero;
                var tempNodes = new List<Node>();

                for (int i = 0; i < data.nodeJsonData.Count; i++)
                {
                    var json = data.nodeJsonData[i];
                    var type = Type.GetType(data.nodeTypes[i]);
                    if (type == null)
                    {
                        Debug.LogError($"[Paste] Could not find type '{data.nodeTypes[i]}'. Skipping node.");
                        continue;
                    }

                    Node newNode = (Node)ScriptableObject.CreateInstance(type);
                    EditorJsonUtility.FromJsonOverwrite(json, newNode); 
                    
                    foreach (var oldChild in newNode.GetChildren().ToList())
                    {
                        newNode.RemoveChild(oldChild);
                    }

                    string oldId = newNode.id;
                    averagePosition += newNode.position.position;
                    newNode.SetNewID(Guid.NewGuid().ToString());
                    
                    AssetDatabase.AddObjectToAsset(newNode, m_BTree);
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
                if (m_editorWindow != null) m_editorWindow.SetUnsavedChanges(true);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Paste] An exception occurred during the paste operation. Error: {e.Message}\n{e.StackTrace}");
            }
        }
    }
}