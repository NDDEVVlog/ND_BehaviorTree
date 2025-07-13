using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using ND.Inventory;
using System;

public class InventoryController : MonoBehaviour
{
    [Header("Window Settings")]
    [Tooltip("Controls the position and size of the inventory window on screen.")]
    public WindowSettings windowSettings = new WindowSettings();

    [Header("Visual Customization")]
    [Tooltip("Assign custom sprites for UI elements. If left empty, the default USS styles will be used.")]
    public VisualCustomization visuals = new VisualCustomization();

    [Header("Data")]
    [SerializeField] private InventoryItemScriptableObject itemDatabase;

    [Header("Inventory Settings")]
    [SerializeField] private int inventoryRows = 10;
    [SerializeField] private int inventoryColumns = 5;

    [Header("Grid Settings")]
    [Tooltip("Controls the alignment of the item grid within the inventory window.")]
    public GridSettings gridSettings = new GridSettings();
    [SerializeField] private float slotSize = 50f;
    [SerializeField] private float slotGap = 4f;

    // UI References
    private VisualElement root;
    private VisualElement inventoryContainer;
    private VisualElement inventoryGrid;
    private ScrollView inventoryScrollView;
    private InventoryContextMenu contextMenu;

    // Data Structures
    private InventorySlot[,] slots;
    private PlacedItem currentlyDraggedItem;
    private Vector2 dragStartPosition;
    private Vector2 dragStartOffset;

    // For live Inspector updates
    private Vector2 lastPosition;
    private Vector2 lastSize;

    void OnEnable()
    {
        root = GetComponent<UIDocument>().rootVisualElement;

        inventoryContainer = root.Q<VisualElement>("InventoryContainer");
        inventoryGrid = root.Q<VisualElement>("InventoryGrid");
        inventoryScrollView = root.Q<ScrollView>("InventoryScrollView");

        // Find the InventoryContextMenu component
        contextMenu = FindObjectOfType<InventoryContextMenu>();
        if (contextMenu == null)
        {
            Debug.LogWarning("InventoryContextMenu component not found in the scene. The context menu will not function.");
        }
        else
        {
            contextMenu.Initialize(root);
            contextMenu.OnUseClicked += (item) => Debug.Log($"Used {item?.ItemData.Name}");
            contextMenu.OnDropClicked += (item) => Debug.Log($"Dropped {item?.ItemData.Name}");
            contextMenu.OnRemoveClicked += (item) => RemoveItem(item);
            contextMenu.OnRotateClicked += (item) => RotateItem(item);
        }

        ApplyWindowSettings();

        if (visuals.inventoryBackground != null)
        {
            inventoryContainer.style.backgroundImage = new StyleBackground(visuals.inventoryBackground);
        }

        CreateGrid();
        ApplyGridAlignment();
        SetupEventHandlers();

        TryAddItemToInventoryByID(0);
        TryAddItemToInventoryByID(1);
    }

    private void Update()
    {
        if (inventoryContainer != null && (windowSettings.Position != lastPosition || windowSettings.Size != lastSize))
        {
            ApplyWindowSettings();
        }
    }

    private void ApplyWindowSettings()
    {
        inventoryContainer.style.left = windowSettings.Position.x;
        inventoryContainer.style.top = windowSettings.Position.y;
        inventoryContainer.style.width = windowSettings.Size.x;
        inventoryContainer.style.height = windowSettings.Size.y;

        lastPosition = windowSettings.Position;
        lastSize = windowSettings.Size;
    }

    private void ApplyGridAlignment()
    {
        var contentContainer = inventoryScrollView.contentContainer;
        if (contentContainer == null) return;

        switch (gridSettings.horizontalAlignment)
        {
            case GridSettings.Horizontal.Left: contentContainer.style.justifyContent = Justify.FlexStart; break;
            case GridSettings.Horizontal.Center: contentContainer.style.justifyContent = Justify.Center; break;
            case GridSettings.Horizontal.Right: contentContainer.style.justifyContent = Justify.FlexEnd; break;
        }

        switch (gridSettings.verticalAlignment)
        {
            case GridSettings.Vertical.Top: contentContainer.style.alignContent = Align.FlexStart; break;
            case GridSettings.Vertical.Middle: contentContainer.style.alignContent = Align.Center; break;
            case GridSettings.Vertical.Bottom: contentContainer.style.alignContent = Align.FlexEnd; break;
        }
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

            if (visuals.slotHover != null)
            {
                slotElement.RegisterCallback<MouseEnterEvent>(evt =>
                {
                    if (currentlyDraggedItem == null)
                    {
                        (evt.target as VisualElement).style.backgroundImage = new StyleBackground(visuals.slotHover);
                    }
                });
                slotElement.RegisterCallback<MouseLeaveEvent>(evt =>
                {
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

    #region Event Handlers
    private void SetupEventHandlers()
    {
        root.RegisterCallback<PointerDownEvent>(OnPointerDown);
        root.RegisterCallback<PointerMoveEvent>(OnPointerMove);
        root.RegisterCallback<PointerUpEvent>(OnPointerUp);

        root.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (contextMenu != null && contextMenu.IsVisible && !contextMenu.ContainsPoint(evt.position))
            {
                contextMenu.Hide();
            }
        });
    }

    private void OnPointerDown(PointerDownEvent evt)
    {
        if (evt.button != 0) return;

        var itemElement = this.GetClosestAncestorWithClass(evt.target as VisualElement, "inventory-item");
        if (itemElement != null)
        {
            StartDrag(evt, itemElement);
        }
    }

    private void OnPointerUp(PointerUpEvent evt)
    {
        if (currentlyDraggedItem != null)
        {
            StopDrag(evt);
            ClearSlotHighlights();
            return;
        }

        if (evt.button == 1)
        {
            Debug.Log("Right-click detected at position: " + evt.position);
            var itemElement = this.GetClosestAncestorWithClass(evt.target as VisualElement, "inventory-item");
            if (itemElement != null)
            {
                Debug.Log("Right-clicked on inventory-item: " + itemElement.name);
                if (contextMenu != null)
                {
                    contextMenu.Show(itemElement, evt.position);
                }
                else
                {
                    Debug.LogWarning("InventoryContextMenu is not assigned or not found.");
                }
            }
            else
            {
                Debug.LogWarning("No inventory-item found at click position");
            }
        }
    }

    private void OnPointerMove(PointerMoveEvent evt)
    {
        if (currentlyDraggedItem == null) return;

        HighlightSlotsUnderDrag();
        Vector2 currentPos = currentlyDraggedItem.ItemElement.parent.WorldToLocal(evt.position);
        currentlyDraggedItem.ItemElement.style.left = currentPos.x - dragStartOffset.x;
        currentlyDraggedItem.ItemElement.style.top = currentPos.y - dragStartOffset.y;
    }
    #endregion

    private void HighlightSlotsUnderDrag()
    {
        ClearSlotHighlights();
        var targetSlot = this.GetSlotAtPosition(currentlyDraggedItem.ItemElement.LocalToWorld(Vector2.zero));
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
                slotElement.RemoveFromClassList("slot-highlight-can-drop");
                slotElement.RemoveFromClassList("slot-highlight-cannot-drop");
                slotElement.style.backgroundImage = (visuals.slotDefault != null) ? new StyleBackground(visuals.slotDefault) : null;
            }
        }
    }

    #region API, Placement, Drag & Drop, Item Actions
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
        itemElement.style.left = slots[startRow, startCol].SlotElement.layout.position.x;
        itemElement.style.top = slots[startRow, startCol].SlotElement.layout.position.y;

        if (item.ItemSprite != null)
        {
            var icon = new Image { sprite = item.ItemSprite, scaleMode = ScaleMode.ScaleToFit };
            icon.AddToClassList("inventory-item-icon");
            itemElement.Add(icon);
        }

        placedItem.ItemElement = itemElement;
        itemElement.userData = placedItem;

        inventoryGrid.Add(itemElement);

        this.MarkSlots(placedItem);
    }

    private void RemoveItem(PlacedItem itemToRemove)
    {
        if (itemToRemove == null) return;
        this.UnmarkSlots(itemToRemove);
        itemToRemove.ItemElement.RemoveFromHierarchy();
        if (contextMenu != null) contextMenu.Hide();
    }

    private void StartDrag(PointerDownEvent evt, VisualElement itemElement)
    {
        currentlyDraggedItem = (PlacedItem)itemElement.userData;
        currentlyDraggedItem.ItemElement.AddToClassList("inventory-item--dragging");

        this.UnmarkSlots(currentlyDraggedItem);

        dragStartPosition = new Vector2(currentlyDraggedItem.ItemElement.layout.position.x, currentlyDraggedItem.ItemElement.layout.position.y);
        dragStartOffset = itemElement.WorldToLocal(evt.position);

        itemElement.BringToFront();
    }

    private void StopDrag(PointerUpEvent evt)
    {
        var targetSlot = this.GetSlotAtPosition(evt.position);
        if (targetSlot != null && CanPlaceItemAt(currentlyDraggedItem.ItemData, targetSlot.Row, targetSlot.Col))
        {
            UpdateItemPosition(currentlyDraggedItem, targetSlot.Row, targetSlot.Col);
        }
        else
        {
            currentlyDraggedItem.ItemElement.style.left = dragStartPosition.x;
            currentlyDraggedItem.ItemElement.style.top = dragStartPosition.y;
            this.MarkSlots(currentlyDraggedItem);
        }

        currentlyDraggedItem.ItemElement.RemoveFromClassList("inventory-item--dragging");
        currentlyDraggedItem = null;
    }

    private void RotateItem(PlacedItem itemToRotate)
    {
        this.UnmarkSlots(itemToRotate);
        itemToRotate.ItemData.boolArray2D.Rotate(true);

        if (CanPlaceItemAt(itemToRotate.ItemData, itemToRotate.StartRow, itemToRotate.StartCol))
        {
            UpdateItemPosition(itemToRotate, itemToRotate.StartRow, itemToRotate.StartCol);
        }
        else
        {
            itemToRotate.ItemData.boolArray2D.Rotate(false);
            this.MarkSlots(itemToRotate);
            Debug.Log("Cannot rotate item here.");
        }

        if (contextMenu != null) contextMenu.Hide();
    }

    private void UpdateItemPosition(PlacedItem item, int newRow, int newCol)
    {
        item.StartRow = newRow;
        item.StartCol = newCol;
        item.ItemElement.style.left = slots[newRow, newCol].SlotElement.layout.position.x;
        item.ItemElement.style.top = slots[newRow, newCol].SlotElement.layout.position.y;

        item.ItemElement.style.width = item.ItemData.boolArray2D.Columns * (slotSize + slotGap) - slotGap;
        item.ItemElement.style.height = item.ItemData.boolArray2D.Rows * (slotSize + slotGap) - slotGap;

        this.MarkSlots(item);
    }
    #endregion
}