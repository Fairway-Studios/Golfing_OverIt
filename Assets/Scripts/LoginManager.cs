using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using PlayFab;
using PlayFab.ClientModels;
using System.Data.SqlTypes;

public class LoginManager : MonoBehaviour
{
    [Header("UI Components")]
    public TMP_InputField usernameInput;
    public TMP_InputField passwordInput;
    public TextMeshProUGUI feedbackText;

    [Header("Buttons")]
    public Button loginButton;
    public Button createAccountButton;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            SelectNextElement();
        }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            GameObject currentSelection = EventSystem.current.currentSelectedGameObject;

            if (currentSelection == createAccountButton.gameObject)
            {
                CreateAccount();
            }
            else
            {
                Login();
            }
        }
    }

    private void SelectNextElement()
    {
        GameObject current = EventSystem.current.currentSelectedGameObject;

        if (current == usernameInput.gameObject)
        {
            passwordInput.Select();
        }
        else if (current == passwordInput.gameObject)
        {
            loginButton.Select();
        }
        else if (current == loginButton.gameObject)
        {
            createAccountButton.Select();
        }
        else if (current == createAccountButton.gameObject)
        {
            usernameInput.Select();
        }
        else
        {
            usernameInput.Select(); // if nothing is selected, start with username input
        }
    }

    public void Login()
    {
        if (string.IsNullOrEmpty(usernameInput.text) || string.IsNullOrEmpty(passwordInput.text))
        {
            feedbackText.text = "Please enter both username and password!";
            return;
        }

        feedbackText.text = "Connecting to server...";

        var request = new LoginWithPlayFabRequest()
        {
            Username = usernameInput.text,
            Password = passwordInput.text,
        };

        // PlayFabClientAPI.LoginWithPlayFab takes the request, a Success Callback, and an Error Callback.

        PlayFabClientAPI.LoginWithPlayFab(request, OnLoginSuccess, OnPlayFabError);
    }

    public void CreateAccount()
    {
        if (string.IsNullOrEmpty(usernameInput.text) || string.IsNullOrEmpty(passwordInput.text))
        {
            feedbackText.text = "Please enter both username and password!";
            return;
        }

        var request = new RegisterPlayFabUserRequest
        {
            Username = usernameInput.text,
            Password = passwordInput.text,
            RequireBothUsernameAndEmail = false
        };

        PlayFabClientAPI.RegisterPlayFabUser(request, OnRegisterSuccess, OnPlayFabError);
    }

    // CallbacK Methods (Sucess and Error)

    // This executes Only if the Playfab serverconfirms a successful login

    private void OnLoginSuccess(LoginResult result)
    {
        feedbackText.text = "Login Successful! Player ID: " + result.PlayFabId;
        Debug.Log("Successfully logged in with PlayFab ID: " + result.PlayFabId);

        // Load the main game scene after successful login
        SceneManager.LoadScene("MainMenu");
    }

    // This is only executed if PlayFab server confirms successful account creation.

    private void OnRegisterSuccess(RegisterPlayFabUserResult result)
    {
        feedbackText.text = "Account Created Successfully!";
    }

    private void OnPlayFabError(PlayFabError error)
    {
        if (error.Error == PlayFabErrorCode.UsernameNotAvailable)
        {
            feedbackText.text = "That Account Already Exists!";
        }
        else
        {
            feedbackText.text = "Error: " + error.ErrorMessage;
        }

        Debug.LogError("PlayFab Error Report: " + error.GenerateErrorReport());
    }
}
