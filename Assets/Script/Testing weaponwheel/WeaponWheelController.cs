// WeaponWheelController.cs
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
// Remove: using UnityEngine.InputSystem; // No longer needed

public class WeaponWheelController : MonoBehaviour
{
    public UIDocument uiDocument;
    public List<WeaponData> weapons = new List<WeaponData>();
    public float wheelOuterRadius = 150f;
    public float wheelInnerRadius = 70f;
    [Tooltip("Overall size of the VisualElement containing the wheel. Should be > wheelOuterRadius * 2")]
    public float wheelContainerSize = 350f;

    // Changed Key to KeyCode for old Input Manager
    public KeyCode showHideKey = KeyCode.Tab;

    private VisualElement _rootVisualElement;
    private VisualElement _wheelContainer;
    private Label _selectedWeaponNameLabel;
    private List<WeaponCellElement> _weaponCells = new List<WeaponCellElement>();
    private WeaponCellElement _currentHoveredCell = null;
    private WeaponCellElement _selectedCell = null;
    private bool _isWheelVisible = false; // Will be set to true in Start for testing

    void Start()
    {
        if (uiDocument == null)
        {
            Debug.LogError("UIDocument not assigned to WeaponWheelController!");
            this.enabled = false;
            return;
        }
        _rootVisualElement = uiDocument.rootVisualElement;
        _wheelContainer = _rootVisualElement.Q<VisualElement>("WeaponWheelContainer");
        _selectedWeaponNameLabel = _rootVisualElement.Q<Label>("SelectedWeaponName");

        if (_wheelContainer == null)
        {
            Debug.LogError("WeaponWheelContainer not found in UXML!");
            this.enabled = false;
            return;
        }

        _wheelContainer.style.width = wheelContainerSize;
        _wheelContainer.style.height = wheelContainerSize;
        _wheelContainer.style.left = Length.Percent(50);
        _wheelContainer.style.top = Length.Percent(50);
        _wheelContainer.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50));
        _wheelContainer.style.translate = new Translate(new Length(-50, LengthUnit.Percent), new Length(-50, LengthUnit.Percent));

        PopulateWheel();

        // Show the wheel immediately for testing
        ShowWheel(true);
    }

    void Update()
    {
        // Using old Input Manager for key check
        if (Input.GetKeyDown(showHideKey))
        {
            if (_isWheelVisible)
                HideWheel();
            else
                ShowWheel();
        }

        if (_isWheelVisible)
        {
            HandleMouseInput();
        }
    }

    void PopulateWheel()
    {
        _wheelContainer.Clear();
        _weaponCells.Clear();

        if (weapons == null || weapons.Count == 0) return;

        float totalWeight = weapons.Sum(w => w.angleWeight);
        if (totalWeight <= 0) totalWeight = weapons.Count;

        float currentAngle = 0f;

        foreach (var weaponData in weapons)
        {
            float sweepAngle = (weaponData.angleWeight / totalWeight) * 360f;
            weaponData.startAngle = currentAngle;
            weaponData.sweepAngle = sweepAngle;

            var cell = new WeaponCellElement(weaponData, wheelInnerRadius, wheelOuterRadius);
            _weaponCells.Add(cell);
            _wheelContainer.Add(cell);

            cell.RegisterCallback<PointerDownEvent>(evt => OnCellClicked(cell));
            cell.RegisterCallback<PointerEnterEvent>(evt => OnCellPointerEnter(cell));
            cell.RegisterCallback<PointerLeaveEvent>(evt => OnCellPointerLeave(cell));


            currentAngle += sweepAngle;
        }
        if (_selectedWeaponNameLabel != null && _selectedWeaponNameLabel.parent != _wheelContainer)
        {
             if(_selectedWeaponNameLabel.parent != null) _selectedWeaponNameLabel.parent.RemoveFromHierarchy(); // Remove from old parent if any
            _wheelContainer.Add(_selectedWeaponNameLabel.parent); // Re-add center info on top
        }
    }

    private void OnCellPointerEnter(WeaponCellElement cell)
    {
        _currentHoveredCell = cell;
        if (_selectedWeaponNameLabel != null)
        {
            _selectedWeaponNameLabel.text = cell.Data.name;
        }
    }

    private void OnCellPointerLeave(WeaponCellElement cell)
    {
        if (_currentHoveredCell == cell)
        {
            _currentHoveredCell = null;
            if (_selectedWeaponNameLabel != null)
            {
                _selectedWeaponNameLabel.text = _selectedCell != null ? _selectedCell.Data.name : "---";
            }
        }
    }

    private void OnCellClicked(WeaponCellElement cell)
    {
        if (cell != null)
        {
            SelectWeapon(cell);
            HideWheel(); // Hide after selection
        }
    }

    void HandleMouseInput()
    {
        // Using old Input Manager for mouse button check (0 is left mouse button)
        if (Input.GetMouseButtonUp(0))
        {
            if (_currentHoveredCell != null)
            {
                SelectWeapon(_currentHoveredCell);
                HideWheel(); // Hide after selection
            }
            // If you want to hide the wheel even if clicking outside a cell:
            // else if(!_wheelContainer.worldBound.Contains(Mouse.current.position.ReadValue())) // (Need to adapt this check for old input)
            // {
            //     HideWheel();
            // }
        }
    }

    void SelectWeapon(WeaponCellElement cellToSelect)
    {
        if (_selectedCell != null)
        {
            _selectedCell.SetSelected(false);
        }

        _selectedCell = cellToSelect;

        if (_selectedCell != null)
        {
            _selectedCell.SetSelected(true);
            Debug.Log($"Weapon Selected: {_selectedCell.Data.name}");
            if (_selectedWeaponNameLabel != null) _selectedWeaponNameLabel.text = _selectedCell.Data.name;
            // TODO: Add your game logic for equipping the weapon here
        }
    }

    public void ShowWheel(bool immediate = false)
    {
        _isWheelVisible = true;
        if (immediate)
        {
            _wheelContainer.style.opacity = 1;
            _wheelContainer.style.scale = new Scale(Vector2.one);
            _wheelContainer.AddToClassList("weapon-wheel-container--visible"); // Ensure class consistency
        }
        else
        {
            _wheelContainer.AddToClassList("weapon-wheel-container--visible"); // Triggers USS transition
        }

        Time.timeScale = 0.2f;
        // Explicitly use UnityEngine.Cursor to resolve ambiguity
        UnityEngine.Cursor.lockState = CursorLockMode.None;
        UnityEngine.Cursor.visible = true;
    }

    public void HideWheel(bool immediate = false)
    {
        _isWheelVisible = false;
        if (immediate)
        {
            _wheelContainer.style.opacity = 0;
            _wheelContainer.style.scale = new Scale(Vector2.one * 0.8f);
            _wheelContainer.RemoveFromClassList("weapon-wheel-container--visible");
        }
        else
        {
            _wheelContainer.RemoveFromClassList("weapon-wheel-container--visible"); // Triggers USS transition
        }

        Time.timeScale = 1f;
        // Explicitly use UnityEngine.Cursor to resolve ambiguity
        // UnityEngine.Cursor.lockState = CursorLockMode.Locked; // Or your game's default
        // UnityEngine.Cursor.visible = false;

        if (_selectedWeaponNameLabel != null)
        {
            _selectedWeaponNameLabel.text = _selectedCell != null ? _selectedCell.Data.name : "---";
        }
        _currentHoveredCell = null;
    }

    [ContextMenu("Setup Default Weapons")]
    void SetupDefaultWeapons()
    {
        weapons = new List<WeaponData>
        {
            new WeaponData("Pistol", 1f, new Color(0.8f, 0.5f, 0.5f), Color.red),
            new WeaponData("Shotgun", 1.5f, new Color(0.5f, 0.8f, 0.5f), Color.green),
            new WeaponData("Rifle", 2f, new Color(0.5f, 0.5f, 0.8f), Color.blue),
            new WeaponData("Sniper", 0.8f, new Color(0.8f, 0.8f, 0.5f), Color.yellow),
            new WeaponData("Grenade", 0.5f, new Color(0.6f, 0.6f, 0.6f), Color.magenta),
        };
        Debug.Log("Default weapons configured. Run the game or call PopulateWheel if already running.");
    }
}