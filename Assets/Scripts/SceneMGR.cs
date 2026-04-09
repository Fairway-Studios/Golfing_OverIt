using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class SceneMGR : MonoBehaviour
{
    [Header("Main Menu UI")]
    [SerializeField] private GameObject menuCanvas;
    [SerializeField] private GameObject settingsCanvas;

    [Header("Singleplayer Mode UI")]
    [SerializeField] private GameObject singlePlayerSubMenu;
    [SerializeField] private Button continueButton;

    [Header("Main Menu Focus Targets")]
    [SerializeField] private GameObject newGameButton;
    [SerializeField] private GameObject singlePlayerMainButton;

    [Header("Pause Menu Focus Targets")]
    [SerializeField] private GameObject pauseFirstButton;
    [SerializeField] private GameObject pauseSettingsButton;
    [SerializeField] private GameObject settingsFirstSlider;
    // --- NEW: Controls Menu Focus Targets ---
    [SerializeField] private GameObject pauseControlsOpenButton; // The button on the pause menu that opens controls
    [SerializeField] private GameObject controlsFirstButton;     // The "Back/Done" button inside the controls menu

    [Header("Game Over UI")]
    [SerializeField] private GameObject winCanvas;
    [SerializeField] private GameObject loseCanvas;

    [Header("Pause System UI")]
    [SerializeField] private GameObject pauseMenuRoot;
    [SerializeField] private GameObject pauseButtons;
    [SerializeField] private GameObject pauseSettings;
    // --- NEW: Controls Menu Panel ---
    [SerializeField] private GameObject pauseControlsInfo;

    [SerializeField] private GameManager gameManager;

    private GameObject[] balls;
    private Dictionary<KeyCode, Action> keyActions;
    private bool isGamePaused = false;
    private Transform playerTransform;

    private void Start()
    {
        balls = GameObject.FindGameObjectsWithTag("GolfBall");

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
            if (singlePlayerSubMenu != null) singlePlayerSubMenu.SetActive(false);

            if (EventSystem.current != null && singlePlayerMainButton != null)
            {
                StartCoroutine(HighlightButtonDelayed(singlePlayerMainButton));
            }
        }
        else
        {
            CloseAllMenus();
        }
    }

    private void Update()
    {
        if (keyActions != null)
        {
            foreach (var entry in keyActions)
            {
                if (Input.GetKeyDown(entry.Key)) entry.Value?.Invoke();
            }
        }

        if (Gamepad.current != null)
        {
            var eastButton = Gamepad.current.buttonEast;
            var startButton = Gamepad.current.startButton;

            if (startButton != null && startButton.wasPressedThisFrame)
            {
                HandleEscapeInput();
            }
            else if (eastButton != null && eastButton.wasPressedThisFrame)
            {
                string sceneName = SceneManager.GetActiveScene().name;

                if (sceneName == "MainMenu" || sceneName == "CustomizationScene" || isGamePaused)
                {
                    HandleEscapeInput();
                }
            }
        }
    }

    public void OpenSinglePlayerMenu()
    {
        if (singlePlayerSubMenu != null)
        {
            singlePlayerSubMenu.SetActive(true);

            if (continueButton != null)
            {
                // Disable the button for a split second while we ask PlayFab
                continueButton.interactable = false;

                // Ask PlayFab, and when it answers, turn the button on or off
                SaveSystem.CheckHasSaveFile(hasSave =>
                {
                    continueButton.interactable = hasSave;
                });
            }

            if (newGameButton != null)
            {
                StartCoroutine(HighlightButtonDelayed(newGameButton));
            }
        }
    }

    public void BackToMainMenu()
    {
        if (singlePlayerSubMenu != null)
        {
            singlePlayerSubMenu.SetActive(false);

            if (singlePlayerMainButton != null)
            {
                StartCoroutine(HighlightButtonDelayed(singlePlayerMainButton));
            }
        }
    }

    private IEnumerator HighlightButtonDelayed(GameObject targetButton)
    {
        EventSystem.current.SetSelectedGameObject(null);
        yield return null;
        EventSystem.current.SetSelectedGameObject(targetButton);
    }

    public void LoadSceneByName(string sceneName)
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }

    public void StartNewGame()
    {
        GameSession.IsLoadingGame = false;
        SaveSystem.DeleteSaveFile();
        SceneManager.LoadScene("SingleplayerScene");
    }

    public void ContinueGame()
    {
        // Ask PlayFab to double-check the save exists before loading the scene
        SaveSystem.CheckHasSaveFile(hasSave =>
        {
            if (hasSave)
            {
                GameSession.IsLoadingGame = true;
                SceneManager.LoadScene("SingleplayerScene");
            }
            else
            {
                Debug.LogWarning("Tried to continue, but PlayFab couldn't find a save.");
            }
        });
    }

    public void SaveGame()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        GameObject ball = GameObject.FindGameObjectWithTag("GolfBall");

        if (p != null && ball != null)
        {
            int currentStrokes = gameManager.GetStrokeCount();
            float currentTime = gameManager.GetElapsedTime();

            SaveSystem.SavePlayer(p.transform, ball.transform, SceneManager.GetActiveScene().buildIndex, currentStrokes, currentTime);
            AchievementManager.Instance.UnlockAchievement("FIRST_SAVE");
        }
        else
        {
            Debug.LogError("Cannot Save: Could not find either the Player or the Golf Ball in the scene!");
        }
    }

    private void HandleEscapeInput()
    {
        string sceneName = SceneManager.GetActiveScene().name;

        if (sceneName == "MainMenu")
        {
            if (settingsCanvas != null && settingsCanvas.activeSelf)
            {
                SwitchToCanvas(menuCanvas);

                if (singlePlayerMainButton != null)
                {
                    StartCoroutine(HighlightButtonDelayed(singlePlayerMainButton));
                }
                return;
            }
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
            if (pauseSettings != null && pauseSettings.activeSelf)
            {
                ClosePauseSettings();
            }
            // --- NEW: Check if Controls menu is open, if so, back out of it ---
            else if (pauseControlsInfo != null && pauseControlsInfo.activeSelf)
            {
                CloseControlsMenu();
            }
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
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        gameManager.StopTimer();
        if (pauseMenuRoot != null) pauseMenuRoot.SetActive(true);
        OpenPauseButtons();

        if (pauseFirstButton != null)
        {
            StartCoroutine(HighlightButtonDelayed(pauseFirstButton));
        }
    }

    public void ResumeGame()
    {
        isGamePaused = false;
        Cursor.lockState = CursorLockMode.Confined;
        Cursor.visible = false;
        gameManager.StartTimer();
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
        if (pauseControlsInfo != null) pauseControlsInfo.SetActive(false); // Make sure Controls hide
    }

    // --- Settings Menu Logic ---
    public void OpenPauseSettings()
    {
        if (pauseButtons != null) pauseButtons.SetActive(false);
        if (pauseControlsInfo != null) pauseControlsInfo.SetActive(false);
        if (pauseSettings != null) pauseSettings.SetActive(true);

        if (settingsFirstSlider != null)
        {
            StartCoroutine(HighlightButtonDelayed(settingsFirstSlider));
        }
    }

    public void ClosePauseSettings()
    {
        OpenPauseButtons();

        if (pauseSettingsButton != null)
        {
            StartCoroutine(HighlightButtonDelayed(pauseSettingsButton));
        }
    }

    // --- NEW: Controls Menu Logic ---
    public void OpenControlsMenu()
    {
        if (pauseButtons != null) pauseButtons.SetActive(false);
        if (pauseSettings != null) pauseSettings.SetActive(false);

        if (pauseControlsInfo != null) pauseControlsInfo.SetActive(true);

        if (controlsFirstButton != null)
        {
            StartCoroutine(HighlightButtonDelayed(controlsFirstButton));
        }
    }

    public void CloseControlsMenu()
    {
        OpenPauseButtons();

        if (pauseControlsOpenButton != null)
        {
            StartCoroutine(HighlightButtonDelayed(pauseControlsOpenButton));
        }
    }
    // --------------------------------

    public void OpenMainMenuSettings()
    {
        SwitchToCanvas(settingsCanvas);
    }

    private void SwitchToCanvas(GameObject target)
    {
        GameObject[] allMenus = { menuCanvas, settingsCanvas, winCanvas, loseCanvas, singlePlayerSubMenu, pauseControlsInfo };

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