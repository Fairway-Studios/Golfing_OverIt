using UnityEngine;
using TMPro;

public class WinUIFeedback : MonoBehaviour
{
    private TextMeshProUGUI feedbackText;
    private GameManager gameManager;

    void Awake()
    {
        feedbackText = GetComponent<TextMeshProUGUI>();
        gameManager = Object.FindFirstObjectByType<GameManager>();
    }

    public void DisplayResults()
    {
        if (feedbackText == null || gameManager == null) return;

        int strokes = gameManager.GetStrokeCount();
        float elapsed = gameManager.GetElapsedTime();

        int minutes = Mathf.FloorToInt(elapsed / 60f);
        int seconds = Mathf.FloorToInt(elapsed % 60f);

        feedbackText.text = $"Strokes: {strokes}\nTime: {minutes:00}:{seconds:00}";
    }
}