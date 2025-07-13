using UnityEngine;

public class WheelHoverEffect : MonoBehaviour
{
    [Tooltip("The GameObject to rotate towards the pointer.")]
    public GameObject targetObject;

    [Tooltip("Custom position on screen (0 to 1) where the object should be anchored. Default is middle (0.5, 0.5).")]
    public Vector2 customPosition = new Vector2(0.5f, 0.5f);

    private Camera mainCamera;

    void Start()
    {
        mainCamera = Camera.main;
        if (targetObject == null)
        {
            Debug.LogWarning("No targetObject assigned to WheelHoverEffect!");
        }
    }

    void Update()
    {
        if (targetObject != null && mainCamera != null)
        {
            // Convert screen position to world position
            Vector2 screenPosition = new Vector2(customPosition.x * Screen.width, customPosition.y * Screen.height);
            Vector3 worldPosition = mainCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, 10f));

            // Calculate direction from target object to pointer
            Vector3 direction = (worldPosition - targetObject.transform.position).normalized;

            // Rotate the target object to look at the pointer in 2D
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            targetObject.transform.rotation = Quaternion.Euler(0, 0, angle - 90f);
        }
    }
}