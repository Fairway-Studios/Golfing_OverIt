using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HoleController : MonoBehaviour
{
    [Header("Effects")]
    [SerializeField] private AudioClip holeSFX;
    [SerializeField] private ParticleSystem holeParticles;

    [Header("Game Over UI")]
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private float gameOverDelay = 3f;

    [Header("Sink Probability Thresholds")]
    [SerializeField] private float guaranteedSinkSpeed = 2f;
    [SerializeField] private float impossibleSinkSpeed = 12f;

    [SerializeField] private int impossibleRangeMax = 20;

    private bool ballSunk = false;


    private void Awake()
    {
        if (gameOverPanel == null)
        {
            GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();

            foreach (GameObject obj in allObjects)
            {
                if (obj.name == "WinUI")
                {
                    gameOverPanel = obj;
                    break;
                }
            }
        }

        if (gameOverPanel == null)
            Debug.LogWarning("[Hole] Could not find WinUI, even with inactive search.");
        else
            Debug.Log("[Hole] Found WinUI: " + gameOverPanel.name + " | ActiveSelf: " + gameOverPanel.activeSelf);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (ballSunk) return;

        GolfBallController ball = other.GetComponentInParent<GolfBallController>();
        if (ball == null) return;

        Rigidbody2D ballRb = ball.GetRigidbody();
        float speed = ballRb != null ? ballRb.linearVelocity.magnitude : 0f;

        if (!ShouldSink(speed)) return;

        ballSunk = true;
        if (ballRb != null)
        {
            ballRb.linearVelocity = Vector2.zero;
            ballRb.angularVelocity = 0f;
            ballRb.position = transform.position;
            ballRb.simulated = false;
        }
        ball.DisableTrail();

        StartCoroutine(SinkBall(ball, ballRb));
    }

    private bool ShouldSink(float speed)
    {
        if (speed <= guaranteedSinkSpeed)
        {
            Debug.Log($"[Hole] Speed: {speed:F2} | Guaranteed sink (below {guaranteedSinkSpeed}) | Sinks: True");
            return true;
        }

        if (speed >= impossibleSinkSpeed)
        {
            Debug.Log($"[Hole] Speed: {speed:F2} | Impossible sink (above {impossibleSinkSpeed}) | Sinks: False");
            return false;
        }

        float t = Mathf.InverseLerp(guaranteedSinkSpeed, impossibleSinkSpeed, speed);
        int upperBound = Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(1f, impossibleRangeMax, t)));

        int roll = Random.Range(0, upperBound);
        Debug.Log($"[Hole] Speed: {speed:F2} | Range: 0-{upperBound - 1} | Roll: {roll} | Sinks: {roll == 0}");
        return roll == 0;
    }

    private IEnumerator SinkBall(GolfBallController ball, Rigidbody2D ballRb)
    {
        Vector3 pos = ball.GetPosition();

        if (holeParticles != null)
        {
            holeParticles.transform.position = pos;
            holeParticles.Play();
        }

        if (holeSFX != null)
            SFXManager.Instance.PlaySFX(holeSFX);

        yield return StartCoroutine(ShrinkAndFade(ball));

        ball.gameObject.SetActive(false);

        if (AchievementManager.Instance != null)
        {
            // --- ACHIEVEMENT: Level Complete ---
            AchievementManager.Instance.UnlockAchievement("LEVEL_COMPLETE");
        }

        yield return new WaitForSeconds(gameOverDelay);

        ShowGameOverUI(ball.GetOwnerIndex());
    }

    private IEnumerator ShrinkAndFade(GolfBallController ball)
    {
        float duration = 0.35f;
        float elapsed = 0f;

        Transform ballTransform = ball.transform;
        Vector3 originalScale = ballTransform.localScale;

        SpriteRenderer sr = ball.GetSpriteRenderer();

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            float scale = Mathf.Lerp(1f, 0f, t);
            ballTransform.localScale = originalScale * scale;

            if (sr != null)
            {
                Color c = sr.color;
                c.a = Mathf.Lerp(1f, 0f, t);
                sr.color = c;
            }

            yield return null;
        }

        ballTransform.localScale = originalScale;
        if (sr != null)
        {
            Color c = sr.color;
            c.a = 1f;
            sr.color = c;
        }
    }

    private void ShowGameOverUI(int playerIndex)
    {
        if (gameOverPanel == null) return;

        GameManager gameManager = Object.FindFirstObjectByType<GameManager>();
        if (gameManager != null)
            gameManager.StopTimer();

        Cursor.visible = true;
        gameOverPanel.SetActive(true);

        WinUIFeedback feedback = gameOverPanel.GetComponentInChildren<WinUIFeedback>();
        if (feedback != null)
            feedback.DisplayResults();
    }
}