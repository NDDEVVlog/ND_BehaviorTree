// --- MODIFIED FILE: ND_BehaviorTreeView.SearchAndMenu.cs ---

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
            // Add our custom "Create Node" which uses the advanced search window.
            evt.menu.AppendAction("Create Node...", (a) => OpenSearchWindow(GUIUtility.GUIToScreenPoint(evt.mousePosition)));

            // Add a separator before the actions that require a selection.
            if (selection.Any())
            {
                evt.menu.AppendSeparator();
                
                // Add the "Delete" action, which triggers the same logic as the Delete key.
                // It's disabled if nothing is selected.
                evt.menu.AppendAction("Delete", (a) => OnDeleteSelectionKeyPressed("Delete", AskUser.DontAskUser), 
                    (e) => selection.Any() ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            }
            
            // If the user right-clicked on a Composite Node, add the special option to attach a Service.
            if (evt.target is ND_NodeEditor clickedEditor && clickedEditor.node is CompositeNode compositeNode)
            {
                evt.menu.AppendSeparator();
                evt.menu.AppendAction("Add Service...", (a) => OpenChildNodeSearchWindow(GUIUtility.GUIToScreenPoint(evt.mousePosition), compositeNode, typeof(ServiceNode)));
            }
        }
    }
}