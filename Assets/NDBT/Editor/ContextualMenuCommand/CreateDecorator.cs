using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND_BehaviorTree.Editor
{
    public class CreateDecorator : ContextualMenuManipulator
    {
        public CreateDecorator() : base(evt => OnContextMenu(evt))
        {
        }

        private static void OnContextMenu(ContextualMenuPopulateEvent evt)
        {
            
            evt.menu.AppendAction("Create Decorator", action =>
            {
                Debug.Log("Edge marked");
            });
        }
    }
}
