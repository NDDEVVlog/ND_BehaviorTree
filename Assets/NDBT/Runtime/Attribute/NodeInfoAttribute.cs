using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ND_BehaviorTree
{
    public class NodeInfoAttribute : Attribute
    {
        private string m_nodeTitle;
        private string m_menuItem;
        private bool m_hasFlowInput;  // New private field
        private bool m_hasFlowOutput; // New private field (corrected typo from Oupput)


        public string title => m_nodeTitle;
        public string menuItem => m_menuItem;
        public bool HasFlowInput => m_hasFlowInput;
        public bool HasFlowOutput => m_hasFlowOutput;

        public NodeInfoAttribute(string title, string menuItem = "", bool hasFlowInput = true, bool hasFlowOutput = true)
        {
            m_nodeTitle = title;
            m_menuItem = menuItem;
            m_hasFlowInput = hasFlowInput;
            m_hasFlowOutput = hasFlowOutput;
        }
    }
}
