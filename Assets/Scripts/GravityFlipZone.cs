using System.Collections;
using UnityEngine;

public class GravityFlipZone : MonoBehaviour
{
    public bool flipGravity = true;
    public float originalGravityScale = 2f;
    public Transform playerVisual;

    private Vector3 originalOffset;
    private Vector3 flippedOffset;
    private GolfBallController ballController;
    private GameManager gameManager;
    private static bool s_gravityFlipped = false;

    // ADDED: separate visual state from gameplay gravity state
    private static bool s_visualGravityFlipped = false;

    // ADDED: lets us delay the visual flip slightly so teleport/position correction can happen first
    [SerializeField] private float visualFlipDelay = 4f;

    // ADDED: track running coroutine so repeated trigger hits do not stack flips
    private Coroutine visualFlipCoroutine;

    [SerializeField] private CameraController cameraController;

    void Start()
    {
        ballController = Object.FindFirstObjectByType<GolfBallController>();
        gameManager = Object.FindFirstObjectByType<GameManager>();

        if (gameManager != null)
        {
            originalOffset = gameManager.GetPlayerOffsetFromBall();
            flippedOffset = new Vector3(originalOffset.x, -originalOffset.y, originalOffset.z);
        }

        // ADDED: keep visual state in sync on scene start
        s_visualGravityFlipped = s_gravityFlipped;
    }

    void LateUpdate()
    {
        // Only handle visual rotation — position handled by GameManager now
        if (playerVisual != null)
            playerVisual.localEulerAngles = new Vector3(0f, 0f, s_visualGravityFlipped ? 180f : 0f);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("GolfBall")) return;

        Rigidbody2D rb = other.GetComponent<Rigidbody2D>();
        if (rb == null) return;

        if (flipGravity)
        {
            // ENTRY — gameplay state changes immediately
            s_gravityFlipped = true;
            rb.gravityScale = -1f;

            if (gameManager != null)
                gameManager.SetPlayerOffsetFromBall(flippedOffset);

            if (cameraController != null)
                cameraController.SwapVerticalBias();

            // ADDED: visual flip happens after a short delay
            StartDelayedVisualFlip(true);
        }
        else
        {
            // EXIT — gameplay state changes immediately
            s_gravityFlipped = false;
            rb.gravityScale = originalGravityScale;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
            rb.AddForce(Vector2.down * 3f, ForceMode2D.Impulse);

            if (gameManager != null)
                gameManager.SetPlayerOffsetFromBall(originalOffset);

            if (cameraController != null)
                cameraController.SwapVerticalBias();

            // ADDED: visual unflip happens after a short delay
            StartDelayedVisualFlip(false);
        }
    }

    // ADDED
    private void StartDelayedVisualFlip(bool flippedState)
    {
        if (visualFlipCoroutine != null)
            StopCoroutine(visualFlipCoroutine);

        visualFlipCoroutine = StartCoroutine(ApplyVisualFlipAfterDelay(flippedState));
    }

    // ADDED
    private IEnumerator ApplyVisualFlipAfterDelay(bool flippedState)
    {
        if (visualFlipDelay > 0f)
            yield return new WaitForSeconds(visualFlipDelay);
        else
            yield return null;

        s_visualGravityFlipped = flippedState;
        visualFlipCoroutine = null;
    }

    public static bool IsGravityFlipped() => s_gravityFlipped;
}