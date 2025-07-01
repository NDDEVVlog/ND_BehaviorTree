using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using ND_BehaviorTree;
using System.Security;
using UnityEditor.Experimental.GraphView;
using System;

namespace ND_BehaviorTree.Editor
{
    public class ND_BehaviorTreeEditorWindow : EditorWindow
    {
        public static void Open(BehaviorTree target)
        {
            ND_BehaviorTreeEditorWindow[] windows = Resources.FindObjectsOfTypeAll<ND_BehaviorTreeEditorWindow>();
            foreach (var n in windows)
            {
                if (n.currentGraph == target)
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

        public BehaviorTree currentGraph => m_currentGraph;

        private void OnEnable()
        {
            if (m_currentGraph != null)
            {
                DrawGraph();
            }
        }

        public void Load(BehaviorTree target)
        {   
            //LoadGraph
            m_currentGraph = target;
            DrawGraph();
            

        }
        private void DrawGraph()
        {
            m_serializeObject = new SerializedObject(m_currentGraph);
            m_currentView = new ND_BehaviorTreeView(m_serializeObject, this);
            m_currentView.graphViewChanged += OnChange;
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
    }
}
