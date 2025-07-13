using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

[ExecuteAlways]
[RequireComponent(typeof(UIDocument))]
public class RingDisplay : MonoBehaviour
{
    // --- Layout Settings ---
    [Header("Layout")]  
    [Range(0, 360)] public float wheelRotation = 0f;
    
    [Header("Inventory Items")]
    public List<WheelItem> items;

    [Header("Wheel Shape")]
    [Min(0)] public float innerRadius = 50f;
    [Min(1)] public float outerRadius = 150f;
    [Range(3, 128)] public int segmentsPerSlice = 32;
    public Color defaultColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
    public Color hoverColor = Color.white;

    [Header("Gaps")]
    [Tooltip("Create gaps by leaving an angular space between slices.")]
    [Range(0, 0.1f)] public float percentageOfGapPerPie = 0.01f;
    [Tooltip("Create gaps by shaving off the sides of each slice, creating a straight edge.")]
    public bool rectangularGaps = false;
    [Tooltip("The width of the rectangular gap in pixels.")]
    [Min(0)] public float gapWidth = 5f;

    [Header("Edge Settings")]
    [Tooltip("Show a border around each slice.")]
    public bool showEdge = true;
    [Tooltip("The width of the border around each slice.")]
    [Min(0)] public float edgeWidth = 2f;
    [Tooltip("The color of the border around each slice.")]
    public Color edgeColor = Color.black;

    [Header("Interaction")]
    [Tooltip("Adds extra padding (in degrees) to the hit detection area of each slice, making them easier to select.")]
    [Range(0, 10)] public float hitAnglePadding = 2.0f;
    [Range(0, 1)] public float hoverRangeMin = 0.5f;

    [Header("Icon & Text Settings")]
    public bool scaleIconWithSliceSize = true;
    [Min(1)] public float iconBaseSize = 40f;
    [Min(0)] public float iconSizeMultiplier = 1.0f;
    [Tooltip("Font size for the item name text.")]
    [Min(1)] public float textSize = 14f;

    [Header("Hover Line Settings")]
    [Tooltip("Enable or disable the hover line feature.")]
    public bool enableHoverLine = false;
    [Tooltip("Width of the hover line.")]
    [Min(1)] public float hoverLineWidth = 2f;
    [Tooltip("Color of the hover line.")]
    public Color hoverLineColor = Color.white;
    [Tooltip("Width of the hover line outline.")]
    [Min(1)] public float hoverLineOutlineWidth = 3f;
    [Tooltip("Color of the hover line outline.")]
    public Color hoverLineOutlineColor = Color.black;

    private UIDocument uiDoc;
    private VisualElement root;
    private List<RingElement> ringElements = new List<RingElement>();

    void OnEnable()
    {
        uiDoc = GetComponent<UIDocument>();
        if (uiDoc?.rootVisualElement == null) return;
        
        root = uiDoc.rootVisualElement;
        root.style.flexGrow = 1;
        root.style.justifyContent = Justify.Center;
        root.style.alignItems = Align.Center;

        RebuildWheel();
    }
    
    void OnValidate()
    {
        if (Application.isPlaying) return;
        if (root != null && items != null)
        {
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += RebuildWheel;
            #endif
        }
    }

    public void RebuildWheel()
    {
        if (root == null || items == null) return;

        root.Clear();
        ringElements.Clear();

        if (items.Count == 0) return;

        float totalPercentage = items.Sum(item => item.percentageOccupied);
        if (totalPercentage <= 0) totalPercentage = 1;
        
        float gapAngle = rectangularGaps ? 0f : percentageOfGapPerPie * 360f;
        float totalGapPercentage = items.Count * (gapAngle / 360f);

        float availablePiePercentage = 1.0f - totalGapPercentage;
        if (availablePiePercentage < 0) availablePiePercentage = 0;
        
        float currentAngle = this.wheelRotation;

        foreach (var item in items)
        {
            float normalizedPercentage = item.percentageOccupied / totalPercentage;
            float sliceAngle = normalizedPercentage * availablePiePercentage * 360f;

            float finalIconSize = this.iconBaseSize;
            if (scaleIconWithSliceSize)
            {
                float scaleFactor = sliceAngle / 36.0f;
                finalIconSize = this.iconBaseSize * scaleFactor * this.iconSizeMultiplier;
            }

            var ringElement = new RingElement();
            
            ringElement.startAngle = currentAngle;
            ringElement.endAngle = currentAngle + sliceAngle;
            ringElement.innerRadius = this.innerRadius;
            ringElement.outerRadius = this.outerRadius;
            ringElement.segments = this.segmentsPerSlice;
            
            ringElement.itemName = item.itemName;
            ringElement.itemIcon = item.itemIcon;
            ringElement.iconSize = finalIconSize;
            
            ringElement.defaultColor = this.defaultColor;
            ringElement.hoverColor = this.hoverColor;
            ringElement.hitAnglePadding = this.hitAnglePadding;
            ringElement.hoverRangeMin = this.hoverRangeMin;

            ringElement.showEdge = this.showEdge;
            ringElement.edgeWidth = this.edgeWidth;
            ringElement.edgeColor = this.edgeColor;
            
            if (this.rectangularGaps)
            {
                (ringElement as IRectangularGap).halfGapWidth = this.gapWidth / 2f;
            }
            
            // Set the text size for the label
            ringElement.nameLabel.style.fontSize = this.textSize;

            // Set hover line properties
            ringElement.showHoverLine = this.enableHoverLine;
            ringElement.hoverLineWidth = this.hoverLineWidth;
            ringElement.hoverLineColor = this.hoverLineColor;
            ringElement.hoverLineOutlineWidth = this.hoverLineOutlineWidth;
            ringElement.hoverLineOutlineColor = this.hoverLineOutlineColor;
            
            root.Add(ringElement);
            ringElements.Add(ringElement);
            
            currentAngle += sliceAngle + gapAngle;
        }
    }
}