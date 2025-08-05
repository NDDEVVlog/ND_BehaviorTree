using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ND_BehaviorTree
{   
    public class ComparisonTypeKey: Key<ComparisonType>{

    }
    public enum ComparisonType
    {
        Equal,
        NotEqual,
        Greater,
        GreaterEqual,
        Less,
        LessEqual
    }
}
