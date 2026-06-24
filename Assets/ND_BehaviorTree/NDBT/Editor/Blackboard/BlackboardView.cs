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
        private BlackboardKeySearchProvider m_keySearchProvider;
        private BlackboardTheme m_Theme;

        private bool m_IsDragging;
        private Vector2 m_DragStartMousePos;
        private Vector2 m_DragStartPanelPos;

        public BlackboardView(SerializedObject treeSerializer)
        {
            m_treeSerializer = treeSerializer;
            m_BTree = treeSerializer.targetObject as BehaviorTree;
            m_Theme = ND_BehaviorTreeSetting.Instance.blackboardTheme;

            style.position = Position.Absolute;
            style.left = style.top = 20;
            style.minWidth = 250; style.minHeight = 150; style.maxHeight = 400;
            style.backgroundColor = m_Theme.backgroundColor;
            style.borderTopWidth = style.borderBottomWidth = style.borderLeftWidth = style.borderRightWidth = 1;
            style.borderTopColor = style.borderBottomColor = style.borderLeftColor = style.borderRightColor = new Color(0.1f, 0.1f, 0.1f, 1.0f);
            style.borderTopLeftRadius = style.borderTopRightRadius = style.borderBottomLeftRadius = style.borderBottomRightRadius = 8;

            m_keySearchProvider = ScriptableObject.CreateInstance<BlackboardKeySearchProvider>();
            m_keySearchProvider.Initialize(this);

            if (m_Theme.customUXML != null) m_Theme.customUXML.CloneTree(this);
            else CreateFallbackUI();

            if (m_Theme.customUSS != null) styleSheets.Add(m_Theme.customUSS);

            SetupHeaderDrag();
            this.Q<Button>("add-key-button")?.RegisterCallback<ClickEvent>(e => OnAddKeyClicked());
            var title = this.Q<Label>("title-label");
            if (title != null) title.text = m_BTree.name + " Blackboard";

            PopulateView();
        }

        private void SetupHeaderDrag()
        {
            var header = this.Q<VisualElement>("header");
            if (header == null) return;
            header.RegisterCallback<PointerDownEvent>(e => {
                if (e.button != 0) return;
                m_IsDragging = true;
                m_DragStartMousePos = e.position;
                m_DragStartPanelPos = new Vector2(style.left.value.value, style.top.value.value);
                header.CapturePointer(e.pointerId);
                BringToFront();
                e.StopPropagation();
            });
            header.RegisterCallback<PointerMoveEvent>(e => {
                if (!m_IsDragging || !header.HasPointerCapture(e.pointerId)) return;
                Vector2 d = e.position - (Vector3)m_DragStartMousePos;
                style.left = m_DragStartPanelPos.x + d.x;
                style.top = m_DragStartPanelPos.y + d.y;
                e.StopPropagation();
            });
            header.RegisterCallback<PointerUpEvent>(e => {
                if (m_IsDragging && header.HasPointerCapture(e.pointerId)) {
                    m_IsDragging = false; header.ReleasePointer(e.pointerId); e.StopPropagation();
                }
            });
        }

        private void CreateFallbackUI()
        {
            var header = new VisualElement { name = "header", style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween, backgroundColor = new Color(0.1f, 0.1f, 0.1f), paddingBottom = 5, paddingTop = 5 } };
            header.Add(new Label("Blackboard") { name = "title-label", style = { color = Color.white, unityFontStyleAndWeight = FontStyle.Bold } });
            header.Add(new Button { name = "add-key-button", text = "+" });
            Add(header);
            var scroll = new ScrollView();
            var foldout = new Foldout { text = "Keys", name = "blackboard-foldout" };
            foldout.Add(new VisualElement { name = "keys-container" });
            scroll.Add(foldout); Add(scroll);
        }

        private void OnAddKeyClicked()
        {
            if (m_BTree.blackboard == null) {
                if (EditorUtility.DisplayDialog("Create", "No Blackboard found. Create one?", "Yes", "No")) CreateAndAssignBlackboard();
                return;
            }
            SearchWindow.Open(new SearchWindowContext(GUIUtility.GUIToScreenPoint(Event.current.mousePosition)), m_keySearchProvider);
        }

        private void CreateAndAssignBlackboard()
        {
            var bb = ScriptableObject.CreateInstance<Blackboard>();
            bb.name = $"{m_BTree.name}_Blackboard";
            AssetDatabase.CreateAsset(bb, AssetDatabase.GenerateUniqueAssetPath($"{System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(m_BTree))}/{bb.name}.asset"));
            AssetDatabase.SaveAssets();
            m_treeSerializer.FindProperty("blackboard").objectReferenceValue = bb;
            m_treeSerializer.ApplyModifiedProperties();
            EditorUtility.SetDirty(m_BTree);
            PopulateView();
        }

        public void AddKey(Type keyType)
        {
            Undo.RecordObject(m_BTree.blackboard, "Add Key");
            var key = ScriptableObject.CreateInstance(keyType) as Key;
            key.name = keyType.Name;
            key.keyName = ObjectNames.GetUniqueName(m_BTree.blackboard.keys.Select(k => k.keyName).ToArray(), $"New {keyType.Name.Replace("Key_", "")}");
            AssetDatabase.AddObjectToAsset(key, m_BTree.blackboard);
            m_BTree.blackboard.keys.Add(key);
            EditorUtility.SetDirty(m_BTree.blackboard);
            AssetDatabase.SaveAssets();
            PopulateView();
        }

        public void PopulateView()
        {
            var keysContainer = this.Q("keys-container");
            if (keysContainer == null) return;
            keysContainer.Clear();
            var foldout = this.Q<Foldout>("blackboard-foldout");

            if (m_BTree.blackboard == null) {
                if (foldout != null) foldout.style.display = DisplayStyle.None;
                keysContainer.Add(new Label("No Blackboard Asset.") { style = { unityTextAlign = TextAnchor.MiddleCenter } });
                return;
            }

            if (foldout != null) foldout.style.display = DisplayStyle.Flex;
            var serObj = new SerializedObject(m_BTree.blackboard);
            var keysProp = serObj.FindProperty("keys");

            for (int i = 0; i < m_BTree.blackboard.keys.Count; i++) {
                Key key = m_BTree.blackboard.keys[i];
                if (key == null) continue;

                var row = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 2, marginBottom = 2 } };
                var colorAttr = key.GetType().GetCustomAttribute<KeyColorAttribute>();
                
                row.Add(new VisualElement { style = { width = 10, height = 10, alignSelf = Align.Center, marginRight = 5, borderTopLeftRadius = 5, borderTopRightRadius = 5, borderBottomLeftRadius = 5, borderBottomRightRadius = 5, backgroundColor = colorAttr?.Color ?? Color.grey } });
                
                var nameField = new TextField { value = key.keyName, style = { width = 100 } };
                nameField.RegisterValueChangedCallback(e => { Undo.RecordObject(key, "Rename"); key.keyName = e.newValue; EditorUtility.SetDirty(key); });
                
                var propField = new PropertyField(keysProp.GetArrayElementAtIndex(i).FindPropertyRelative("value"), "") { style = { flexGrow = 1 } };
                propField.Bind(serObj);
                
                row.Add(nameField); row.Add(propField);
                row.Add(new Button(() => {
                    Undo.RecordObject(m_BTree.blackboard, "Delete Key"); m_BTree.blackboard.keys.Remove(key); Undo.DestroyObjectImmediate(key);
                    EditorUtility.SetDirty(m_BTree.blackboard); AssetDatabase.SaveAssets(); PopulateView();
                }) { text = "X", style = { width = 20 } });
                
                keysContainer.Add(row);
            }
        }
    }
}