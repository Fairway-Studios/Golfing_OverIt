using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class LoginManager : MonoBehaviour
{
    public TMP_InputField usernameInput;
    public TMP_InputField passwordInput;
    public TextMeshProUGUI feedbackText;

    private string savedUsernameKey = "SavedUsername";
    private string savedPasswordKey = "SavedPassword";

    public void Login()
    {
        string inputUser = usernameInput.text;
        string inputPass = passwordInput.text;

        string savedUser = PlayerPrefs.GetString(savedUsernameKey, "");
        string savedPass = PlayerPrefs.GetString(savedPasswordKey, "");

        if (string.IsNullOrEmpty(savedUser) || string.IsNullOrEmpty(savedPass))
        {
            feedbackText.text = "No account found. Please create one first.";
            return;
        }

        if (inputUser == savedUser && inputPass == savedPass)
        {
            feedbackText.text = "Login successful!";
            SceneManager.LoadScene("MainMenu"); // Load the menu in your screenshot
        }
        else
        {
            feedbackText.text = "Invalid username or password!";
        }
    }

    public void CreateAccount()
    {
        string newUser = usernameInput.text;
        string newPass = passwordInput.text;

        if (string.IsNullOrEmpty(newUser) || string.IsNullOrEmpty(newPass))
        {
            feedbackText.text = "Username or password cannot be empty!";
            return;
        }

        PlayerPrefs.SetString(savedUsernameKey, newUser);
        PlayerPrefs.SetString(savedPasswordKey, newPass);
        PlayerPrefs.Save();

        feedbackText.text = "Account created successfully!";
    }
}
