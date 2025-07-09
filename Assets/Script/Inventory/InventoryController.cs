// --- START OF FILE InventoryController.cs (MODIFIED) ---
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using ND.Inventory;
using System;
using System.Collections;

public class InventoryController : MonoBehaviour
{
    // --- A class to hold window settings for a clean Inspector ---
    [Serializable]
    public class WindowSettings
    {
        public Vector2 Position = new Vector2(50, 50);
        public Vector2 Size = new Vector2(600, 400);
    }

    // --- NEW: A class to hold all visual customization sprites ---
    [Serializable]
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


    [Header("Window Settings")]
    [Tooltip("Controls the position and size of the inventory window on screen.")]
    public WindowSettings windowSettings = new WindowSettings();

    // --- NEW: Inspector field for all our custom sprites ---
    [Header("Visual Customization")]
    [Tooltip("Assign custom sprites for UI elements. If left empty, the default USS styles will be used.")]
    public VisualCustomization visuals = new VisualCustomization();

    [Header("Data")]
    [SerializeField] private InventoryItemScriptableObject itemDatabase;

    [Header("Inventory Settings")]
    [SerializeField] private int inventoryRows = 10;
    [SerializeField] private int inventoryColumns = 5;
    
    [Header("Grid Visuals")]
    [SerializeField] private float slotSize = 50f;
    [SerializeField] private float slotGap = 4f;

    // --- UI References ---
    private VisualElement root;
    private VisualElement inventoryContainer;
    private VisualElement inventoryGrid;
    private ScrollView inventoryScrollView;
    private VisualElement contextMenu;
    
    // --- Data Structures ---
    private class InventorySlot
    {
        public int Row, Col;
        public VisualElement SlotElement;
        public PlacedItem PlacedItemRef;
        public bool IsOccupied => PlacedItemRef != null;
    }
    private InventorySlot[,] slots;
    private PlacedItem currentlyDraggedItem;
    private Vector2 dragStartPosition;
    private Vector2 dragStartOffset;
    private PlacedItem rightClickedItem;


    void OnEnable()
    {
        root = GetComponent<UIDocument>().rootVisualElement;
        
        // Get references to all our UI elements
        inventoryContainer = root.Q<VisualElement>("InventoryContainer");
        inventoryGrid = root.Q<VisualElement>("InventoryGrid");
        inventoryScrollView = root.Q<ScrollView>("InventoryScrollView");
        contextMenu = root.Q<VisualElement>("ContextMenu");

        // Apply window position and size from the Inspector
        inventoryContainer.style.left = windowSettings.Position.x;
        inventoryContainer.style.top = windowSettings.Position.y;
        inventoryContainer.style.width = windowSettings.Size.x;
        inventoryContainer.style.height = windowSettings.Size.y;

        // --- NEW: Apply custom inventory background if provided ---
        if (visuals.inventoryBackground != null)
        {
            inventoryContainer.style.backgroundImage = new StyleBackground(visuals.inventoryBackground);
        }

        CreateGrid();
        SetupEventHandlers();
        
        // Example: Add some items for testing
        TryAddItemToInventoryByID(0);
        TryAddItemToInventoryByID(1);
    }

    private void CreateGrid()
    {
        slots = new InventorySlot[inventoryRows, inventoryColumns];
        inventoryGrid.Clear();

        inventoryGrid.style.width = (inventoryColumns + 1) * (slotSize + slotGap);

        int totalSlots = inventoryRows * inventoryColumns;
        for (int i = 0; i < totalSlots; i++)
        {
            int row = i / inventoryColumns;
            int col = i % inventoryColumns;

            var slotElement = new VisualElement();
            slotElement.AddToClassList("inventory-slot");

            // --- NEW: Apply custom default slot sprite ---
            if (visuals.slotDefault != null)
            {
                slotElement.style.backgroundImage = new StyleBackground(visuals.slotDefault);
            }
            
            slotElement.style.width = slotSize;
            slotElement.style.height = slotSize;
            slotElement.style.marginRight = slotGap / 2;
            slotElement.style.marginLeft = slotGap / 2;
            slotElement.style.marginTop = slotGap / 2;
            slotElement.style.marginBottom = slotGap / 2;
            
            // --- NEW: Setup hover events for custom sprites if they exist ---
            if (visuals.slotHover != null)
            {
                slotElement.RegisterCallback<MouseEnterEvent>(evt =>
                {
                    // Only show hover effect if not currently dragging an item
                    if (currentlyDraggedItem == null)
                    {
                        (evt.target as VisualElement).style.backgroundImage = new StyleBackground(visuals.slotHover);
                    }
                });
                slotElement.RegisterCallback<MouseLeaveEvent>(evt =>
                {
                    // Revert to default, but only if it's not currently highlighted for dropping.
                    // The ClearSlotHighlights method will handle resetting after a drag.
                    if (currentlyDraggedItem == null)
                    {
                        (evt.target as VisualElement).style.backgroundImage = (visuals.slotDefault != null) ? new StyleBackground(visuals.slotDefault) : null;
                    }
                });
            }

            inventoryGrid.Add(slotElement);
            slots[row, col] = new InventorySlot { Row = row, Col = col, SlotElement = slotElement };
        }
    }
    
    // --- FROM HERE DOWN, ONLY METHODS RELATED TO HIGHLIGHTING HAVE BEEN MODIFIED ---
    // --- The vast majority of the code remains the same as your original version. ---

    private void HighlightSlotsUnderDrag()
    {
        ClearSlotHighlights();
        var targetSlot = GetSlotAtPosition(currentlyDraggedItem.ItemElement.LocalToWorld(Vector2.zero));
        if (targetSlot == null) return;

        bool canDrop = CanPlaceItemAt(currentlyDraggedItem.ItemData, targetSlot.Row, targetSlot.Col);
        var item = currentlyDraggedItem.ItemData;
        
        for (int r = 0; r < item.boolArray2D.Rows; r++)
        {
            for (int c = 0; c < item.boolArray2D.Columns; c++)
            {
                if (item.boolArray2D[r, c])
                {
                    int gridRow = targetSlot.Row + r;
                    int gridCol = targetSlot.Col + c;
                    if (gridRow < inventoryRows && gridCol < inventoryColumns)
                    {
                        VisualElement currentSlotElement = slots[gridRow, gridCol].SlotElement;
                        // --- MODIFIED: Use custom sprites if available, otherwise use USS classes ---
                        if (canDrop)
                        {
                            if (visuals.slotCanDrop != null)
                                currentSlotElement.style.backgroundImage = new StyleBackground(visuals.slotCanDrop);
                            else
                                currentSlotElement.AddToClassList("slot-highlight-can-drop");
                        }
                        else
                        {
                            if (visuals.slotCannotDrop != null)
                                currentSlotElement.style.backgroundImage = new StyleBackground(visuals.slotCannotDrop);
                            else
                                currentSlotElement.AddToClassList("slot-highlight-cannot-drop");
                        }
                    }
                }
            }
        }
    }
    
    private void ClearSlotHighlights()
    {
        for (int r = 0; r < inventoryRows; r++)
        {
            for (int c = 0; c < inventoryColumns; c++)
            {
                var slotElement = slots[r, c].SlotElement;
                // --- MODIFIED: Remove classes AND reset background image to default sprite (or none) ---
                slotElement.RemoveFromClassList("slot-highlight-can-drop");
                slotElement.RemoveFromClassList("slot-highlight-cannot-drop");
                slotElement.style.backgroundImage = (visuals.slotDefault != null) ? new StyleBackground(visuals.slotDefault) : null;
            }
        }
    }
    
    #region (UNCHANGED) Setup, API, Placement, Drag & Drop, Item Actions, Helpers...
    // All other methods from your original script are included below without any changes.
    // ...
    private void SetupEventHandlers()
    {
        root.RegisterCallback<PointerDownEvent>(OnPointerDown);
        root.RegisterCallback<PointerMoveEvent>(OnPointerMove);
        root.RegisterCallback<PointerUpEvent>(OnPointerUp);

        root.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (contextMenu.style.display == DisplayStyle.Flex && !contextMenu.worldBound.Contains(evt.position))
            {
                contextMenu.style.display = DisplayStyle.None;
            }
        });

        contextMenu.Q<Button>("UseButton").clicked += () => Debug.Log($"Used {rightClickedItem?.ItemData.Name}");
        contextMenu.Q<Button>("DropButton").clicked += () => Debug.Log($"Dropped {rightClickedItem?.ItemData.Name}");
        contextMenu.Q<Button>("RemoveButton").clicked += () => RemoveItem(rightClickedItem);
        contextMenu.Q<Button>("RotateButton").clicked += () => RotateItem(rightClickedItem);
    }
    
    public bool TryAddItemToInventoryByID(int id, int amount = 1)
    {
        ItemMapper itemMapper = itemDatabase.itemMappers.Find(im => im.ID == id);
        if (itemMapper == null)
        {
            Debug.LogError($"Item with ID {id} not found in database.");
            return false;
        }

        Item itemToAdd = new Item(id, itemMapper);

        for (int i = 0; i < amount; i++)
        {
            if (!TryPlaceItemInFirstAvailableSlot(itemToAdd))
            {
                Debug.LogWarning($"Inventory is full. Could not add item ID {id}.");
                return false;
            }
        }
        return true;
    }
    
    #region Item Placement and Removal

    private bool TryPlaceItemInFirstAvailableSlot(Item item)
    {
        for (int r = 0; r < inventoryRows; r++)
        {
            for (int c = 0; c < inventoryColumns; c++)
            {
                if (CanPlaceItemAt(item, r, c))
                {
                    PlaceItem(item, r, c);
                    return true;
                }
            }
        }
        return false;
    }
    
    private bool CanPlaceItemAt(Item item, int startRow, int startCol)
    {
        for (int r = 0; r < item.boolArray2D.Rows; r++)
        {
            for (int c = 0; c < item.boolArray2D.Columns; c++)
            {
                if (item.boolArray2D[r, c])
                {
                    int gridRow = startRow + r;
                    int gridCol = startCol + c;

                    if (gridRow >= inventoryRows || gridCol >= inventoryColumns)
                        return false; 

                    if (slots[gridRow, gridCol].IsOccupied && slots[gridRow, gridCol].PlacedItemRef != currentlyDraggedItem)
                        return false;
                }
            }
        }
        return true;
    }

    private void PlaceItem(Item item, int startRow, int startCol)
    {
        var placedItem = new PlacedItem(item, startRow, startCol);
        
        VisualElement itemElement = new VisualElement();
        
        itemElement.AddToClassList("inventory-item");
        itemElement.style.width = item.boolArray2D.Columns * (slotSize + slotGap) - slotGap;
        itemElement.style.height = item.boolArray2D.Rows * (slotSize + slotGap) - slotGap;
        itemElement.style.left = slots[startRow,startCol].SlotElement.layout.position.x;
        itemElement.style.top = slots[startRow,startCol].SlotElement.layout.position.y;
        
        if (item.ItemSprite != null)
        {
            var icon = new Image { sprite = item.ItemSprite, scaleMode = ScaleMode.ScaleToFit };
            icon.AddToClassList("inventory-item-icon");
            itemElement.Add(icon);
        }
        
        placedItem.ItemElement = itemElement;
        itemElement.userData = placedItem;
        
        inventoryGrid.Add(itemElement);

        MarkSlots(placedItem);
    }

    private void RemoveItem(PlacedItem itemToRemove)
    {
        if (itemToRemove == null) return;
        UnmarkSlots(itemToRemove);
        itemToRemove.ItemElement.RemoveFromHierarchy();
        contextMenu.style.display = DisplayStyle.None;
    }
    
    #endregion
    
    #region Drag, Drop and Context Menu

    private void OnPointerDown(PointerDownEvent evt)
    {
        var target = evt.target as VisualElement;
        if (target == null) return;
        
        var itemElement = GetClosestAncestorWithClass(target, "inventory-item");

        if (itemElement != null && itemElement.userData is PlacedItem)
        {
            if (evt.button == 0) // Left-click for drag
            {
                StartDrag(evt, itemElement);
            }
            else if (evt.button == 1) // Right-click for context menu
            {
                ShowContextMenu(evt, itemElement);
            }
        }
    }

    private void OnPointerMove(PointerMoveEvent evt)
    {
        if (currentlyDraggedItem == null) return;

        Vector2 currentPos = currentlyDraggedItem.ItemElement.parent.WorldToLocal(evt.position);
        currentlyDraggedItem.ItemElement.style.left = currentPos.x - dragStartOffset.x;
        currentlyDraggedItem.ItemElement.style.top = currentPos.y - dragStartOffset.y;

        HighlightSlotsUnderDrag();
    }

    private void OnPointerUp(PointerUpEvent evt)
    {
        if (currentlyDraggedItem == null) return;
        
        StopDrag(evt);
        ClearSlotHighlights();
    }
    
    private void StartDrag(PointerDownEvent evt, VisualElement itemElement)
    {
        currentlyDraggedItem = (PlacedItem)itemElement.userData;
        currentlyDraggedItem.ItemElement.AddToClassList("inventory-item--dragging");
        
        UnmarkSlots(currentlyDraggedItem);
        
        dragStartPosition = new Vector2(currentlyDraggedItem.ItemElement.layout.position.x,currentlyDraggedItem.ItemElement.layout.position.y);
        dragStartOffset = itemElement.WorldToLocal(evt.position);

        itemElement.BringToFront();
    }

    private void StopDrag(PointerUpEvent evt)
    {
        var targetSlot = GetSlotAtPosition(evt.position);
        if (targetSlot != null && CanPlaceItemAt(currentlyDraggedItem.ItemData, targetSlot.Row, targetSlot.Col))
        {
            UpdateItemPosition(currentlyDraggedItem, targetSlot.Row, targetSlot.Col);
        }
        else
        {
            currentlyDraggedItem.ItemElement.style.left = dragStartPosition.x;
            currentlyDraggedItem.ItemElement.style.top = dragStartPosition.y;
            MarkSlots(currentlyDraggedItem);
        }
        
        currentlyDraggedItem.ItemElement.RemoveFromClassList("inventory-item--dragging");
        currentlyDraggedItem = null;
    }
    
    private void ShowContextMenu(PointerDownEvent evt, VisualElement itemElement)
    {
        rightClickedItem = (PlacedItem)itemElement.userData;
        var itemToggles = rightClickedItem.ItemData.toggles;
        
        contextMenu.style.display = DisplayStyle.Flex;
        contextMenu.style.left = evt.position.x;
        contextMenu.style.top = evt.position.y;
        
        contextMenu.Q<Button>("UseButton").style.display = itemToggles.isUsable ? DisplayStyle.Flex : DisplayStyle.None;
        contextMenu.Q<Button>("DropButton").style.display = itemToggles.isDroppable ? DisplayStyle.Flex : DisplayStyle.None;
        contextMenu.Q<Button>("RemoveButton").style.display = itemToggles.isRemovable ? DisplayStyle.Flex : DisplayStyle.None;
        contextMenu.Q<Button>("RotateButton").style.display = DisplayStyle.Flex;
    }
    
    #endregion
    
    #region Item Actions

    private void RotateItem(PlacedItem itemToRotate)
    {
        UnmarkSlots(itemToRotate);
        itemToRotate.ItemData.boolArray2D.Rotate(true);
        
        if (CanPlaceItemAt(itemToRotate.ItemData, itemToRotate.StartRow, itemToRotate.StartCol))
        {
            UpdateItemPosition(itemToRotate, itemToRotate.StartRow, itemToRotate.StartCol);
        }
        else
        {
            itemToRotate.ItemData.boolArray2D.Rotate(false);
            MarkSlots(itemToRotate);
            Debug.Log("Cannot rotate item here.");
        }
        
        contextMenu.style.display = DisplayStyle.None;
    }

    #endregion

    #region Helper & Utility Methods

    private VisualElement GetClosestAncestorWithClass(VisualElement element, string className)
    {
        var parent = element;
        while (parent != null)
        {
            if (parent.ClassListContains(className))
            {
                return parent;
            }
            parent = parent.parent;
        }
        return null;
    }
    
    private InventorySlot GetSlotAtPosition(Vector2 screenPosition)
    {
        Vector2 localPos = inventoryGrid.WorldToLocal(screenPosition);
        
        int col = Mathf.FloorToInt(localPos.x / (slotSize + slotGap));
        int row = Mathf.FloorToInt(localPos.y / (slotSize + slotGap));

        if (row >= 0 && row < inventoryRows && col >= 0 && col < inventoryColumns)
        {
            return slots[row, col];
        }
        return null;
    }
    
    private void UpdateItemPosition(PlacedItem item, int newRow, int newCol)
    {
        item.StartRow = newRow;
        item.StartCol = newCol;
        item.ItemElement.style.left = slots[newRow, newCol].SlotElement.layout.position.x;
        item.ItemElement.style.top = slots[newRow, newCol].SlotElement.layout.position.y;
        
        item.ItemElement.style.width = item.ItemData.boolArray2D.Columns * (slotSize + slotGap) - slotGap;
        item.ItemElement.style.height = item.ItemData.boolArray2D.Rows * (slotSize + slotGap) - slotGap;
        
        MarkSlots(item);
    }
    
    private void UnmarkSlots(PlacedItem item)
    {
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

    private void MarkSlots(PlacedItem item)
    {
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

    private class PlacedItem
    {
        public Item ItemData;
        public int StartRow;
        public int StartCol;
        public VisualElement ItemElement;
        public PlacedItem(Item item, int row, int col) { ItemData = item; StartRow = row; StartCol = col; }
    }
    
    #endregion
    #endregion
}