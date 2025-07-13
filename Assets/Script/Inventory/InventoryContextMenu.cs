using UnityEngine;
using UnityEngine.UIElements;
using System;

namespace ND.Inventory
{
    public class InventoryContextMenu : MonoBehaviour
    {
        [SerializeField] private VisualTreeAsset contextMenuAsset;
        private VisualElement contextMenu;
        private VisualElement root;
        private PlacedItem rightClickedItem;

        public Action<PlacedItem> OnUseClicked { get; set; }
        public Action<PlacedItem> OnDropClicked { get; set; }
        public Action<PlacedItem> OnRemoveClicked { get; set; }
        public Action<PlacedItem> OnRotateClicked { get; set; }

        public bool IsVisible => contextMenu != null && contextMenu.style.display == DisplayStyle.Flex;

        void Awake()
        {
            // Ensure the component is ready, but don't initialize UI yet
            if (contextMenuAsset == null)
            {
                Debug.LogWarning("ContextMenuAsset is not assigned in the InventoryContextMenu inspector. The context menu will not function.");
            }
        }

        public void Initialize(VisualElement rootVisualElement)
        {
            this.root = rootVisualElement;
            if (contextMenuAsset != null)
            {
                contextMenu = contextMenuAsset.Instantiate();
                contextMenu.style.display = DisplayStyle.None;
                root.Add(contextMenu);
                
                // Setup button event handlers
                contextMenu.Q<Button>("UseButton").clicked += () => OnUseClicked?.Invoke(rightClickedItem);
                contextMenu.Q<Button>("DropButton").clicked += () => OnDropClicked?.Invoke(rightClickedItem);
                contextMenu.Q<Button>("RemoveButton").clicked += () => OnRemoveClicked?.Invoke(rightClickedItem);
                contextMenu.Q<Button>("RotateButton").clicked += () => OnRotateClicked?.Invoke(rightClickedItem);
            }
            else
            {
                Debug.LogError("ContextMenuAsset is null in InventoryContextMenu. Ensure it is assigned in the Inspector.");
            }
        }

        public void Show(VisualElement itemElement, Vector2 position)
        {
            if (contextMenu == null)
            {
                Debug.LogError("ContextMenu is null. Ensure ContextMenuAsset is assigned and Initialize was called.");
                return;
            }

            rightClickedItem = (PlacedItem)itemElement.userData;
            var itemToggles = rightClickedItem.ItemData.toggles;

            contextMenu.style.display = DisplayStyle.Flex;
            contextMenu.style.left = position.x;
            contextMenu.style.top = position.y;
            
            contextMenu.style.minWidth = 100;
            contextMenu.style.minHeight = 100;
            contextMenu.style.opacity = 1;

            Debug.Log($"Context menu styles - display: {contextMenu.style.display}, position: ({contextMenu.resolvedStyle.left}, {contextMenu.resolvedStyle.top}), size: ({contextMenu.resolvedStyle.width}, {contextMenu.resolvedStyle.height})");
            Debug.Log($"Root size: ({root.resolvedStyle.width}, {root.resolvedStyle.height})");
            Debug.Log($"Toggle states - isUsable: {itemToggles.isUsable}, isDroppable: {itemToggles.isDroppable}, isRemovable: {itemToggles.isRemovable}");

            float menuWidth = contextMenu.resolvedStyle.width > 0 ? contextMenu.resolvedStyle.width : 100;
            float menuHeight = contextMenu.resolvedStyle.height > 0 ? contextMenu.resolvedStyle.height : 100;
            float screenWidth = Screen.width;
            float screenHeight = Screen.height;

            if (float.IsNaN(root.resolvedStyle.width) || root.resolvedStyle.width <= 0)
            {
                Debug.LogWarning("Root width is invalid, using Screen.width: " + screenWidth);
            }
            else
            {
                screenWidth = root.resolvedStyle.width;
            }

            if (float.IsNaN(root.resolvedStyle.height) || root.resolvedStyle.height <= 0)
            {
                Debug.LogWarning("Root height is invalid, using Screen.height: " + screenHeight);
            }
            else
            {
                screenHeight = root.resolvedStyle.height;
            }

            if (position.x + menuWidth > screenWidth)
                contextMenu.style.left = screenWidth - menuWidth;
            if (position.y + menuHeight > screenHeight)
                contextMenu.style.top = screenHeight - menuHeight;

            Debug.Log($"Adjusted context menu position: ({contextMenu.style.left.value}, {contextMenu.style.top.value})");

            contextMenu.Q<Button>("UseButton").style.display = itemToggles.isUsable ? DisplayStyle.Flex : DisplayStyle.None;
            contextMenu.Q<Button>("DropButton").style.display = itemToggles.isDroppable ? DisplayStyle.Flex : DisplayStyle.None;
            contextMenu.Q<Button>("RemoveButton").style.display = itemToggles.isRemovable ? DisplayStyle.Flex : DisplayStyle.None;
            contextMenu.Q<Button>("RotateButton").style.display = DisplayStyle.Flex;

            contextMenu.BringToFront();
        }

        public void Hide()
        {
            if (contextMenu != null)
            {
                contextMenu.style.display = DisplayStyle.None;
            }
        }

        public bool ContainsPoint(Vector2 position)
        {
            return contextMenu != null && contextMenu.worldBound.Contains(position);
        }
    }
}