using System.Collections;
using System.Collections.Generic;
using UnityEngine;

    [System.Serializable]
    public class GridSettings
    {
        public enum Horizontal { Left, Center, Right }
        public enum Vertical { Top, Middle, Bottom }

        [Tooltip("Horizontal alignment of the grid within the scroll view.")]
        public Horizontal horizontalAlignment = Horizontal.Left;
        [Tooltip("Vertical alignment of the grid within the scroll view.")]
        public Vertical verticalAlignment = Vertical.Top;
    }