using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ND_BehaviorTree
{   
    [NodeInfo("Trello", "Trello/TrelloDefault")]
    public class TrelloNode : Node
    {   
        [SerializeReference]
        public List<TrelloChildNode> childrenNode = new List<TrelloChildNode>();
    }

}
