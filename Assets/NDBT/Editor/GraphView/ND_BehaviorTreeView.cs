// --- MODIFIED FILE: ND_BehaviorTreeView.cs ---

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
    /// <summary>
    /// The core file for the ND_BehaviorTreeView class.
    /// It contains the main fields, constructor, and setup methods for the graph view.
    /// </summary>
    public partial class ND_BehaviorTreeView : GraphView
    {
        // --- Fields ---
        private BehaviorTree m_BTree; // The actual ScriptableObject data
        private SerializedObject m_serialLizeObject; // SerializedObject wrapper for m_BTree
        private ND_BehaviorTreeEditorWindow m_editorWindow; // Reference to the hosting editor window
        public ND_BehaviorTreeEditorWindow EditorWindow => m_editorWindow; // Public getter

        // Collections for managing graph elements
        public BehaviorTree BTree => m_BTree; 
        public List<ND_NodeEditor> TreeNodes { get; private set; }
        public Dictionary<Edge, ND_BTConnection> ConnectionDictionary { get; private set; }
        public Dictionary<string, ND_NodeEditor> NodeDictionary { get; private set; }
        
        // --- NEW FIELD ---
        // Flag to prevent OnGraphViewInternalChange from interfering with programmatic deletions.
        private bool m_isDeletingProgrammatically = false;

        private ND_BTSearchProvider m_searchProvider;
        private List<IContextualMenuCommand> m_contextualMenuCommands;

        // --- Constructor ---
        public ND_BehaviorTreeView(SerializedObject serializedObject, ND_BehaviorTreeEditorWindow editorWindow)
        {
            m_editorWindow = editorWindow;
            m_serialLizeObject = serializedObject;
            m_BTree = (BehaviorTree)serializedObject.targetObject;

            

            // Initialize collections
            TreeNodes = new List<ND_NodeEditor>();
            NodeDictionary = new Dictionary<string, ND_NodeEditor>();
            ConnectionDictionary = new Dictionary<Edge, ND_BTConnection>();

            // Initialize search provider
            m_searchProvider = ScriptableObject.CreateInstance<ND_BTSearchProvider>();
            if (m_searchProvider == null) Debug.LogError("[ND_DrawTrelloView.ctor] SearchProvider is NULL after CreateInstance!");
            else m_searchProvider.view = this; // Provide reference to this view

            // Assign node creation request callback
            this.nodeCreationRequest = OnNodeCreationRequest;

            // Initialize Contextual Menu Commands
            m_contextualMenuCommands = new List<IContextualMenuCommand>
            {
                new CreateNodeContextualCommand(),
                new DeleteAnimatedContextualCommand()
            };

            SetupStylingAndBackground();
            SetupManipulators();
            
            DrawExistingGraphElementsFromData(); // Populate view from m_BTree

            SetupZoom(0.1f, 3.0f); // Min and Max zoom levels
            graphViewChanged += OnGraphViewInternalChange; // Callback for internal GraphView changes
            this.deleteSelection = OnDeleteSelectionKeyPressed; // Callback for Delete key press
        }

        // --- Setup Methods ---
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
        {   // Ensure the tree asset is properly initialized with a RootNode before drawing.
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