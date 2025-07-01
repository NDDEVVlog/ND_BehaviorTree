using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ND_BehaviorTree
{
    [System.Serializable]
    public struct ND_BTConnection
    {   
        public string edgeText ;
        public ND_BTConectionPort inputPort;
        public ND_BTConectionPort outputPort;

        public ND_BTConnection(ND_BTConectionPort inputPort, ND_BTConectionPort outputPort)
        {
            edgeText = "";
            this.inputPort = inputPort;
            this.outputPort = outputPort;
        }
        public ND_BTConnection(string inputId, int inputIndex, string outputID, int outputIndex)
        {   
            edgeText = "";
            inputPort = new ND_BTConectionPort(inputId, inputIndex);
            outputPort = new ND_BTConectionPort(outputID, outputIndex);
        }
    }
    [System.Serializable]
    public struct ND_BTConectionPort
    {
        public string nodeID;
        public int portIndex;

        public ND_BTConectionPort(string id, int index)
        {
            this.nodeID = id;
            this.portIndex = index;
        }
    }
}
