using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCrosshair : MonoBehaviour
{
    [Header("Player Identification")]
    [Tooltip("1 for Player 1, 2 for Player 2")]
    public int playerID = 1;

    [Header("UI Reference")]
    [Tooltip("Drag the UI Image RectTransform for this player's crosshair here.")]
    public RectTransform crosshairUI;

    [Header("Aim Settings")]
    [Tooltip("Speed at which the crosshair moves when using the controller right stick.")]
    public float gamepadAimSpeed = 1200f;

    [Header("Control Scheme")]
    [Tooltip("Check this if this player is using Keyboard & Mouse (drives crosshair with mouse position).")]
    public bool useMouse = false;

    private Vector2 screenPosition;
    private Camera cam;

    void Start()
    {
        cam = Camera.main;
        if (crosshairUI != null)
        {
            // Initialize crosshair in the center of the screen
            screenPosition = new Vector2(Screen.width / 2f, Screen.height / 2f);
        }
    }

    void Update()
    {
        if (crosshairUI == null || cam == null) return;

        Vector2 inputDelta = Vector2.zero;

        if (useMouse && playerID == 1 && Mouse.current != null)
        {
            // Player 1 using Mouse: directly lock crosshair to mouse coordinates
            screenPosition = Mouse.current.position.ReadValue();
        }
        else
        {
            // Controller User: Read from the Right Joystick
            Gamepad pad = null;
            int targetIndex = playerID - 1; // Player 1 = Index 0, Player 2 = Index 1

            if (Gamepad.all.Count > targetIndex)
            {
                pad = Gamepad.all[targetIndex];
            }
            else if (Gamepad.current != null)
            {
                pad = Gamepad.current;
            }

            if (pad != null)
            {
                inputDelta = pad.rightStick.ReadValue();

                // Apply a deadzone so minor stick drift doesn't move the crosshair
                if (inputDelta.magnitude < 0.2f)
                {
                    inputDelta = Vector2.zero;
                }
            }

            // Move the crosshair smoothly based on right stick input
            screenPosition += inputDelta * gamepadAimSpeed * Time.deltaTime;

            // Clamp the crosshair so it cannot leave the game screen boundaries
            screenPosition.x = Mathf.Clamp(screenPosition.x, 0, Screen.width);
            screenPosition.y = Mathf.Clamp(screenPosition.y, 0, Screen.height);
        }

        // Apply the updated position to the UI Image RectTransform
        crosshairUI.position = screenPosition;
    }

    /// <summary>
    /// Returns the world-space position where this crosshair is pointing, 
    /// perfect for passing into your WandShooter.FireAt() method.
    /// </summary>
    public Vector2 GetAimWorldPosition()
    {
        if (cam == null) return transform.position;
        Vector3 worldPoint = cam.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, -cam.transform.position.z));
        return new Vector2(worldPoint.x, worldPoint.y);
    }
}