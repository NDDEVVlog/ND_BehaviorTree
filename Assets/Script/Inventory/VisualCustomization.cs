using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
    public class VisualCustomization
    {
        [Header("Container Background")]
        [Tooltip("The background image for the main inventory window.")]
        public Sprite inventoryBackground;

        [Header("Slot Backgrounds")]
        [Tooltip("Default background for an empty slot.")]
        public Sprite slotDefault;
        [Tooltip("Background for a slot when the mouse hovers over it.")]
        public Sprite slotHover;
        [Tooltip("Background for slots where an item can be dropped.")]
        public Sprite slotCanDrop;
        [Tooltip("Background for slots where an item cannot be dropped.")]
        public Sprite slotCannotDrop;
    }
