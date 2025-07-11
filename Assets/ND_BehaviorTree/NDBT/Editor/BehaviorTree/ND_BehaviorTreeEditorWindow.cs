

using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using ND_BehaviorTree;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using UnityEditor.UIElements; // Required for Toolbar and ToolbarToggle

namespace ND_BehaviorTree.Editor
{
    public class ND_BehaviorTreeEditorWindow : EditorWindow
    {
        // --- NEW: Standard Menu Item to Open the Window ---
        [MenuItem("Window/ND Behavior Tree Editor")]
        public static void OpenWindow()
        {
            ND_BehaviorTreeEditorWindow window = GetWindow<ND_BehaviorTreeEditorWindow>();
            window.titleContent = new GUIContent("Behavior Tree Editor");
        }

        // --- MODIFIED: Open Methods now use GetWindow to avoid duplicates ---
        public static void Open(BehaviorTreeRunner runner)
        {
            // GetWindow will find an existing window or create one.
            ND_BehaviorTreeEditorWindow window = GetWindow<ND_BehaviorTreeEditorWindow>();
            window.titleContent = new GUIContent("Behavior Tree Editor"); // Set a generic title first
            window.Load(runner);
        }

        public static void Open(BehaviorTree target)
        {
            ND_BehaviorTreeEditorWindow window = GetWindow<ND_BehaviorTreeEditorWindow>();
            window.titleContent = new GUIContent("Behavior Tree Editor"); // Set a generic title first
            window.Load(target);
        }

        // --- Fields ---
        [SerializeField] private BehaviorTree m_currentGraph;
        [SerializeField] private SerializedObject m_serializeObject;
        [SerializeField] private ND_BehaviorTreeView m_currentView;
        [SerializeField] public BehaviorTreeRunner m_targetRunner;
        
        // --- NEW: Fields for Lock functionality and UI structure ---
        [SerializeField] private bool m_isLocked;
        private VisualElement m_contentContainer;
        private ToolbarToggle m_lockToggle;

        public BehaviorTree currentGraph => m_currentGraph;

        // --- Unity Messages ---

        // --- MODIFIED: OnEnable now creates the toolbar and UI structure ---
        private void OnEnable()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
            
            Selection.selectionChanged -= OnSelectionChanged;
            Selection.selectionChanged += OnSelectionChanged;

            // Create the static UI structure (Toolbar + Content Container)
            CreateUIStructure();

            // When the window is enabled, immediately check the current selection
            // to automatically load the correct graph (if not locked).
            OnSelectionChanged();
        }
        
        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            Selection.selectionChanged -= OnSelectionChanged;
        }

        // --- MODIFIED: The core auto-detection logic now respects the lock state ---
        private void OnSelectionChanged()
        {
            // If locked, do nothing.
            if (m_isLocked) return;
            
            // If the window is closed, do nothing.
            if (this == null) return;
            
            BehaviorTree treeToLoad = null;
            BehaviorTreeRunner runnerToDebug = null;

            GameObject selectedObject = Selection.activeGameObject;
            
            // If a GameObject is selected, check it for a runner
            if (selectedObject != null)
            {
                BehaviorTreeRunner runner = selectedObject.GetComponent<BehaviorTreeRunner>();
                if (runner != null && runner.treeAsset != null)
                {
                    treeToLoad = runner.treeAsset;
                    // Only assign the runner for debugging if in Play Mode
                    if (Application.isPlaying)
                    {
                        runnerToDebug = runner;
                    }
                }
            }

            // If no tree was found on a GameObject, check if an asset is selected
            if (treeToLoad == null)
            {
                treeToLoad = Selection.activeObject as BehaviorTree;
            }

            // Now, decide what to do based on what was found
            if (runnerToDebug != null)
            {
                // Highest priority: Live debugging a selected runner
                Load(runnerToDebug);
            }
            else if (treeToLoad != null)
            {
                // Second priority: Show the asset from a runner (in Edit Mode) or a selected asset
                if (m_currentGraph != treeToLoad || m_targetRunner != null) // Reload if tree is different or we were previously debugging
                {
                    Load(treeToLoad);
                }
            }
            else
            {
                // Nothing relevant selected, clear the view
                ClearView();
            }
        }
        
        // --- NEW: Methods to create the window's UI structure ---
        private void CreateUIStructure()
        {
            rootVisualElement.Clear();

            // Create the toolbar at the top
            CreateToolbar();

            // Create a container for the graph view or prompt message
            m_contentContainer = new VisualElement { name = "ContentContainer" };
            m_contentContainer.style.flexGrow = 1; // Make it fill remaining space
            rootVisualElement.Add(m_contentContainer);
        }

        private void CreateToolbar()
        {
            var toolbar = new Toolbar();

            // Add the lock toggle
            m_lockToggle = new ToolbarToggle { text = "Lock" };
            m_lockToggle.tooltip = "Prevents the editor from changing the tree when you select a different object.";
            m_lockToggle.value = m_isLocked; // Restore saved lock state
            m_lockToggle.RegisterValueChangedCallback(evt => {
                m_isLocked = evt.newValue;
                // If we just unlocked, immediately sync with the current selection
                if (!m_isLocked)
                {
                    OnSelectionChanged();
                }
            });
            
            toolbar.Add(m_lockToggle);
            rootVisualElement.Add(toolbar);
        }

        // --- Load & Draw Methods ---
        
        public void Load(BehaviorTreeRunner runner)
        {
            if (runner == null || runner.treeAsset == null) 
            {
                ClearView();
                return;
            }
            m_targetRunner = runner;
            m_currentGraph = runner.treeAsset;
            titleContent = new GUIContent($"{runner.gameObject.name} ({runner.treeAsset.name})", EditorGUIUtility.ObjectContent(null, typeof(BehaviorTree)).image);
            DrawGraph();
        }

        public void Load(BehaviorTree target)
        {   
            if (target == null)
            {
                ClearView();
                return;
            }
            m_targetRunner = null;
            m_currentGraph = target;
            titleContent = new GUIContent($"{target.name}", EditorGUIUtility.ObjectContent(null, typeof(BehaviorTree)).image);
            DrawGraph();
        }

        // --- MODIFIED: Draws the graph inside the content container ---
        private void DrawGraph()
        {
            if (m_currentGraph == null || m_contentContainer == null) return;
            
            m_serializeObject = new SerializedObject(m_currentGraph);
            m_currentView = new ND_BehaviorTreeView(m_serializeObject, this);
            m_currentView.graphViewChanged += OnChange;
            
            m_contentContainer.Clear(); 
            m_contentContainer.Add(m_currentView);
        }
        
        // --- MODIFIED: Clears the content container, leaving the toolbar ---
        private void ClearView()
        {
            m_currentGraph = null;
            m_serializeObject = null;
            m_currentView = null;
            m_targetRunner = null;
            
            if (m_contentContainer == null) return;
            m_contentContainer.Clear();

            var prompt = new Label("Select a GameObject with a BehaviorTreeRunner or a BehaviorTree asset.");
            prompt.style.unityTextAlign = TextAnchor.MiddleCenter;
            prompt.style.fontSize = 14;
            m_contentContainer.Add(prompt);

            // Only reset title if it's not already the default
            if (titleContent.text != "Behavior Tree Editor")
            {
                titleContent = new GUIContent("Behavior Tree Editor");
            }
            
            SetUnsavedChanges(false);
        }

        private GraphViewChange OnChange(GraphViewChange graphViewChange)
        {
            if (m_currentGraph == null) return graphViewChange;
            this.hasUnsavedChanges = true;
            EditorUtility.SetDirty(m_currentGraph);
            return graphViewChange;
        }

        public void SetUnsavedChanges(bool unsaved)
        {
            this.hasUnsavedChanges = unsaved;
        }
        
        private void OnEditorUpdate()
        {
            if (m_currentView == null) return;

            if (Application.isPlaying && m_targetRunner != null && m_targetRunner.RuntimeTree != null)
            {
                m_currentView.nodes.ForEach(n => {
                    if (n is ND_NodeEditor nodeView) nodeView.UpdateState();
                });
            }
            else
            {
                 m_currentView.nodes.ForEach(n => {
                    if (n is ND_NodeEditor nodeView) nodeView.ClearState();
                });
            }
        }
    }
}