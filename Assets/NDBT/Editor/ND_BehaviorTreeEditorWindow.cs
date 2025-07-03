using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using ND_BehaviorTree;
using System.Linq;
using UnityEditor.Experimental.GraphView;

namespace ND_BehaviorTree.Editor
{
    public class ND_BehaviorTreeEditorWindow : EditorWindow
    {
        // --- NEW ---
        // Overload to open for a specific runner (for debugging)
        public static void Open(BehaviorTreeRunner runner)
        {
            ND_BehaviorTreeEditorWindow[] windows = Resources.FindObjectsOfTypeAll<ND_BehaviorTreeEditorWindow>();
            foreach (var n in windows)
            {
                // Focus window if it's already debugging this runner
                if (n.m_targetRunner == runner)
                {
                    n.Focus();
                    return;
                }
            }
            ND_BehaviorTreeEditorWindow window = CreateWindow<ND_BehaviorTreeEditorWindow>(typeof(ND_BehaviorTreeEditorWindow), typeof(SceneView));
            // MODIFIED: Set a more descriptive title for debugging
            window.titleContent = new GUIContent($"{runner.gameObject.name} ({runner.treeAsset.name})", EditorGUIUtility.ObjectContent(null, typeof(BehaviorTree)).image);
            window.Load(runner);
        }

        // Original method for opening an asset directly
        public static void Open(BehaviorTree target)
        {
            ND_BehaviorTreeEditorWindow[] windows = Resources.FindObjectsOfTypeAll<ND_BehaviorTreeEditorWindow>();
            foreach (var n in windows)
            {
                // Focus window if it's showing this asset AND not in debug mode
                if (n.m_targetRunner == null && n.currentGraph == target)
                {
                    n.Focus();
                    return;
                }
            }
            ND_BehaviorTreeEditorWindow window = CreateWindow<ND_BehaviorTreeEditorWindow>(typeof(ND_BehaviorTreeEditorWindow), typeof(SceneView));
            window.titleContent = new GUIContent($"{target.name}", EditorGUIUtility.ObjectContent(null, typeof(BehaviorTree)).image);
            window.Load(target);
        }

        [SerializeField] private BehaviorTree m_currentGraph;
        [SerializeField] private SerializedObject m_serializeObject;
        [SerializeField] private ND_BehaviorTreeView m_currentView;
        
        // --- NEW ---
        [SerializeField] public BehaviorTreeRunner m_targetRunner;

        public BehaviorTree currentGraph => m_currentGraph;

        private void OnEnable()
        {
            // --- NEW ---
            // Subscribe to the editor update loop for live UI updates
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
            
            // Re-draw the graph if an asset is already selected
            if (m_currentGraph != null)
            {
                DrawGraph();
            }
        }
        
        // --- NEW ---
        private void OnDisable()
        {
            // Unsubscribe to prevent memory leaks
            EditorApplication.update -= OnEditorUpdate;
        }
        
        // --- NEW ---
        // Load method for debugging a runner instance
        public void Load(BehaviorTreeRunner runner)
        {
            m_targetRunner = runner;
            m_currentGraph = runner.treeAsset;
            DrawGraph();
        }

        // Original load method for an asset
        public void Load(BehaviorTree target)
        {   
            m_targetRunner = null; // Ensure we are not in debug mode
            m_currentGraph = target;
            DrawGraph();
        }

        private void DrawGraph()
        {
            if (m_currentGraph == null) return;
            
            m_serializeObject = new SerializedObject(m_currentGraph);
            m_currentView = new ND_BehaviorTreeView(m_serializeObject, this);
            m_currentView.graphViewChanged += OnChange;
            
            // Clear previous graph view before adding a new one
            rootVisualElement.Clear(); 
            rootVisualElement.Add(m_currentView);
        }

        private GraphViewChange OnChange(GraphViewChange graphViewChange)
        {
            this.hasUnsavedChanges = true;
            EditorUtility.SetDirty(m_currentGraph);
            return graphViewChange;
        }

        public void SetUnsavedChanges(bool unsaved)
        {
            this.hasUnsavedChanges = unsaved;
        }

        // --- NEW ---
        /// <summary>
        /// Called on every editor update. Used to update node visuals during play mode.
        /// </summary>
        private void OnEditorUpdate()
        {
            if (m_currentView == null) return;

            // Only update visuals if we are in play mode and debugging a specific runner
            if (Application.isPlaying && m_targetRunner != null && m_targetRunner.RuntimeTree != null)
            {
                // Traverse all nodes in the view and update their status
                m_currentView.nodes.ForEach(n => {
                    var nodeView = n as ND_NodeEditor;
                    if (nodeView != null)
                    {
                        nodeView.UpdateState();
                    }
                });
            }
            else
            {
                // If not playing, ensure all nodes have their default state
                 m_currentView.nodes.ForEach(n => {
                    var nodeView = n as ND_NodeEditor;
                    if (nodeView != null)
                    {
                        nodeView.ClearState();
                    }
                });
            }
        }
    }
}