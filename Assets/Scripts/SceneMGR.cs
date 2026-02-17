using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI; // Added for Button interaction

public class SceneMGR : MonoBehaviour
{
    [Header("Main Menu UI")]
    [SerializeField] private GameObject menuCanvas;
    [SerializeField] private GameObject settingsCanvas;

    // --- NEW: Singleplayer Sub-Menu References ---
    [Header("Singleplayer Mode UI")]
    [SerializeField] private GameObject singlePlayerSubMenu; // The Panel with New Game / Continue
    [SerializeField] private Button continueButton;          // To disable if no save exists

    [Header("Game Over UI")]
    [SerializeField] private GameObject winCanvas;
    [SerializeField] private GameObject loseCanvas;

    [Header("Pause System UI")]
    [SerializeField] private GameObject pauseMenuRoot;
    [SerializeField] private GameObject pauseButtons;
    [SerializeField] private GameObject pauseSettings;

    private GameObject[] balls;
    private Dictionary<KeyCode, Action> keyActions;
    private bool isGamePaused = false;
    private Transform playerTransform; // Cache for saving

    private void Start()
    {
        balls = GameObject.FindGameObjectsWithTag("GolfBall");

        // Try to find player for saving (Singleplayer assumption)
        GameObject p1 = GameObject.FindGameObjectWithTag("Player");
        if (p1 != null) playerTransform = p1.transform;

        keyActions = new Dictionary<KeyCode, Action>
        {
            { KeyCode.Escape, HandleEscapeInput },
            { KeyCode.S, OpenMainMenuSettings },
            { KeyCode.W, () => SwitchToCanvas(winCanvas) },
            { KeyCode.L, () => SwitchToCanvas(loseCanvas) },
        };

        string currentScene = SceneManager.GetActiveScene().name;

        if (currentScene == "MainMenu" || currentScene == "CustomizationScene")
        {
            SwitchToCanvas(menuCanvas);
            // Hide the sub-menu by default
            if (singlePlayerSubMenu != null) singlePlayerSubMenu.SetActive(false);
        }
        else
        {
            CloseAllMenus();
        }
    }

    private void Update()
    {
        foreach (var entry in keyActions)
        {
            if (Input.GetKeyDown(entry.Key)) entry.Value?.Invoke();
        }
    }

    // --- NEW: Menu Navigation Logic ---

    public void OpenSinglePlayerMenu()
    {
        // ONLY turn on the sub-menu. Leave menuCanvas alone so the background stays!
        if (singlePlayerSubMenu != null)
        {
            singlePlayerSubMenu.SetActive(true);

            // Check if we can continue
            if (continueButton != null)
            {
                continueButton.interactable = SaveSystem.HasSaveFile();
            }
        }
    }

    public void BackToMainMenu()
    {
        // ONLY hide the sub-menu.
        if (singlePlayerSubMenu != null) singlePlayerSubMenu.SetActive(false);
    }

    public void LoadSceneByName(string sceneName)
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }

    public void StartNewGame()
    {
        GameSession.IsLoadingGame = false;
        // Optional: Delete old save? SaveSystem.DeleteSaveFile();
        SceneManager.LoadScene("SingleplayerScene");
    }

    public void ContinueGame()
    {
        if (SaveSystem.HasSaveFile())
        {
            GameSession.IsLoadingGame = true;
            SceneManager.LoadScene("SingleplayerScene");
        }
    }

    public void SaveGame()
    {
        // Find player if not cached
        if (playerTransform == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerTransform = p.transform;
        }

        if (playerTransform != null)
        {
            SaveSystem.SavePlayer(playerTransform, SceneManager.GetActiveScene().buildIndex);
        }
        else
        {
            Debug.LogError("Cannot Save: Player Transform not found!");
        }
    }

    // --- Existing Pause/Menu Logic (Unchanged) ---

    private void HandleEscapeInput()
    {
        string sceneName = SceneManager.GetActiveScene().name;

        if (sceneName == "MainMenu")
        {
            if (settingsCanvas != null && settingsCanvas.activeSelf)
            {
                SwitchToCanvas(menuCanvas);
                return;
            }
            // If in sub-menu, go back
            if (singlePlayerSubMenu != null && singlePlayerSubMenu.activeSelf)
            {
                BackToMainMenu();
                return;
            }
            return;
        }

        if (sceneName == "CustomizationScene")
        {
            ReturnToMainMenu();
            return;
        }

        if (isGamePaused)
        {
            if (pauseSettings != null && pauseSettings.activeSelf) OpenPauseButtons();
            else ResumeGame();
        }
        else
        {
            PauseGame();
        }
    }

    public void PauseGame()
    {
        isGamePaused = true;
        // Assuming you removed Time.timeScale per previous request, relying on input disable
        // If physics pause is needed, add Time.timeScale = 0f here.
        if (pauseMenuRoot != null) pauseMenuRoot.SetActive(true);
        OpenPauseButtons();
    }

    public void ResumeGame()
    {
        isGamePaused = false;
        if (pauseMenuRoot != null) pauseMenuRoot.SetActive(false);
    }

    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }

    public void OpenPauseButtons()
    {
        if (pauseButtons != null) pauseButtons.SetActive(true);
        if (pauseSettings != null) pauseSettings.SetActive(false);
    }

    public void OpenPauseSettings()
    {
        if (pauseButtons != null) pauseButtons.SetActive(false);
        if (pauseSettings != null) pauseSettings.SetActive(true);
    }

    public void OpenMainMenuSettings()
    {
        SwitchToCanvas(settingsCanvas);
    }

    private void SwitchToCanvas(GameObject target)
    {
        GameObject[] allMenus = { menuCanvas, settingsCanvas, winCanvas, loseCanvas, singlePlayerSubMenu };

        foreach (var canvas in allMenus)
        {
            if (canvas != null) canvas.SetActive(false);
        }

        if (target != null) target.SetActive(true);
    }

    private void CloseAllMenus()
    {
        SwitchToCanvas(null);
        if (pauseMenuRoot != null) pauseMenuRoot.SetActive(false);
    }

    public bool IsGamePaused()
    {
        return isGamePaused;
    }
}