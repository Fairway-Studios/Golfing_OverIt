using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneMGR : MonoBehaviour
{
    // Map keys to actions
    private Dictionary<KeyCode, Action> keyActions;

    [SerializeField] private GameObject menuCanvas;
    [SerializeField] private GameObject settingsCanvas;
    [SerializeField] private GameObject winCanvas;
    [SerializeField] private GameObject loseCanvas;

    private void Start()
    {
        settingsCanvas.SetActive(false);

        keyActions = new Dictionary<KeyCode, Action>
        {
            { KeyCode.Escape, HandleEscape },
            { KeyCode.S, ToggleSettings },
            { KeyCode.W, ToggleWin },
            { KeyCode.L, ToggleLoss },
        };
    }

    private void Update()
    {
        foreach (var entry in keyActions)
        {
            if (Input.GetKeyDown(entry.Key))
            {
                entry.Value?.Invoke();
            }
        }
    }

    private void HandleEscape()
    {
        LoadSceneByName("MainMenu");
    }
    public void ToggleSettings()
    {
        if (menuCanvas != null) menuCanvas.SetActive(false);
        if (settingsCanvas != null) settingsCanvas.SetActive(true);
    }
    private void ToggleWin()
    {
        if (menuCanvas != null) menuCanvas.SetActive(false);
        if (winCanvas != null) winCanvas.SetActive(true);
    }
    private void ToggleLoss()
    {
        if (menuCanvas != null) menuCanvas.SetActive(false);
        if (loseCanvas != null) loseCanvas.SetActive(true);
    }

    public void LoadSceneByName(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }
}
