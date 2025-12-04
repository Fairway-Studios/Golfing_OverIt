using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using PlayFab;
using PlayFab.ClientModels;

public class LoginManager : MonoBehaviour
{
    public TMP_InputField usernameInput;
    public TMP_InputField passwordInput;
    public TextMeshProUGUI feedbackText;

    // NOTE: PlayerPrefs keys are no longer needed

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

        Login(); // Automatically log in after account creation
    }

    private void OnPlayFabError(PlayFabError error)
    {
        feedbackText.text = "Login Error: " + error.ErrorMessage;
        Debug.LogError("PlayFab Error Report: " + error.GenerateErrorReport());
    }
}
