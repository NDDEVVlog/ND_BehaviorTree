// WeaponCellElement.cs
using UnityEngine;
using UnityEngine.UIElements;

public class WeaponCellElement : VisualElement
{
    public WeaponData Data { get; private set; }
    public float InnerRadius { get; set; }
    public float OuterRadius { get; set; }

    private bool _isHovered = false;
    private bool _isSelected = false;
    private Color _currentDrawColor;
    private Image _iconImageElement;

    public WeaponCellElement(WeaponData data, float innerRadius, float outerRadius)
    {
        this.Data = data;
        this.InnerRadius = innerRadius;
        this.OuterRadius = outerRadius;

        // Basic sanity checks
        if (this.InnerRadius >= this.OuterRadius)
        {
            Debug.LogWarning($"Cell '{Data.name}': InnerRadius ({this.InnerRadius}) >= OuterRadius ({this.OuterRadius}). Clamping inner radius.");
            this.InnerRadius = this.OuterRadius * 0.5f; // Attempt a fix
        }
        if (this.InnerRadius < 0) this.InnerRadius = 0;
        if (Data.sweepAngle <= 0) Debug.LogWarning($"Cell '{Data.name}': SweepAngle ({Data.sweepAngle}) is zero or negative.");


        this.name = $"WeaponCell_{data.name.Replace(" ", "")}";
        AddToClassList("weapon-cell");
        _currentDrawColor = data.baseColor;

        generateVisualContent += OnGenerateVisualContent;
        RegisterCallback<PointerEnterEvent>(OnPointerEnter);
        RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
        RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        pickingMode = PickingMode.Position;

        if (Data.icon != null)
        {
            _iconImageElement = new Image { sprite = Data.icon, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            _iconImageElement.style.position = Position.Absolute;
            Add(_iconImageElement);
        }
    }

    private void OnGeometryChanged(GeometryChangedEvent evt) { LayoutIcon(); }
    private void LayoutIcon()
    {
        if (_iconImageElement == null || Data.icon == null || contentRect.width <= 0 || contentRect.height <= 0 || Data.sweepAngle <= 0)
        {
            if (_iconImageElement != null) _iconImageElement.style.display = DisplayStyle.None;
            return;
        }

        Vector2 cellCenter = contentRect.size / 2f;
        float iconVisualMidAngleDeg = Data.startAngle + Data.sweepAngle / 2f; // 0-up, CW
        float iconVisualMidAngleRad = iconVisualMidAngleDeg * Mathf.Deg2Rad;
        float iconDistance = (InnerRadius + OuterRadius) / 2f;

        // Convert our visual angle (0-up, CW) to local space coords (X right, Y down for UIElements default)
        // X = R * sin(visual_angle_rad_0_up_cw)
        // Y = R * cos(visual_angle_rad_0_up_cw) (but positive Y is down, so Y = R * cos for "up" if angle 0 = up)
        // If Data.startAngle=0 means top: X=0, Y=-Dist. Our visual is 0-up CW.
        // X_coord = Distance * Sin(Angle_rad)
        // Y_coord = Distance * -Cos(Angle_rad) (because +Y is down in screen, but -Cos makes Y go up for angle 0)
        Vector2 iconAnchorPos = cellCenter + new Vector2(
            iconDistance * Mathf.Sin(iconVisualMidAngleRad),
            iconDistance * -Mathf.Cos(iconVisualMidAngleRad)
        );

        float radialThickness = OuterRadius - InnerRadius;
        float chordLengthAtIconDist = Data.sweepAngle * Mathf.Deg2Rad * iconDistance;
        float iconSize = Mathf.Min(radialThickness, chordLengthAtIconDist) * 0.60f;

        if (iconSize < 5) { _iconImageElement.style.display = DisplayStyle.None; return; }

        _iconImageElement.style.display = DisplayStyle.Flex;
        _iconImageElement.style.width = iconSize;
        _iconImageElement.style.height = iconSize;
        _iconImageElement.style.left = iconAnchorPos.x - iconSize / 2;
        _iconImageElement.style.top = iconAnchorPos.y - iconSize / 2;
    }


    private void UpdateDrawColor() { /* ... same as before ... */ _currentDrawColor = _isSelected ? Data.selectedColor : (_isHovered ? Data.hoverColor : Data.baseColor); MarkDirtyRepaint(); }
    public void SetSelected(bool selected) { /* ... same as before ... */ if (_isSelected == selected) return; _isSelected = selected; UpdateDrawColor(); }
    private void OnPointerEnter(PointerEnterEvent evt) { /* ... same as before ... */ _isHovered = true; UpdateDrawColor(); }
    private void OnPointerLeave(PointerLeaveEvent evt) { /* ... same as before ... */ _isHovered = false; UpdateDrawColor(); }

    void OnGenerateVisualContent(MeshGenerationContext mgc)
    {
        if (OuterRadius <= InnerRadius || Data.sweepAngle <= 0f || Data.sweepAngle > 360f)
        {
             // Debug.LogWarning($"Skipping draw for {Data.name}: R_In:{InnerRadius}, R_Out:{OuterRadius}, Sweep:{Data.sweepAngle}");
            return;
        }
        // Debug.Log($"Drawing {Data.name}: Start {Data.startAngle}, Sweep {Data.sweepAngle}, InnerR {InnerRadius}, OuterR {OuterRadius}");

        var painter = mgc.painter2D;
        Vector2 center = contentRect.size / 2f;

        painter.strokeColor = Color.black; // Or Data.strokeColor
        painter.lineWidth = 1.5f;
        painter.fillColor = _currentDrawColor;

        // Convert our visual angles (0-degrees = Up, positive = Clockwise)
        // to Painter2D's system (0-degrees = Right, positive = Counter-Clockwise)

        // Start of the segment in Painter2D's system
        float p2d_startAngleDeg = (90f - Data.startAngle + 360f) % 360f;
        // End of the segment in Painter2D's system
        float p2d_endAngleDeg = (90f - (Data.startAngle + Data.sweepAngle) + 360f) % 360f;

        // Calculate corner points using Painter2D angles (0-right, CCW)
        float p2d_startAngleRad = p2d_startAngleDeg * Mathf.Deg2Rad;
        float p2d_endAngleRad   = p2d_endAngleDeg * Mathf.Deg2Rad;

        Vector2 innerStartPoint = center + new Vector2(InnerRadius * Mathf.Cos(p2d_startAngleRad), InnerRadius * Mathf.Sin(p2d_startAngleRad));
        Vector2 outerStartPoint = center + new Vector2(OuterRadius * Mathf.Cos(p2d_startAngleRad), OuterRadius * Mathf.Sin(p2d_startAngleRad));
        Vector2 outerEndPoint   = center + new Vector2(OuterRadius * Mathf.Cos(p2d_endAngleRad),   OuterRadius * Mathf.Sin(p2d_endAngleRad));
        Vector2 innerEndPoint   = center + new Vector2(InnerRadius * Mathf.Cos(p2d_endAngleRad),   InnerRadius * Mathf.Sin(p2d_endAngleRad));

        painter.BeginPath();
        painter.MoveTo(innerStartPoint);
        painter.LineTo(outerStartPoint);

        // Outer arc: from p2d_startAngleDeg, sweep CCW by -Data.sweepAngle (which is Data.sweepAngle CW)
        painter.Arc(center, OuterRadius, p2d_startAngleDeg, -Data.sweepAngle);
        // Current point is now outerEndPoint

        painter.LineTo(innerEndPoint); // Line from outer arc end to inner arc end

        // Inner arc: from p2d_endAngleDeg, sweep CCW by Data.sweepAngle to get back to p2d_startAngleDeg
        painter.Arc(center, InnerRadius, p2d_endAngleDeg, Data.sweepAngle);
        // Current point is now innerStartPoint

        painter.ClosePath(); // Should connect current point (innerStartPoint) to the initial MoveTo point.

        painter.Fill();
        painter.Stroke();
    }

    public override bool ContainsPoint(Vector2 localPoint)
    {
        // ... (This logic should still be largely correct, ensure angle systems match)
        if (!base.ContainsPoint(localPoint)) return false;

        Vector2 center = contentRect.size / 2f;
        Vector2 relativePoint = localPoint - center;
        float distSq = relativePoint.sqrMagnitude;

        if (distSq < InnerRadius * InnerRadius || distSq > OuterRadius * OuterRadius) return false;

        // Point angle in Painter2D system (0-right, CCW)
        float pointAngleDeg_p2d = (Mathf.Atan2(relativePoint.y, relativePoint.x) * Mathf.Rad2Deg + 360f) % 360f;

        // Cell angles in Painter2D system
        float cellStartAngleDeg_p2d = (90f - Data.startAngle + 360f) % 360f;
        // For end angle, consider sweep is CW in Data.sweepAngle
        float cellEndAngleDeg_p2d = (cellStartAngleDeg_p2d - Data.sweepAngle + 360f) % 360f; // Subtract because sweep is CW

        // Standard angle in range check, handles wrapping
        if (Data.sweepAngle == 360f) return true; // Full circle segment

        if (cellStartAngleDeg_p2d > cellEndAngleDeg_p2d) // Wraps around 0/360 (e.g. start 10, end 350 due to CW sweep)
        {
            return (pointAngleDeg_p2d >= cellStartAngleDeg_p2d && pointAngleDeg_p2d < 360f) ||
                   (pointAngleDeg_p2d >= 0f && pointAngleDeg_p2d < cellEndAngleDeg_p2d);
        }
        else
        {
            return pointAngleDeg_p2d >= cellStartAngleDeg_p2d && pointAngleDeg_p2d < cellEndAngleDeg_p2d;
        }
    }
}