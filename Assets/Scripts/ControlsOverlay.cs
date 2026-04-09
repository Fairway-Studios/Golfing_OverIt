using UnityEngine;
using UnityEngine.InputSystem;

public class ControlsOverlay : MonoBehaviour
{
    [Header("UI Reference")]
    [Tooltip("Drag your ControlsInfo Panel/Image here")]
    public GameObject controlsMenu;

    void Start()
    {
        // Make sure it is always hidden when the scene first loads
        if (controlsMenu != null)
        {
            controlsMenu.SetActive(false);
        }
    }

    void Update()
    {
        bool togglePressed = false;

        // 1. Check if the 'H' key was pressed on the keyboard
        if (Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame)
        {
            togglePressed = true;
        }

        // 2. Check if the 'View/Share/Create' button was pressed on the controller
        // In Unity's Input System, the left-middle button is universally called 'selectButton'
        if (Gamepad.current != null && Gamepad.current.selectButton.wasPressedThisFrame)
        {
            togglePressed = true;
        }

        // 3. If either was pressed, toggle the menu on or off
        if (togglePressed && controlsMenu != null)
        {
            bool isCurrentlyActive = controlsMenu.activeSelf;
            controlsMenu.SetActive(!isCurrentlyActive);
        }
    }
}