using UnityEngine;

public class GravityFlipZone : MonoBehaviour
{
    public bool flipGravity = true;
    public float originalGravityScale = 1f;
    public Transform playerVisual;

    private Vector3 originalOffset;
    private Vector3 flippedOffset;
    private GolfBallController ballController;
    private GameManager gameManager;
    private static bool s_gravityFlipped = false;

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
    }

    void LateUpdate()
    {
        // Only handle visual rotation — position handled by GameManager now
        if (playerVisual != null)
            playerVisual.localEulerAngles = new Vector3(0f, 0f, s_gravityFlipped ? 180f : 0f);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("GolfBall")) return;

        Rigidbody2D rb = other.GetComponent<Rigidbody2D>();
        if (rb == null) return;

        if (flipGravity)
        {
            // ENTRY — tell GameManager to use flipped offset
            s_gravityFlipped = true;
            rb.gravityScale = -1f;
            if (gameManager != null)
                gameManager.SetPlayerOffsetFromBall(flippedOffset);

            cameraController.InvertCamCompositionY();
        }
        else
        {
            // EXIT — restore GameManager to original offset
            s_gravityFlipped = false;
            rb.gravityScale = originalGravityScale;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
            rb.AddForce(Vector2.down * 3f, ForceMode2D.Impulse);
            if (gameManager != null)
                gameManager.SetPlayerOffsetFromBall(originalOffset);

            cameraController.InvertCamCompositionY();
        }
    }

    public static bool IsGravityFlipped() => s_gravityFlipped;
}