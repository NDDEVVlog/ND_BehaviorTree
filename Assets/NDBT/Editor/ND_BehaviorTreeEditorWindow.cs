// --- MODIFIED FILE: ND_BehaviorTreeEditorWindow.cs ---

using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using ND_BehaviorTree;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements; // Required for Label in ClearView

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

        public BehaviorTree currentGraph => m_currentGraph;

        // --- Unity Messages ---

        private void OnEnable()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
            
            Selection.selectionChanged -= OnSelectionChanged;
            Selection.selectionChanged += OnSelectionChanged;

            // When the window is enabled, immediately check the current selection
            // to automatically load the correct graph.
            OnSelectionChanged();
        }
        
        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            Selection.selectionChanged -= OnSelectionChanged;
        }

        // --- MODIFIED: The core auto-detection logic ---
        private void OnSelectionChanged()
        {
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

        private void DrawGraph()
        {
            if (m_currentGraph == null) return;
            
            m_serializeObject = new SerializedObject(m_currentGraph);
            m_currentView = new ND_BehaviorTreeView(m_serializeObject, this);
            m_currentView.graphViewChanged += OnChange;
            
            rootVisualElement.Clear(); 
            rootVisualElement.Add(m_currentView);
        }
        
        private void ClearView()
        {
            m_currentGraph = null;
            m_serializeObject = null;
            m_currentView = null;
            m_targetRunner = null;
            
            rootVisualElement.Clear();

            var prompt = new Label("Select a GameObject with a BehaviorTreeRunner or a BehaviorTree asset.");
            prompt.style.unityTextAlign = TextAnchor.MiddleCenter;
            prompt.style.fontSize = 14;
            rootVisualElement.Add(prompt);

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