

using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using ND_BehaviorTree;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace ND_BehaviorTree.Editor
{
    public class ND_BehaviorTreeEditorWindow : EditorWindow
    {
        // --- Menu Items & Open Methods (no changes) ---
        [MenuItem("ND_BehaviorTree/ND Behavior Tree Window Editor")]
        public static void OpenWindow()
        {
            ND_BehaviorTreeEditorWindow window = GetWindow<ND_BehaviorTreeEditorWindow>();
            window.titleContent = new GUIContent("Behavior Tree Editor");
        }
        
        public static void Open(BehaviorTreeRunner runner)
        {
            ND_BehaviorTreeEditorWindow window = GetWindow<ND_BehaviorTreeEditorWindow>();
            window.titleContent = new GUIContent("Behavior Tree Editor");
            window.Load(runner);
        }

        public static void Open(BehaviorTree target)
        {
            ND_BehaviorTreeEditorWindow window = GetWindow<ND_BehaviorTreeEditorWindow>();
            window.titleContent = new GUIContent("Behavior Tree Editor");
            window.Load(target);
        }

        // --- Fields ---
        [SerializeField] private BehaviorTree m_currentGraph;
        [SerializeField] private SerializedObject m_serializeObject;
        [SerializeField] private ND_BehaviorTreeView m_currentView;
        [SerializeField] public BehaviorTreeRunner m_targetRunner;
        
        [SerializeField] private bool m_isLocked;
        private VisualElement m_contentContainer;
        private ToolbarToggle m_lockToggle;

        // --- NEW: Fields for Blackboard Toggle ---
        [SerializeField] private bool m_isBlackboardVisible = true; // Remember state
        private ToolbarToggle m_blackboardToggle;

        public BehaviorTree currentGraph => m_currentGraph;
        
        // --- Unity Messages (OnEnable is modified) ---
        private void OnEnable()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
            
            Selection.selectionChanged -= OnSelectionChanged;
            Selection.selectionChanged += OnSelectionChanged;

            CreateUIStructure();
            OnSelectionChanged();
        }
        
        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            Selection.selectionChanged -= OnSelectionChanged;
        }

        private void OnSelectionChanged()
        {
            // If locked, do nothing.
            if (m_isLocked) return;
            
            // If the window is closed, do nothing.
            if (this == null) return;
            
            BehaviorTree treeToLoad = null;
            BehaviorTreeRunner runnerToDebug = null;

            GameObject selectedObject = Selection.activeGameObject;
            
            if (selectedObject != null)
            {
                BehaviorTreeRunner runner = selectedObject.GetComponent<BehaviorTreeRunner>();
                if (runner != null && runner.treeAsset != null)
                {
                    treeToLoad = runner.treeAsset;
                    if (Application.isPlaying)
                    {
                        runnerToDebug = runner;
                    }
                }
            }

            if (treeToLoad == null)
            {
                treeToLoad = Selection.activeObject as BehaviorTree;
            }

            if (runnerToDebug != null)
            {
                Load(runnerToDebug);
            }
            else if (treeToLoad != null)
            {
                if (m_currentGraph != treeToLoad || m_targetRunner != null) 
                {
                    Load(treeToLoad);
                }
            }
            else
            {
                ClearView();
            }
        }
        
        // --- UI Creation (CreateToolbar is modified) ---
        private void CreateUIStructure()
        {
            rootVisualElement.Clear();
            CreateToolbar();
            m_contentContainer = new VisualElement { name = "ContentContainer" };
            m_contentContainer.style.flexGrow = 1;
            rootVisualElement.Add(m_contentContainer);
        }

        private void CreateToolbar()
        {
            var toolbar = new Toolbar();

            // --- NEW: Blackboard Toggle ---
            m_blackboardToggle = new ToolbarToggle { text = "Blackboard" };
            m_blackboardToggle.tooltip = "Show or hide the Blackboard panel.";
            m_blackboardToggle.value = m_isBlackboardVisible; // Restore saved state
            m_blackboardToggle.RegisterValueChangedCallback(evt => {
                m_isBlackboardVisible = evt.newValue;
                if (m_currentView != null)
                {
                    m_currentView.ToggleBlackboard(m_isBlackboardVisible);
                }
            });
            toolbar.Add(m_blackboardToggle);
            // --- END NEW ---

            // Add a separator
            toolbar.Add(new ToolbarSpacer());

            // Lock toggle
            m_lockToggle = new ToolbarToggle { text = "Lock" };
            m_lockToggle.tooltip = "Prevents the editor from changing the tree when you select a different object.";
            m_lockToggle.value = m_isLocked;
            m_lockToggle.RegisterValueChangedCallback(evt => {
                m_isLocked = evt.newValue;
                if (!m_isLocked)
                {
                    OnSelectionChanged();
                }
            });
            
            toolbar.Add(m_lockToggle);
            rootVisualElement.Add(toolbar);
        }

        
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
            if (m_currentGraph == null || m_contentContainer == null) return;
            
            m_serializeObject = new SerializedObject(m_currentGraph);
            m_currentView = new ND_BehaviorTreeView(m_serializeObject, this);
            m_currentView.graphViewChanged += OnChange;
            
            // --- NEW: Ensure blackboard visibility is set correctly on load ---
            m_currentView.ToggleBlackboard(m_isBlackboardVisible);
            
            m_contentContainer.Clear(); 
            m_contentContainer.Add(m_currentView);
        }
        
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