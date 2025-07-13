using System.Collections;
using System.Collections.Generic;
using ND.Inventory;
using UnityEngine;
using UnityEngine.UIElements;

public class PlacedItem
    {
        public Item ItemData;
        public int StartRow;
        public int StartCol;
        public VisualElement ItemElement;

        public PlacedItem(Item item, int row, int col)
        {
            ItemData = item;
            StartRow = row;
            StartCol = col;
        }
    }