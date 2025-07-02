// --- START OF FILE ND_BehaviorTreeView.SearchAndMenu.cs ---

using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND_BehaviorTree.Editor
{
    /// <summary>
    /// This partial class handles the search window and contextual menu functionality
    /// for the Behavior Tree View.
    /// </summary>
    public partial class ND_BehaviorTreeView
    {
        public void OpenSearchWindow(Vector2 screenPosition)
        {   
            m_searchProvider.Initialize(this); 
            SearchWindow.Open(new SearchWindowContext(screenPosition, 300, 200), m_searchProvider); 
        }
        
        public void OpenChildNodeSearchWindow(Vector2 screenPosition, CompositeNode parent, Type filterType)
        {
            m_searchProvider.Initialize(this, parent, filterType);
            SearchWindow.Open(new SearchWindowContext(screenPosition, 300, 200), m_searchProvider);
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            base.BuildContextualMenu(evt);

            Vector2 screenPoint = GUIUtility.GUIToScreenPoint(evt.mousePosition);
            
            if (evt.target is ND_NodeEditor clickedEditor && clickedEditor.m_Node is CompositeNode compositeNode)
            {
                evt.menu.AppendAction("Add Decorator...", (a) => OpenChildNodeSearchWindow(screenPoint, compositeNode, typeof(DecoratorNode)));
                evt.menu.AppendAction("Add Service...", (a) => OpenChildNodeSearchWindow(screenPoint, compositeNode, typeof(ServiceNode)));
            }
            else
            {
                evt.menu.AppendAction("Create Node", (a) => OpenSearchWindow(evt.mousePosition));
            }
        }
        
    }
}
// --- END OF FILE ND_BehaviorTreeView.SearchAndMenu.cs ---