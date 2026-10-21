using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND_BehaviorTree.Editor.CustomGraph
{
    /// <summary>What a <see cref="ND_BTPortPainter"/> draws from: the port and its current state.</summary>
    public readonly struct ND_BTPortState
    {
        public readonly BTPortDirection Direction;
        public readonly BTPortCapacity Capacity;
        /// <summary>The theme's input or output colour.</summary>
        public readonly Color Color;
        public readonly bool IsPlugged;
        public readonly int ConnectionCount;
        /// <summary>Under the mouse (also while a connection is dragged onto it).</summary>
        public readonly bool IsHover;
        /// <summary>A connection is being dragged and this port can receive it.</summary>
        public readonly bool IsCandidate;

        public ND_BTPortState(BTPortDirection direction, BTPortCapacity capacity, Color color, int connectionCount, bool isHover, bool isCandidate)
        {
            Direction = direction;
            Capacity = capacity;
            Color = color;
            ConnectionCount = connectionCount;
            IsPlugged = connectionCount > 0;
            IsHover = isHover;
            IsCandidate = isCandidate;
        }
    }

    /// <summary>
    /// Draws a port of Shape = Custom, like the built-in Cat. Create one with Create > ND_BehaviorTree > Port Painter,
    /// then drag the script into a Port Theme's Custom Painter (Project Settings → ND Behavior Tree, or a mapping of
    /// a Node Appearance Pack). One instance per port, made with the parameterless constructor; it is redrawn whenever
    /// the port's state changes. Can be combined with a Custom Template (the painting goes over the template).
    /// </summary>
    public abstract class ND_BTPortPainter
    {
        /// <summary>Size of the port element. Default: a square of the theme's port size (a wider bar for Multi outputs).</summary>
        public virtual Vector2 GetSize(float portSize, BTPortDirection direction, BTPortCapacity capacity) =>
            new Vector2(capacity == BTPortCapacity.Multi ? portSize * 1.25f : portSize, portSize);

        /// <summary>Paints the port into <paramref name="rect"/> (local space, origin top-left).</summary>
        public abstract void Draw(Painter2D painter, Rect rect, in ND_BTPortState state);

        /// <summary>A painter of <paramref name="type"/>, or null (logged) when it is not a concrete ND_BTPortPainter with a parameterless constructor.</summary>
        public static ND_BTPortPainter Create(Type type, string scriptName)
        {
            if (type == null || type.IsAbstract || !typeof(ND_BTPortPainter).IsAssignableFrom(type))
            {
                Debug.LogError($"[ND_BTPortPainter] Custom Painter '{scriptName}' is not a concrete {nameof(ND_BTPortPainter)} subclass (the class name must match the file name).");
                return null;
            }
            if (type.GetConstructor(Type.EmptyTypes) == null)
            {
                Debug.LogError($"[ND_BTPortPainter] Custom Painter '{type.FullName}' needs a public parameterless constructor.");
                return null;
            }
            return (ND_BTPortPainter)Activator.CreateInstance(type);
        }
    }
}
