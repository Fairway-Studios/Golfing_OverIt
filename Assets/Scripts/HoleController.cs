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
            return true;

        if (speed >= impossibleSinkSpeed)
            return false;

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

        gameOverPanel.SetActive(true);
    }
}