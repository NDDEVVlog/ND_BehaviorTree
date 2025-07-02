// File: Assets/NDBT/Editor/Windows/TrelloChildNodeEditorWindow.cs
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
// using System.CodeDom; // Removed as likely unused

namespace ND_BehaviorTree.Editor
{
    public class TrelloChildNodeEditorWindow : EditorWindow
    {
        private TrelloChildNode _targetNode;
        private ND_NodeEditor visualNode;
        private SerializedObject _serializedNodeObject;
        private Vector2 _scrollPosition;

        private static Dictionary<string, TrelloChildNodeEditorWindow> _openWindows = new Dictionary<string, TrelloChildNodeEditorWindow>();

        public static void Open(TrelloChildNode nodeToEdit, ND_NodeEditor nD_Node)
        {
            if (nodeToEdit == null)
            {
                Debug.LogError("TrelloChildNodeEditorWindow.Open: nodeToEdit is null.");
                return;
            }

            if (_openWindows.TryGetValue(nodeToEdit.id, out TrelloChildNodeEditorWindow existingWindow) && existingWindow != null)
            {
                existingWindow.Focus();
                return;
            }

            TrelloChildNodeEditorWindow window = GetWindow<TrelloChildNodeEditorWindow>(false, "Card Details", true);
            window.SetNode(nodeToEdit, nD_Node);
            window.minSize = new Vector2(380, 550);
            window.Show();
            _openWindows[nodeToEdit.id] = window;
        }

        private void SetNode(TrelloChildNode node, ND_NodeEditor nD_Node)
        {
            visualNode = nD_Node;
            _targetNode = node;
            if (_targetNode != null)
            {
                _serializedNodeObject = new SerializedObject(_targetNode);
                UpdateWindowTitle();
            }
            else
            {
                _serializedNodeObject = null;
            }
        }

        private void UpdateWindowTitle()
        {
            if (_targetNode != null)
            {
                this.titleContent = new GUIContent(string.IsNullOrEmpty(_targetNode.task) ? "Trello Card Details" : _targetNode.task);
            }
        }

        private void OnEnable()
        {
            if (_targetNode != null && (_serializedNodeObject == null || _serializedNodeObject.targetObject == null))
            {
                _serializedNodeObject = new SerializedObject(_targetNode);
            }
            UpdateWindowTitle();
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
                EditorGUILayout.HelpBox("No Trello card selected or its data is invalid. Please close this window.", MessageType.Error);
                if (GUILayout.Button("Close Window")) this.Close();
                return;
            }

            _serializedNodeObject.Update();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.ExpandHeight(true));

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(_serializedNodeObject.FindProperty("isComplete"), GUIContent.none, GUILayout.Width(20));
            EditorGUILayout.PropertyField(_serializedNodeObject.FindProperty("task"), GUIContent.none, GUILayout.ExpandWidth(true));
            EditorGUILayout.EndHorizontal();

            if (EditorGUI.EndChangeCheck())
            {
                if (_targetNode.name != _targetNode.task && !string.IsNullOrEmpty(_targetNode.task))
                {
                    _targetNode.name = _targetNode.task;
                }
                UpdateWindowTitle();
            }
            EditorGUILayout.Space(5);
            if (visualNode != null)
            {
                //visualNode.UpdateNode();
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent(" Add", GetIcon("Toolbar Plus")), EditorStyles.miniButtonLeft, GUILayout.Height(22))) { /* TODO */ }
            if (GUILayout.Button(new GUIContent(" Labels", GetIcon("FilterByLabel")), EditorStyles.miniButtonMid, GUILayout.Height(22))) { /* TODO */ }
            if (GUILayout.Button(new GUIContent(" Dates", GetIcon("UnityEditor.Timeline.TimelineWindow") ?? GetIcon("ClockBundle")), EditorStyles.miniButtonMid, GUILayout.Height(22))) { /* TODO */ }
            if (GUILayout.Button(new GUIContent(" Checklist", GetIcon("Toggle") ?? GetIcon("FilterByType")), EditorStyles.miniButtonMid, GUILayout.Height(22))) { /* TODO: Add new checklist group */ }
            if (GUILayout.Button(new GUIContent(" Members", GetIcon("UserIcon") ?? GetIcon("UsersIcon")), EditorStyles.miniButtonRight, GUILayout.Height(22))) { /* TODO */ }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Description", EditorStyles.boldLabel);
            SerializedProperty descriptionProp = _serializedNodeObject.FindProperty("Description");
            GUIStyle descriptionAreaStyle = new GUIStyle(EditorStyles.textArea) { wordWrap = true };
            descriptionProp.stringValue = EditorGUILayout.TextArea(descriptionProp.stringValue, descriptionAreaStyle, GUILayout.MinHeight(80), GUILayout.ExpandHeight(false));
            EditorGUILayout.Space(10);

            DrawChecklistsGUI();
            EditorGUILayout.Space(10);

            DrawScriptRefsGUI();
            EditorGUILayout.Space(10);

            EditorGUILayout.EndScrollView();

            if (_serializedNodeObject.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(_targetNode);
            }
        }

        private void DrawChecklistsGUI()
        {
            SerializedProperty checkBoxesListProp = _serializedNodeObject.FindProperty("checkBoxes");
            if (checkBoxesListProp == null || !checkBoxesListProp.isArray)
            {
                EditorGUILayout.HelpBox("Checkboxes data not found.", MessageType.Warning);
                return;
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Checklist", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            float progress = checkBoxesListProp.arraySize > 0 ? (float)GetCheckedItemCount(checkBoxesListProp) / checkBoxesListProp.arraySize : 0;
            Rect progressBarRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight, GUILayout.Width(100));
            EditorGUI.ProgressBar(progressBarRect, progress, $"{Mathf.RoundToInt(progress * 100)}%");
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(2);

            if (GUILayout.Button("Add item", GUILayout.ExpandWidth(false)))
            {
                checkBoxesListProp.InsertArrayElementAtIndex(checkBoxesListProp.arraySize);
                SerializedProperty newItemProp = checkBoxesListProp.GetArrayElementAtIndex(checkBoxesListProp.arraySize - 1);
                SerializedProperty textP = newItemProp.FindPropertyRelative("text");
                SerializedProperty isCheckedP = newItemProp.FindPropertyRelative("isChecked");
                if (textP != null) textP.stringValue = "New item";
                if (isCheckedP != null) isCheckedP.boolValue = false;
            }
            EditorGUILayout.Space(5);

            for (int i = 0; i < checkBoxesListProp.arraySize; i++)
            {
                SerializedProperty itemProp = checkBoxesListProp.GetArrayElementAtIndex(i);
                if (itemProp == null) continue;

                SerializedProperty isCheckedProp = itemProp.FindPropertyRelative("isChecked");
                SerializedProperty textProp = itemProp.FindPropertyRelative("text");

                EditorGUILayout.BeginHorizontal();
                if (isCheckedProp != null)
                {
                    isCheckedProp.boolValue = EditorGUILayout.Toggle(GUIContent.none, isCheckedProp.boolValue, GUILayout.Width(18));
                }
                else
                {
                    GUILayout.Label("?", GUILayout.Width(18));
                    Debug.LogError($"Checklist item {i}: 'isChecked' property not found!");
                }

                if (textProp != null)
                    EditorGUILayout.PropertyField(textProp, GUIContent.none, GUILayout.ExpandWidth(true));
                else EditorGUILayout.LabelField("Error: Text Missing", GUILayout.ExpandWidth(true));

                if (GUILayout.Button(EditorGUIUtility.IconContent("winbtn_win_close"), GUILayout.Width(20), GUILayout.Height(18)))
                {
                    string itemText = textProp != null ? textProp.stringValue : "this item";
                    if (EditorUtility.DisplayDialog("Delete Item?", $"Are you sure you want to delete: '{itemText}'?", "Delete", "Cancel"))
                    {
                        checkBoxesListProp.DeleteArrayElementAtIndex(i);
                        GUI.changed = true;
                        break;
                    }
                }
                EditorGUILayout.EndHorizontal();
            }

            if (checkBoxesListProp.arraySize > 0)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Hide checked items", EditorStyles.miniButton)) { /* TODO */ }
                if (GUILayout.Button("Delete Checklist", EditorStyles.miniButton))
                {
                    if (EditorUtility.DisplayDialog("Delete Entire Checklist?", "Are you sure?", "Delete All", "Cancel"))
                    {
                        checkBoxesListProp.ClearArray();
                        GUI.changed = true;
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private int GetCheckedItemCount(SerializedProperty listProp)
        {
            int count = 0;
            for (int i = 0; i < listProp.arraySize; i++)
            {
                SerializedProperty item = listProp.GetArrayElementAtIndex(i);
                if (item != null)
                {
                    SerializedProperty isChecked = item.FindPropertyRelative("isChecked");
                    if (isChecked != null && isChecked.boolValue)
                    {
                        count++;
                    }
                }
            }
            return count;
        }

        private void DrawScriptRefsGUI()
        {
            SerializedProperty scriptRefsListProp = _serializedNodeObject.FindProperty("scriptRefs");
            if (scriptRefsListProp == null || !scriptRefsListProp.isArray)
            {
                EditorGUILayout.HelpBox("Script References data not found.", MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField("Script References", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            if (GUILayout.Button("Add Script Reference", GUILayout.ExpandWidth(false)))
            {
                scriptRefsListProp.InsertArrayElementAtIndex(scriptRefsListProp.arraySize);
                SerializedProperty newItemProp = scriptRefsListProp.GetArrayElementAtIndex(scriptRefsListProp.arraySize - 1);
                
                SerializedProperty targetScriptProp = newItemProp.FindPropertyRelative("targetScript");
                if (targetScriptProp != null) targetScriptProp.objectReferenceValue = null;
                
                SerializedProperty lineProp = newItemProp.FindPropertyRelative("line");
                if (lineProp != null) lineProp.intValue = 0; // <--- CHANGED: Initialize int to 0 (or -1, or 1, as preferred)
            }
            EditorGUILayout.Space(5);

            for (int i = 0; i < scriptRefsListProp.arraySize; i++)
            {
                SerializedProperty itemProp = scriptRefsListProp.GetArrayElementAtIndex(i);
                if (itemProp == null) continue;

                SerializedProperty targetScriptProp = itemProp.FindPropertyRelative("targetScript");
                SerializedProperty lineProp = itemProp.FindPropertyRelative("line");

                EditorGUILayout.BeginHorizontal();

                if (targetScriptProp != null)
                {
                    EditorGUILayout.PropertyField(targetScriptProp, GUIContent.none, GUILayout.ExpandWidth(true));
                }
                else
                {
                    EditorGUILayout.LabelField("Error: Script Property Missing", GUILayout.ExpandWidth(true));
                }

                // 'line' Integer Field
                if (lineProp != null)
                {
                    // Optionally, add a label if "L:" or "Line:" is desired
                    // GUILayout.Label("L:", GUILayout.Width(15)); 
                    lineProp.intValue = EditorGUILayout.IntField(GUIContent.none, lineProp.intValue, GUILayout.Width(40)); // <--- CHANGED to IntField
                }
                else
                {
                     GUILayout.Label("?", GUILayout.Width(40)); // Adjusted width for consistency
                }

                GUI.enabled = targetScriptProp != null && targetScriptProp.objectReferenceValue != null;
                if (GUILayout.Button(new GUIContent(GetIcon("TextAsset Icon"), "Open Script"), GUILayout.Width(30), GUILayout.Height(EditorGUIUtility.singleLineHeight))) // Use singleLineHeight for consistency
                {
                    MonoScript script = targetScriptProp.objectReferenceValue as MonoScript;
                    if (script != null)
                    {
                        // To open at a specific line, if the int 'line' is > 0
                        int lineNumber = lineProp != null ? lineProp.intValue : 0;
                        if (lineNumber > 0) {
                            AssetDatabase.OpenAsset(script, lineNumber);
                        } else {
                            AssetDatabase.OpenAsset(script);
                        }
                    }
                }
                GUI.enabled = true;

                if (GUILayout.Button(EditorGUIUtility.IconContent("winbtn_win_close"), GUILayout.Width(20), GUILayout.Height(EditorGUIUtility.singleLineHeight))) // Use singleLineHeight
                {
                    string scriptName = (targetScriptProp != null && targetScriptProp.objectReferenceValue != null) ? targetScriptProp.objectReferenceValue.name : "this script reference";
                    if (EditorUtility.DisplayDialog("Delete Script Reference?", $"Are you sure you want to delete the reference to '{scriptName}'?", "Delete", "Cancel"))
                    {
                        if (targetScriptProp != null && targetScriptProp.objectReferenceValue != null)
                        {
                            targetScriptProp.objectReferenceValue = null; 
                        }
                        scriptRefsListProp.DeleteArrayElementAtIndex(i);
                        GUI.changed = true;
                        break;
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
             if (scriptRefsListProp.arraySize > 0)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace(); 
                if (GUILayout.Button("Delete All Scripts", EditorStyles.miniButton))
                {
                    if (EditorUtility.DisplayDialog("Delete All Script References?", "Are you sure you want to remove all script references from this card?", "Delete All", "Cancel"))
                    {
                        scriptRefsListProp.ClearArray();
                        GUI.changed = true;
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private Texture GetIcon(string iconName)
        {
            var icon = EditorGUIUtility.IconContent(iconName);
            return icon?.image;
        }
    }
}