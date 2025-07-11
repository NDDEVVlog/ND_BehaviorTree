// --- START OF FILE RingElement.cs ---

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

// An interface makes it easy for RingDisplay to set the gap property without casting to a concrete type.
public interface IRectangularGap
{
    float halfGapWidth { get; set; }
}

public class RingElement : VisualElement, IRectangularGap // Implement the interface
{
    // --- Shape Properties ---
    public float startAngle { get; set; }
    public float endAngle { get; set; }
    public float outerRadius { get; set; }
    public float innerRadius { get; set; }
    public int segments { get; set; }

    // --- Content Properties ---
    private string _itemName;
    public string itemName {
        get => _itemName;
        set { _itemName = value; if (nameLabel != null) nameLabel.text = value; }
    }
    
    private Sprite _itemIcon;
    public Sprite itemIcon {
        get => _itemIcon;
        set { 
            _itemIcon = value; 
            if(iconElement != null) 
            {
                // Explicitly create a new StyleBackground. This is a more robust way to set images.
                // If the new sprite is null, it correctly clears the background.
                iconElement.style.backgroundImage = (value != null) ? new StyleBackground(value) : new StyleBackground();
            }
        }
    }
    
    private float _iconSize;
    public float iconSize
    {
        get => _iconSize;
        set
        {
            _iconSize = Mathf.Max(0, value);
            if (iconElement != null)
            {
                iconElement.style.width = _iconSize;
                iconElement.style.height = _iconSize;
            }
        }
    }
    
    // --- Style & Interaction ---
    private Color _color;
    public Color defaultColor { get; set; }
    public Color hoverColor { get; set; }
    public float hitAnglePadding { get; set; } = 0f;

    // --- Edge Properties ---
    public bool showEdge { get; set; } = false;
    public float edgeWidth { get; set; } = 1f;
    public Color edgeColor { get; set; } = Color.black;

    // --- Property for Rectangular Gaps (from IRectangularGap interface) ---
    public float halfGapWidth { get; set; } = 0f;

    // --- Child Elements ---
    private Label nameLabel;
    private VisualElement iconElement;
    private VisualElement _contentContainer;

    public RingElement()
    {
        style.position = Position.Absolute;
        style.width = Length.Percent(100);
        style.height = Length.Percent(100);

        generateVisualContent += OnGenerateVisualContent;
        
        _contentContainer = new VisualElement { name = "content-container" };
        _contentContainer.style.position = Position.Absolute;
        _contentContainer.style.alignItems = Align.Center;
        _contentContainer.style.justifyContent = Justify.Center;
        _contentContainer.pickingMode = PickingMode.Ignore;
        Add(_contentContainer);

        iconElement = new VisualElement { name = "icon" };
        _contentContainer.Add(iconElement);

        nameLabel = new Label { name = "label" };
        nameLabel.style.color = Color.white;
        nameLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        nameLabel.style.textShadow = new TextShadow { offset = new Vector2(1, 1), blurRadius = 2, color = Color.black };
        _contentContainer.Add(nameLabel);
        
        RegisterCallback<PointerEnterEvent>(OnPointerEnter);
        RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
        RegisterCallback<ClickEvent>(OnClick);
        RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
    }
    
    private void OnGeometryChanged(GeometryChangedEvent evt)
    {
        PositionContent();
        _color = defaultColor;
        MarkDirtyRepaint();
    }

    private void PositionContent()
    {
        if (_contentContainer == null) return;
        
        float midAngleRad = Mathf.Deg2Rad * (startAngle + endAngle) / 2f;
        float midRadius = (innerRadius + outerRadius) / 2f;

        Vector2 center = new Vector2(layout.width / 2, layout.height / 2);
        Vector2 position = new Vector2(
            Mathf.Cos(midAngleRad) * midRadius,
            Mathf.Sin(midAngleRad) * midRadius
        );

        _contentContainer.style.left = center.x + position.x - _contentContainer.layout.width / 2;
        _contentContainer.style.top = center.y + position.y - _contentContainer.layout.height / 2;
    }
    
    void OnGenerateVisualContent(MeshGenerationContext ctx)
    {
        if (segments <= 1) return;

        Vector2 center = new Vector2(layout.width / 2, layout.height / 2);
        
        // Calculate the angular gap needed at each radius to achieve a constant pixel width
        float outerGapAngle = 0f;
        float innerGapAngle = 0f;
        if (halfGapWidth > 0f && outerRadius > 0f && innerRadius > 0f && innerRadius < outerRadius)
        {
            // Clamp the value inside Asin to prevent math errors if gap is too large
            outerGapAngle = Mathf.Rad2Deg * Mathf.Asin(Mathf.Clamp(halfGapWidth / outerRadius, -1f, 1f));
            innerGapAngle = Mathf.Rad2Deg * Mathf.Asin(Mathf.Clamp(halfGapWidth / innerRadius, -1f, 1f));
        }

        float drawStartOuter = startAngle + outerGapAngle;
        float drawEndOuter = endAngle - outerGapAngle;
        float drawStartInner = startAngle + innerGapAngle;
        float drawEndInner = endAngle - innerGapAngle;

        float outerAngleRange = drawEndOuter - drawStartOuter;
        float innerAngleRange = drawEndInner - drawStartInner;

        if (outerAngleRange <= 0 || innerAngleRange <= 0) return; // Slice is too small to draw

        var outerPoints = new List<Vector2>(segments);
        var innerPoints = new List<Vector2>(segments);

        for (int i = 0; i < segments; i++)
        {
            float t = (segments > 1) ? i / (float)(segments - 1) : 0f; // Normalized position (0 to 1)

            // Calculate angle for outer and inner arcs based on normalized position
            float outerAngleRad = Mathf.Deg2Rad * (drawStartOuter + t * outerAngleRange);
            float innerAngleRad = Mathf.Deg2Rad * (drawStartInner + t * innerAngleRange);
            
            outerPoints.Add(center + new Vector2(Mathf.Cos(outerAngleRad), Mathf.Sin(outerAngleRad)) * outerRadius);
            innerPoints.Add(center + new Vector2(Mathf.Cos(innerAngleRad), Mathf.Sin(innerAngleRad)) * innerRadius);
        }
    
        var meshWriteData = ctx.Allocate(segments * 2, (segments - 1) * 6);
    
        for (int i = 0; i < segments; i++)
        {
            meshWriteData.SetNextVertex(new Vertex() { position = outerPoints[i], tint = _color });
            meshWriteData.SetNextVertex(new Vertex() { position = innerPoints[i], tint = _color });
        }
        
        for (int i = 0; i < segments - 1; i++)
        {
            ushort i0 = (ushort)(i * 2);
            ushort i1 = (ushort)(i * 2 + 1);
            ushort i2 = (ushort)(i * 2 + 2);
            ushort i3 = (ushort)(i * 2 + 3);

            meshWriteData.SetNextIndex(i0);
            meshWriteData.SetNextIndex(i2);
            meshWriteData.SetNextIndex(i1);

            meshWriteData.SetNextIndex(i2);
            meshWriteData.SetNextIndex(i3);
            meshWriteData.SetNextIndex(i1);
        }
        
        if (showEdge && edgeWidth > 0 && outerPoints.Count > 1)
        {
            var painter = ctx.painter2D;
            painter.lineWidth = edgeWidth;
            painter.strokeColor = edgeColor;
            
            painter.BeginPath();
            painter.MoveTo(outerPoints[0]);
            for (int i = 1; i < outerPoints.Count; i++) { painter.LineTo(outerPoints[i]); }
            painter.LineTo(innerPoints[innerPoints.Count - 1]);
            for (int i = innerPoints.Count - 2; i >= 0; i--) { painter.LineTo(innerPoints[i]); }
            painter.ClosePath();
            painter.Stroke();
        }
    }

    private void OnPointerEnter(PointerEnterEvent evt)
    {
        _color = hoverColor;
        MarkDirtyRepaint();
        Debug.Log($"Hovering over: {itemName}");
    }

    private void OnPointerLeave(PointerLeaveEvent evt)
    {
        _color = defaultColor;
        MarkDirtyRepaint();
    }

    private void OnClick(ClickEvent evt)
    {
        Debug.Log($"Clicked on: {itemName}");
    }
    
    public override bool ContainsPoint(Vector2 localPoint)
    {
        Vector2 center = new Vector2(layout.width / 2, layout.height / 2);
        Vector2 delta = localPoint - center;

        float sqrDistance = delta.sqrMagnitude;
        float distance = Mathf.Sqrt(sqrDistance);

        if (distance < innerRadius || distance > outerRadius)
        {
            return false;
        }

        float pointerAngle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        
        // Adjust hit detection for rectangular gaps by calculating the gap angle at the pointer's distance from the center.
        float gapAngleAtDistance = 0f;
        if (halfGapWidth > 0 && distance > 0)
        {
            gapAngleAtDistance = Mathf.Rad2Deg * Mathf.Asin(Mathf.Clamp(halfGapWidth / distance, -1f, 1f));
        }

        float detectionStartAngle = startAngle + gapAngleAtDistance - hitAnglePadding;
        float detectionEndAngle = endAngle - gapAngleAtDistance + hitAnglePadding;
        
        return IsAngleBetween(pointerAngle, detectionStartAngle, detectionEndAngle);
    }

    private bool IsAngleBetween(float angle, float start, float end)
    {
        // Normalize all angles to the 0-360 range
        start = (start % 360 + 360) % 360;
        end = (end % 360 + 360) % 360;
        angle = (angle % 360 + 360) % 360;

        if (start < end) // Normal case (e.g., from 10 to 90 degrees)
        {
            return angle >= start && angle <= end;
        }
        else // Wraps around 360 (e.g., from 350 to 20 degrees)
        {
            return angle >= start || angle <= end;
        }
    }
}

// --- END OF FILE RingElement.cs ---