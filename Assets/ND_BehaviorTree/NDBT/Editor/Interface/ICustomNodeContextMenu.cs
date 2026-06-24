using UnityEngine.UIElements;

namespace ND_BehaviorTree.Editor
{
    public interface ICustomNodeContextMenu
    {
        void AppendContextMenu(ContextualMenuPopulateEvent evt, ND_BehaviorTree.Editor.CustomGraph.ND_BTNodeView view);
    }
}