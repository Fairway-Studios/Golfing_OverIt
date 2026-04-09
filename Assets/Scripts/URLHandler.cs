using UnityEngine;

public class URLHandler : MonoBehaviour
{
    [Header("Link Settings")]
    [Tooltip("Paste your Google Doc Share link here")]
    [SerializeField] private string docsLink = "https://docs.google.com/document/d/1BuCSe0qeryz2J0ohqxkKaOOMBJNgJxklUJ9rVOZkcJw/edit?usp=sharing";

    // This is the method the button will call
    public void OpenDocsLink()
    {
        if (!string.IsNullOrEmpty(docsLink))
        {
            Application.OpenURL(docsLink);
            Debug.Log("Opening URL: " + docsLink);
        }
        else
        {
            Debug.LogWarning("Docs link is empty! Please assign it in the Inspector.");
        }
    }
}