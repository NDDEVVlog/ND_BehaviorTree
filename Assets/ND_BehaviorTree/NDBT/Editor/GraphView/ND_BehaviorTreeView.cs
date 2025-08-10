

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND_BehaviorTree.Editor
{
    public partial class ND_BehaviorTreeView : GraphView
    {
        // --- Fields ---
        private BehaviorTree m_BTree;
        private SerializedObject m_serialLizeObject;
        private ND_BehaviorTreeEditorWindow m_editorWindow;
        public ND_BehaviorTreeEditorWindow EditorWindow => m_editorWindow;

        public BehaviorTree BTree => m_BTree; 
        public List<ND_NodeEditor> TreeNodes { get; private set; }
        public Dictionary<string, ND_NodeEditor> NodeDictionary { get; private set; }
        
        private bool m_isDeletingProgrammatically = false;

        private ND_BTSearchProvider m_searchProvider;
        private List<IContextualMenuCommand> m_contextualMenuCommands;
        
        private BlackboardView m_blackboardView;

        public ND_BehaviorTreeView(SerializedObject serializedObject, ND_BehaviorTreeEditorWindow editorWindow)
        {
            m_editorWindow = editorWindow;
            m_serialLizeObject = serializedObject;
            m_BTree = (BehaviorTree)serializedObject.targetObject;

            // This is required to receive keyboard events like Ctrl+C and Ctrl+V.
            this.focusable = true;

            TreeNodes = new List<ND_NodeEditor>();
            NodeDictionary = new Dictionary<string, ND_NodeEditor>();

            m_searchProvider = ScriptableObject.CreateInstance<ND_BTSearchProvider>();
            if (m_searchProvider == null) Debug.LogError("[ND_DrawTrelloView.ctor] SearchProvider is NULL after CreateInstance!");
            else m_searchProvider.view = this;

            this.nodeCreationRequest = OnNodeCreationRequest;

            m_contextualMenuCommands = new List<IContextualMenuCommand>
            {
                new CreateNodeContextualCommand(),
            };

            

            SetupStylingAndBackground();
            SetupManipulators();

            // This call activates the event listeners for Copy, Paste, etc.
            RegisterCopyPasteCallbacks();

            CreateBlackboard();

            DrawExistingGraphElementsFromData();

            SetupZoom(0.1f, 3.0f);
            graphViewChanged += OnGraphViewInternalChange;
            this.deleteSelection = OnDeleteSelectionKeyPressed;
        }
        
        private void CreateBlackboard()
        {
            m_blackboardView = new BlackboardView(m_serialLizeObject);
            Add(m_blackboardView);
            m_blackboardView.style.display = DisplayStyle.None;
        }
        
        public void ToggleBlackboard(bool isVisible)
        {
            if (m_blackboardView != null)
            {
                m_blackboardView.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }
        
        private void SetupStylingAndBackground()
        {
            StyleSheet styleSheetAsset = AssetDatabase.LoadAssetAtPath<StyleSheet>(ND_BehaviorTreeSetting.Instance.GetGraphViewStyleSheetPath());
            styleSheets.Add(styleSheetAsset);

            style.flexGrow = 1;
            style.width = Length.Percent(100);
            style.height = Length.Percent(100);

            GridBackground background = new GridBackground { name = "Grid" };
            Add(background);
            background.SendToBack();
        }

        private void SetupManipulators()
        {
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            this.AddManipulator(new ClickSelector());
        }

        private void DrawExistingGraphElementsFromData()
        {
            m_BTree.EditorInit();
            DrawNodesFromData();
            DrawConnectionsFromData();
            BindSerializedObject();
        }
        
        private void BindSerializedObject()
        {
            if (m_serialLizeObject == null || m_serialLizeObject.targetObject == null) {
                Debug.LogWarning("[BindSerializedObject] SerializedObject or its target is null. Cannot bind.");
                return;
            }
            m_serialLizeObject.Update(); 
            this.Bind(m_serialLizeObject);
        }
    }
}