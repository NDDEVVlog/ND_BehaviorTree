// --- FILE: ND_BehaviorTreeView.SearchAndMenu.cs ---

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND_BehaviorTree.Editor
{
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

            List<DropdownMenuItem> items = evt.menu.MenuItems();


            void RemoveActionByName(string name, out int index)
            {   
                index = -1;
                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i] is DropdownMenuAction action && action.name == name)
                    {
                        items.RemoveAt(i);
                        index = i;
                        break;
                    }
                }
            }
            int PasteIndex, CopyIndex;

            RemoveActionByName("Paste",out PasteIndex);
            RemoveActionByName("Copy",out CopyIndex);

            if (evt.target is GraphElement graphElement)
            {
                if (!selection.Contains(graphElement))
                {
                    ClearSelection();
                    AddToSelection(graphElement);
                }
            }

            Vector2 graphMousePosition = evt.localMousePosition;
            Vector2 screenMousePosition = GUIUtility.GUIToScreenPoint(evt.mousePosition);

            evt.menu.AppendAction("Create Node...", (a) => OpenSearchWindow(screenMousePosition));

            // --- MODIFIED PASTE ACTION ---
            // This now calls a special debug function to check if pasting is allowed.
            evt.menu.AppendAction("Test Log", (a) => Debug.Log("Test clicked!"));

            evt.menu.InsertAction(PasteIndex >= 0 ? PasteIndex : items.Count, "Paste", (a) =>
            {
                PasteFromClipboard(screenMousePosition );
            });

            if (selection.Any())
            {
                evt.menu.AppendSeparator();
                evt.menu.InsertAction(CopyIndex >= 0 ? CopyIndex : items.Count, "Copy", (a) =>
                {
                    
                    CopySelectionToClipboard();
                },
                (e) => selection.OfType<ND_NodeEditor>().Any() ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);

                evt.menu.AppendAction("Delete", (a) => OnDeleteSelectionKeyPressed("Delete", AskUser.DontAskUser),
                    (e) => selection.Any() ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            }
            
            if (evt.target is ND_NodeEditor clickedEditor && clickedEditor.node is CompositeNode compositeNode)
            {
                evt.menu.AppendSeparator();
                evt.menu.AppendAction("Add Service...", (a) =>
                {
                    Debug.Log("Add Service");
                    OpenChildNodeSearchWindow(evt.mousePosition, compositeNode, typeof(ServiceNode));
                });
                
            }
        }
    }
}