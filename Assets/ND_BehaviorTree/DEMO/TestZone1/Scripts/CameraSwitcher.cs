using UnityEngine;
using UnityEngine.UI; // Required for UI elements like Button

public class CameraSwitcher : MonoBehaviour
{
    // An array to hold all your cameras.
    // Drag your camera GameObjects into this array in the Inspector.
    public Camera[] cameras;

    // The index of the currently active camera in the array.
    private int currentCameraIndex;

    void Start()
    {
        // Initialize the camera index to the first camera.
        currentCameraIndex = 0;

        // Disable all cameras except the first one.
        for (int i = 1; i < cameras.Length; i++)
        {
            cameras[i].gameObject.SetActive(false);
        }

        // Ensure the first camera is active.
        if (cameras.Length > 0)
        {
            cameras[0].gameObject.SetActive(true);
            Debug.Log("Camera Switcher started. Active camera: " + cameras[0].name);
        }
    }

    void Update()
    {
        // Check if the 'C' key is pressed down.
        // GetKeyDown is only true for the single frame the key is first pressed.
        if (Input.GetKeyDown(KeyCode.C))
        {
            SwitchToNextCamera();
        }
    }

    // This public method can be called by a UI Button's OnClick event.
    public void SwitchToNextCamera()
    {
        if (cameras.Length == 0)
        {
            Debug.LogWarning("No cameras assigned to the CameraSwitcher script.");
            return;
        }

        // Disable the current camera.
        cameras[currentCameraIndex].gameObject.SetActive(false);

        // Increment the index. If it goes past the end of the array, wrap it back to 0.
        currentCameraIndex = (currentCameraIndex + 1) % cameras.Length;

        // Enable the new current camera.
        cameras[currentCameraIndex].gameObject.SetActive(true);

        Debug.Log("Switched to camera: " + cameras[currentCameraIndex].name);
    }
}