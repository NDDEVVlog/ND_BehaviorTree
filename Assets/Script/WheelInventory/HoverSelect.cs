using UnityEngine;
using UnityEngine.UIElements;

public class HoverSelect : MonoBehaviour
{
    [Header("References")]
    public RingDisplay ringDisplay;      // Drag your RingDisplay GameObject here (or the one with UIDocument + RingDisplay)

    [Header("Input")]
    public KeyCode holdKey = KeyCode.Tab;

    private bool isHolding = false;
    private RingElement currentlyHovered = null;

    private UIDocument uiDoc;           // We'll get this from RingDisplay
    private VisualElement wheelRoot;

    void Awake()
    {
        if (ringDisplay == null)
        {
            ringDisplay = GetComponent<RingDisplay>() ?? FindObjectOfType<RingDisplay>();
            if (ringDisplay == null)
            {
                Debug.LogError("HoverSelect: No RingDisplay reference or found in scene!", this);
                enabled = false;
                return;
            }
        }

        uiDoc = ringDisplay.GetComponent<UIDocument>();
        if (uiDoc == null)
        {
            Debug.LogError("HoverSelect: UIDocument missing on RingDisplay GameObject!", this);
            enabled = false;
            return;
        }

        wheelRoot = uiDoc.rootVisualElement;
        if (wheelRoot == null) return;

        // Initially hide
        HideWheel(true);
    }

    void Update()
    {
        // Press/hold → show
        if (Input.GetKeyDown(holdKey))
        {
            ShowWheel();
        }

        // While holding → track hover
        if (isHolding)
        {
            // Optional: you can poll mouse position here if you want custom center-based selection
            // But your RingElement already handles PointerEnter/Leave very well, so we just wait for release
        }

        // Release → select hovered item + hide
        if (Input.GetKeyUp(holdKey) && isHolding)
        {
            if (currentlyHovered != null)
            {
                SelectItem(currentlyHovered);
            }
            else
            {
                Debug.Log("Weapon wheel released - no item hovered, selection cancelled.");
            }

            HideWheel();
        }
    }

    private void ShowWheel()
    {
        isHolding = true;

        wheelRoot.style.display = DisplayStyle.Flex;   // or opacity = 1, scale = 1, etc.
        wheelRoot.style.opacity = 1f;

        // Optional: nice pop-in
        // wheelRoot.style.scale = new StyleScale(new Scale(new Vector2(1.05f, 1.05f)));
        // ... then tween back to 1 if you have DOTween or similar

        Debug.Log("Weapon wheel opened (hold Tab)");
    }

    private void HideWheel(bool immediate = false)
    {
        isHolding = false;

        if (immediate)
        {
            wheelRoot.style.display = DisplayStyle.None;
            wheelRoot.style.opacity = 0f;
        }
        else
        {
            // Optional: fade out
            wheelRoot.style.opacity = 0f;
            // After delay → display = none (use schedule if needed)
        }

        // Cursor.lockState = CursorLockMode.Locked;   // or your game's default
        // Cursor.visible = false;

        currentlyHovered = null;

        Debug.Log("Weapon wheel hidden");
    }

    private void SelectItem(RingElement element)
    {
        Debug.Log($"Selected weapon on release: {element.itemName}");

        // TODO later: 
        // EquipWeapon(element.itemName);
        // PlaySound();
        // etc.
    }

    // Called by RingElements when they get hovered
    public void RegisterHover(RingElement element)
    {
        currentlyHovered = element;
        Debug.Log($"Hover changed to: {element?.itemName ?? "none"}");
    }

    public void ClearHover(RingElement element)
    {
        if (currentlyHovered == element)
        {
            currentlyHovered = null;
        }
    }
}