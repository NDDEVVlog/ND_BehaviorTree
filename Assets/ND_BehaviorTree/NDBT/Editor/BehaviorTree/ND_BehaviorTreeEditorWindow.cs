using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using ND_BehaviorTree;
using System.Linq;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using ND_BehaviorTree.Editor.CustomGraph;

namespace ND_BehaviorTree.Editor
{
    public class ND_BehaviorTreeEditorWindow : EditorWindow
    {
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

        [SerializeField] private BehaviorTree m_currentGraph;
        [SerializeField] private SerializedObject m_serializeObject;
        [SerializeField] private ND_BTGraphCanvas m_currentView;
        [SerializeField] public BehaviorTreeRunner m_targetRunner;
        
        [SerializeField] private bool m_isLocked;
        private VisualElement m_contentContainer;
        private ToolbarToggle m_lockToggle;

        [SerializeField] private bool m_isBlackboardVisible = true;
        private ToolbarToggle m_blackboardToggle;
        
        private BlackboardView m_blackboardView;

        public BehaviorTree currentGraph => m_currentGraph;
        
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
            if (m_isLocked) return;
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

            m_blackboardToggle = new ToolbarToggle { text = "Blackboard" };
            m_blackboardToggle.tooltip = "Show or hide the Blackboard panel.";
            m_blackboardToggle.value = m_isBlackboardVisible;
            m_blackboardToggle.RegisterValueChangedCallback(evt => {
                m_isBlackboardVisible = evt.newValue;
                ToggleBlackboard(m_isBlackboardVisible);
            });
            toolbar.Add(m_blackboardToggle);

            toolbar.Add(new ToolbarSpacer());

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
            m_currentView = new ND_BTGraphCanvas(m_serializeObject, this);
            
            m_contentContainer.Clear(); 
            m_contentContainer.Add(m_currentView);

            m_blackboardView = new BlackboardView(m_serializeObject);
            m_contentContainer.Add(m_blackboardView);
            ToggleBlackboard(m_isBlackboardVisible);
        }
        
        private void ToggleBlackboard(bool isVisible)
        {
            if (m_blackboardView != null)
            {
                m_blackboardView.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void ClearView()
        {
            m_currentGraph = null;
            m_serializeObject = null;
            m_currentView = null;
            m_targetRunner = null;
            m_blackboardView = null;
            
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

        public void SetUnsavedChanges(bool unsaved)
        {
            this.hasUnsavedChanges = unsaved;
        }
        
        private void OnEditorUpdate()
        {
            if (m_currentView == null) return;

            var nodes = m_currentView.Query<ND_BTNodeView>().ToList();

            if (Application.isPlaying && m_targetRunner != null && m_targetRunner.RuntimeTree != null)
            {
                foreach (var nodeView in nodes)
                {
                    nodeView.UpdateState(m_targetRunner.RuntimeTree);
                }
            }
            else
            {
                foreach (var nodeView in nodes)
                {
                    nodeView.ClearState();
                }
            }
        }
    }
}