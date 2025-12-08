using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneMGR : MonoBehaviour
{
    [Header("Main Menu UI")]
    [SerializeField] private GameObject menuCanvas;       
    [SerializeField] private GameObject settingsCanvas;   

    [Header("Game Over UI")]
    [SerializeField] private GameObject winCanvas;
    [SerializeField] private GameObject loseCanvas;

    [Header("Pause System UI")]
    [SerializeField] private GameObject pauseMenuRoot;    
    [SerializeField] private GameObject pauseButtons;     
    [SerializeField] private GameObject pauseSettings;    

    
    private Dictionary<KeyCode, Action> keyActions;
    private bool isGamePaused = false;

    private void Start()
    {
        
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

    

    private void HandleEscapeInput()
    {
        string sceneName = SceneManager.GetActiveScene().name;

        
        if (sceneName == "MainMenu")
        {
            
            if (settingsCanvas != null && settingsCanvas.activeSelf)
            {
                SwitchToCanvas(menuCanvas);
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
                OpenPauseButtons(); 
            }
            else
            {
                ResumeGame(); 
            }
        }
        else
        {
            PauseGame(); 
        }
    }

    

    public void PauseGame()
    {
        isGamePaused = true;
        

        
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

    public void LoadSceneByName(string sceneName)
    {
        Time.timeScale = 1f; 
        SceneManager.LoadScene(sceneName);
    }

    
    private void SwitchToCanvas(GameObject target)
    {
        
        GameObject[] allMenus = { menuCanvas, settingsCanvas, winCanvas, loseCanvas };

        
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
}