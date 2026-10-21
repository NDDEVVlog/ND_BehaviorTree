using ND.GraphUI;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND_BehaviorTree.Editor.Tests
{
    /// <summary>Port painter for tests (picked by MonoScript, so it lives in a file of its own name).</summary>
    public class TestPortPainter : PortPainter
    {
        public override Vector2 GetSize(float portSize, PortDirection direction, PortCapacity capacity) =>
            new Vector2(portSize * 3f, portSize * 2f);

        public override void Draw(Painter2D painter, Rect rect, in PortState state) { }
    }
}
