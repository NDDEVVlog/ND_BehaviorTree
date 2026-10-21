using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND.GraphUI
{
    /// <summary>What a <see cref="PortPainter"/> draws from: the port and its current state.</summary>
    public readonly struct PortState
    {
        public readonly PortDirection Direction;
        public readonly PortCapacity Capacity;
        public readonly PortOrientation Orientation;
        /// <summary>The theme's input or output colour.</summary>
        public readonly Color Color;
        public readonly bool IsPlugged;
        public readonly int ConnectionCount;
        /// <summary>Under the mouse (also while a connection is dragged onto it).</summary>
        public readonly bool IsHover;
        /// <summary>A connection is being dragged and this port can receive it.</summary>
        public readonly bool IsCandidate;

        public PortState(PortDirection direction, PortCapacity capacity, PortOrientation orientation, Color color, int connectionCount, bool isHover, bool isCandidate)
        {
            Direction = direction;
            Capacity = capacity;
            Orientation = orientation;
            Color = color;
            ConnectionCount = connectionCount;
            IsPlugged = connectionCount > 0;
            IsHover = isHover;
            IsCandidate = isCandidate;
        }
    }

    /// <summary>
    /// Draws a port of Shape = Custom, like the built-in Cat. Assign the script to a Port Theme's Custom Painter.
    /// One instance per port, made with the parameterless constructor; it is redrawn whenever the port's state changes.
    /// Can be combined with a Custom Template (the painting goes over the template).
    /// </summary>
    public abstract class PortPainter
    {
        /// <summary>
        /// Size of the port element for a vertical port (width along the node edge, height across it); horizontal ports
        /// use it transposed. Default: a square of the theme's port size (a wider bar for Multi ports).
        /// </summary>
        public virtual Vector2 GetSize(float portSize, PortDirection direction, PortCapacity capacity) =>
            new Vector2(capacity == PortCapacity.Multi ? portSize * 1.25f : portSize, portSize);

        /// <summary>Paints the port into <paramref name="rect"/> (local space, origin top-left).</summary>
        public abstract void Draw(Painter2D painter, Rect rect, in PortState state);

        /// <summary>A painter of <paramref name="type"/>, or null (logged) when it is not a concrete PortPainter with a parameterless constructor.</summary>
        public static PortPainter Create(Type type, string scriptName)
        {
            if (type == null || type.IsAbstract || !typeof(PortPainter).IsAssignableFrom(type))
            {
                Debug.LogError($"[GraphUI] Custom Painter '{scriptName}' is not a concrete {nameof(PortPainter)} subclass (the class name must match the file name).");
                return null;
            }
            if (type.GetConstructor(Type.EmptyTypes) == null)
            {
                Debug.LogError($"[GraphUI] Custom Painter '{type.FullName}' needs a public parameterless constructor.");
                return null;
            }
            return (PortPainter)Activator.CreateInstance(type);
        }
    }
}
