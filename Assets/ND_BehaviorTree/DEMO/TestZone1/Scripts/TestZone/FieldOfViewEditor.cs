#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(FieldOfView))]
public class FieldOfViewEditor : Editor
{
    private void OnSceneGUI()
    {
        FieldOfView fov = (FieldOfView)target;
        Handles.color = Color.white;
        Vector3 offsetPosition = fov.offset + fov.transform.position;

        // Draw Field of View Arc
        Handles.DrawWireArc(offsetPosition, Vector3.up, Vector3.forward, 360, fov.radius);

        // Calculate view angles
        Vector3 viewAngle01 = DirectionFromAngle(fov.transform.eulerAngles.y, -fov.angle / 2);
        Vector3 viewAngle02 = DirectionFromAngle(fov.transform.eulerAngles.y, fov.angle / 2);

        // Draw view lines
        Handles.color = Color.yellow;
        Handles.DrawLine(offsetPosition, offsetPosition + viewAngle01 * fov.radius);
        Handles.DrawLine(offsetPosition, offsetPosition + viewAngle02 * fov.radius);

        // Draw lines to visible targets
        foreach (GameObject target in fov.targetObjects)
        {
            if (target == null) continue;

            // Access the visibility dictionary via reflection since it's private
            var visibilityDict = (System.Collections.Generic.Dictionary<GameObject, bool>)typeof(FieldOfView)
                .GetField("targetVisibility", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.GetValue(fov);

            if (visibilityDict != null && visibilityDict.TryGetValue(target, out bool isVisible) && isVisible)
            {
                Handles.color = Color.green;
                Handles.DrawLine(offsetPosition, target.transform.position);
            }
        }

        // Draw wireframe sphere with fewer segments
        DrawWireframeSphere(offsetPosition, fov.radius, Color.white, 2);
    }

    private void DrawWireframeSphere(Vector3 center, float radius, Color color, int segments)
    {
        Handles.color = color;

        // Draw circles in the XZ plane
        for (int i = 0; i < segments; i++)
        {
            float angle = (360f / segments) * i;
            Vector3 axis = new Vector3(Mathf.Sin(angle * Mathf.Deg2Rad), 0, Mathf.Cos(angle * Mathf.Deg2Rad));
            Handles.DrawWireArc(center, axis, Vector3.up, 360, radius);
        }

        // Draw circles in the YZ plane
        for (int i = 0; i < segments; i++)
        {
            float angle = (360f / segments) * i;
            Vector3 axis = new Vector3(0, Mathf.Sin(angle * Mathf.Deg2Rad), Mathf.Cos(angle * Mathf.Deg2Rad));
            Handles.DrawWireArc(center, axis, Vector3.right, 360, radius);
        }

        // Draw circles in the XY plane
        for (int i = 0; i < segments; i++)
        {
            float angle = (360f / segments) * i;
            Vector3 axis = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad), 0);
            Handles.DrawWireArc(center, axis, Vector3.forward, 360, radius);
        }
    }

    private Vector3 DirectionFromAngle(float eulerY, float angleInDegrees)
    {
        angleInDegrees += eulerY;
        return new Vector3(Mathf.Sin(angleInDegrees * Mathf.Deg2Rad), 0, Mathf.Cos(angleInDegrees * Mathf.Deg2Rad));
    }
}
#endif