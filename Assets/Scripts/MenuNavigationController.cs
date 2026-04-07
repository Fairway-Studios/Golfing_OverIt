using UnityEngine;
using UnityEngine.EventSystems; // Required for controller focus!

public class MenuNavigationController : MonoBehaviour
{
    [Header("Menu Panels")]
    public GameObject singlePlayerSubMenu;
    public GameObject mainMenuPanel; // The parent object holding your main buttons (if you have one)

    [Header("Buttons to Highlight")]
    public GameObject newGameButton;         // The top button of the sub-menu
    public GameObject singlePlayerMainButton; // The button you clicked to open the sub-menu

    void Update()
    {
        // If the sub-menu is currently open AND the player presses O / B (Cancel)
        if (singlePlayerSubMenu.activeInHierarchy && Input.GetButtonDown("Cancel"))
        {
            CloseSinglePlayerMenu();
        }
    }

    // Call this from your Main Menu's "Singleplayer" button OnClick event
    public void OpenSinglePlayerMenu()
    {
        singlePlayerSubMenu.SetActive(true);

        // 1. Clear the current controller focus
        EventSystem.current.SetSelectedGameObject(null);

        // 2. Force the focus onto the New Game button
        EventSystem.current.SetSelectedGameObject(newGameButton);
    }

    // Call this from your Sub-Menu's "Back" button OnClick event (and it runs via the O button)
    public void CloseSinglePlayerMenu()
    {
        singlePlayerSubMenu.SetActive(false);

        // 1. Clear the current controller focus
        EventSystem.current.SetSelectedGameObject(null);

        // 2. Force the focus back to the original Singleplayer button so the player isn't lost
        EventSystem.current.SetSelectedGameObject(singlePlayerMainButton);
    }
}