using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public static class InventoryControllerExtensions
    {
        public static VisualElement GetClosestAncestorWithClass(this InventoryController controller, VisualElement element, string className)
        {
            var parent = element;
            while (parent != null)
            {
                if (parent.ClassListContains(className))
                {
                    Debug.Log("Found element with class " + className + ": " + parent.name);
                    return parent;
                }
                parent = parent.parent;
            }
            Debug.LogWarning("No element with class " + className + " found in hierarchy");
            return null;
        }

        public static InventorySlot GetSlotAtPosition(this InventoryController controller, Vector2 screenPosition)
        {
            var inventoryGrid = controller.GetType().GetField("inventoryGrid", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(controller) as VisualElement;
            var inventoryRows = (int)controller.GetType().GetField("inventoryRows", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(controller);
            var inventoryColumns = (int)controller.GetType().GetField("inventoryColumns", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(controller);
            var slotSize = (float)controller.GetType().GetField("slotSize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(controller);
            var slotGap = (float)controller.GetType().GetField("slotGap", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(controller);
            var slots = controller.GetType().GetField("slots", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(controller) as InventorySlot[,];

            Vector2 localPos = inventoryGrid.WorldToLocal(screenPosition);
            
            int col = Mathf.FloorToInt(localPos.x / (slotSize + slotGap));
            int row = Mathf.FloorToInt(localPos.y / (slotSize + slotGap));

            if (row >= 0 && row < inventoryRows && col >= 0 && col < inventoryColumns)
            {
                return slots[row, col];
            }
            return null;
        }

        public static void MarkSlots(this InventoryController controller, PlacedItem item)
        {
            var inventoryRows = (int)controller.GetType().GetField("inventoryRows", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(controller);
            var inventoryColumns = (int)controller.GetType().GetField("inventoryColumns", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(controller);
            var slots = controller.GetType().GetField("slots", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(controller) as InventorySlot[,];

            if (item == null || item.ItemData == null) return;
            for (int r = 0; r < item.ItemData.boolArray2D.Rows; r++)
            {
                for (int c = 0; c < item.ItemData.boolArray2D.Columns; c++)
                {
                    if (item.ItemData.boolArray2D[r, c])
                    {
                        int gridRow = item.StartRow + r;
                        int gridCol = item.StartCol + c;
                        if (gridRow >= inventoryRows || gridCol >= inventoryColumns) continue;
                        slots[gridRow, gridCol].PlacedItemRef = item;
                    }
                }
            }
        }

        public static void UnmarkSlots(this InventoryController controller, PlacedItem item)
        {
            var inventoryRows = (int)controller.GetType().GetField("inventoryRows", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(controller);
            var inventoryColumns = (int)controller.GetType().GetField("inventoryColumns", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(controller);
            var slots = controller.GetType().GetField("slots", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(controller) as InventorySlot[,];

            if (item == null || item.ItemData == null) return;
            for (int r = 0; r < item.ItemData.boolArray2D.Rows; r++)
            {
                for (int c = 0; c < item.ItemData.boolArray2D.Columns; c++)
                {
                    if (item.ItemData.boolArray2D[r, c])
                    {
                        int gridRow = item.StartRow + r;
                        int gridCol = item.StartCol + c;
                        if (gridRow >= inventoryRows || gridCol >= inventoryColumns) continue;
                        if (slots[gridRow, gridCol].PlacedItemRef == item)
                        {
                            slots[gridRow, gridCol].PlacedItemRef = null;
                        }
                    }
                }
            }
        }
    }