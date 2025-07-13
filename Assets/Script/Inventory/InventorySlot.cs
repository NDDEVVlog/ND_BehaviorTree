using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class InventorySlot
    {
        public int Row, Col;
        public VisualElement SlotElement;
        public PlacedItem PlacedItemRef;
        public bool IsOccupied => PlacedItemRef != null;
    }