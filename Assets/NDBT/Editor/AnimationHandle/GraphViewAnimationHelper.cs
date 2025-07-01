using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
namespace ND_BehaviorTree.Editor
{
    public static class GraphViewAnimationHelper
    {
        private const int APPEAR_ANIM_DELAY_MS = 10;
        private const int DISAPPEAR_ANIM_DURATION_MS = 250; // Should match USS

        // --- Appear Animations ---

        public static void AnimateNodeAppear(ND_NodeEditor nodeEditor)
        {
            if (nodeEditor == null) return;

            // Ensure initial styles are applied before triggering transition
            nodeEditor.schedule.Execute(() =>
            {
                if (nodeEditor != null && nodeEditor.parent != null)
                {
                    nodeEditor.AddToClassList("appeared");
                    // Debug.Log($"[AnimHelper] Added 'appeared' to {nodeEditor.title}");
                }
            }).StartingIn(APPEAR_ANIM_DELAY_MS);
        }

        public static async void AnimateNodeAppearWithOvershoot_MultiStage(ND_NodeEditor nodeEditor)
        {
            if (nodeEditor == null || nodeEditor.parent == null) return;
            await Task.Delay(APPEAR_ANIM_DELAY_MS); // Ensure initial styles
            if (nodeEditor == null || nodeEditor.parent == null) return;

            nodeEditor.AddToClassList("scale-to-1");
            await Task.Delay(100);
            if (nodeEditor == null || nodeEditor.parent == null) return;

            nodeEditor.RemoveFromClassList("scale-to-1");
            nodeEditor.AddToClassList("scale-to-1-2-overshoot");
            await Task.Delay(80);
            if (nodeEditor == null || nodeEditor.parent == null) return;

            nodeEditor.RemoveFromClassList("scale-to-1-2-overshoot");
            nodeEditor.AddToClassList("appeared");
            // No need to await the final settle if it's the end state
        }

        // --- Disappear Animation ---

        public static async Task AnimateAndPrepareForRemoval(IEnumerable<ND_NodeEditor> nodesToAnimate)
        {
            List<ND_NodeEditor> validNodes = nodesToAnimate.Where(n => n != null).ToList();
            if (!validNodes.Any()) return;

            foreach (var nodeEditor in validNodes)
            {
                nodeEditor.RemoveFromClassList("appeared");
                nodeEditor.AddToClassList("disappearing"); // Assumes a .disappearing class in USS
                                                          // If not, just removing 'appeared' is fine
            }
            await Task.Delay(DISAPPEAR_ANIM_DURATION_MS);
        }
    }
}
