using UnityEngine;

public class QuitGameHandler : MonoBehaviour
{
    public void QuitToDesktop()
    {
        Debug.Log("Quit Game Requested. Exiting to Desktop.");

        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}