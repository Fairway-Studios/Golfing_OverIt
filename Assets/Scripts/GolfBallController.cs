using UnityEngine;

public class GolfBallController : MonoBehaviour
{
    [SerializeField] private float stoppedVelocityThreshold = 0.2f;
    [SerializeField] private float stoppedCheckDuration = 0.5f;
    [SerializeField] private Transform player;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Vector3 playerOffsetFromBall = new Vector3(-0.3f, 1.4f, 0f);

    private Rigidbody2D rb;
    private float timeStationary = 0f;
    private bool hasTeleported = false;
    private bool shouldTeleport = false;
    private Vector3 hitStartPosition;
    private bool hasRecordedHitStart = false;
    private CameraController camController;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        camController = Object.FindFirstObjectByType<CameraController>();
    }

    void FixedUpdate()
    {
        float speed = rb.linearVelocity.magnitude;

        if (!hasRecordedHitStart && speed > stoppedVelocityThreshold)
        {
            hitStartPosition = transform.position;
            hasRecordedHitStart = true;
        }

        if (!hasTeleported && speed > stoppedVelocityThreshold)
        {
            timeStationary = 0f;
        }
        else if (!hasTeleported && speed <= stoppedVelocityThreshold)
        {
            timeStationary += Time.fixedDeltaTime;
            if (timeStationary >= stoppedCheckDuration)
            {
                rb.linearVelocity = Vector2.zero;
                shouldTeleport = true;
            }
        }
    }

    private void Update()
    {
        if (shouldTeleport)
        {
            TeleportToPosition();
            shouldTeleport = false;
            hasTeleported = true;
        }
    }

    void TeleportToPosition()
    {
        Vector3 ballPosition = transform.position;

        if (hasRecordedHitStart)
        {
            float distance = Vector2.Distance(hitStartPosition, ballPosition);
            Debug.Log($"Shot Distance: {distance:F2}m");
            hasRecordedHitStart = false;
        }

        // Teleport player
        player.position = ballPosition + playerOffsetFromBall;

        // Update camera
        cameraTransform.position = new Vector3(ballPosition.x, ballPosition.y, cameraTransform.position.z);

        if (camController != null)
        {
            camController.SetBaseHeight(player.position.y);
        }

        // Notify controllers to disable swinging
        InputController[] controllers = Object.FindObjectsByType<InputController>(FindObjectsSortMode.None);
        foreach (var controller in controllers)
        {
            controller.OnPlayerTeleported();
        }

        Invoke(nameof(ResetTeleportFlag), 0.1f);
    }

    void ResetTeleportFlag()
    {
        hasTeleported = false;
        timeStationary = 0f;
    }
}