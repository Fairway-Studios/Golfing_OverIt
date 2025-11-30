using UnityEngine;
using Unity.Cinemachine;

public class CameraController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform ball;
    [SerializeField] private CinemachineCamera virtualCamera;

    [Header("Camera Settings")]
    [SerializeField] private float baseHeight = 0f;
    [SerializeField] private float lookAheadDistance = 3f;
    [SerializeField] private float lookAheadSmoothing = 5f;

    [Header("Vertical Tracking")]
    [SerializeField] private float maxHeightOffset = 5f;
    [SerializeField] private float heightFollowSpeed = 2f;
    [SerializeField] private float heightReturnSpeed = 1f;
    [SerializeField] private float ballStoppedThreshold = 0.5f;

    [Header("Horizontal Smoothing")]
    [SerializeField] private float horizontalSmoothTime = 0.3f;

    private Transform cameraFollowTarget;
    private Rigidbody2D ballRb;
    private Vector3 currentLookAhead;
    private float currentHeight;
    private float horizontalVelocity;
    private bool ballWasMoving = false;

    void Start()
    {
        GameObject targetObj = new GameObject("CameraTarget");
        cameraFollowTarget = targetObj.transform;

        if (virtualCamera != null)
        {
            virtualCamera.Follow = cameraFollowTarget;
        }

        if (ball != null)
        {
            ballRb = ball.GetComponent<Rigidbody2D>();
            currentHeight = baseHeight;
        }

        if (cameraFollowTarget != null && ball != null)
        {
            cameraFollowTarget.position = new Vector3(ball.position.x, baseHeight, 0);
        }
    }

    void LateUpdate()
    {
        if (ball == null || cameraFollowTarget == null) return;

        UpdateCameraPosition();
    }

    void UpdateCameraPosition()
    {
        Vector2 ballVelocity = ballRb != null ? ballRb.linearVelocity : Vector2.zero;
        float ballSpeed = ballVelocity.magnitude;
        bool ballIsMoving = ballSpeed > ballStoppedThreshold;

        // Calculate look-ahead based on ball's horizontal velocity
        float targetLookAhead = 0f;
        if (ballIsMoving)
        {
            // Look ahead in direction of movement
            targetLookAhead = Mathf.Sign(ballVelocity.x) * lookAheadDistance;
            ballWasMoving = true;
        }
        else if (ballWasMoving)
        {
            targetLookAhead = currentLookAhead.x;
        }

        currentLookAhead.x = Mathf.Lerp(currentLookAhead.x, targetLookAhead, lookAheadSmoothing * Time.deltaTime);

        float targetX = ball.position.x + currentLookAhead.x;
        float smoothX = Mathf.SmoothDamp(cameraFollowTarget.position.x, targetX, ref horizontalVelocity, horizontalSmoothTime);

        // === VERTICAL POSITION ===

        float targetHeight = baseHeight;

        if (ballIsMoving)
        {
            // Ball is airborne and moving - follow it (with limit)
            float heightDifference = ball.position.y - baseHeight;
            targetHeight = baseHeight + Mathf.Min(heightDifference * 0.5f, maxHeightOffset);

            // Follow upward quickly
            currentHeight = Mathf.Lerp(currentHeight, targetHeight, heightFollowSpeed * Time.deltaTime);
        }
        else
        {
            // Ball stopped or on ground - return to base height
            currentHeight = Mathf.Lerp(currentHeight, baseHeight, heightReturnSpeed * Time.deltaTime);

            if (!ballIsMoving && ballWasMoving)
            {
                ballWasMoving = false;
            }
        }

        cameraFollowTarget.position = new Vector3(smoothX, currentHeight, cameraFollowTarget.position.z);
    }

    // Optional: Call this to reset camera when player teleports
    public void ResetCamera(Vector3 newPosition)
    {
        if (cameraFollowTarget != null)
        {
            cameraFollowTarget.position = new Vector3(newPosition.x, baseHeight, cameraFollowTarget.position.z);
            currentHeight = baseHeight;
            currentLookAhead = Vector3.zero;
            horizontalVelocity = 0f;
        }
    }

    public void SetBaseHeight(float newHeight)
    {
        baseHeight = newHeight;
    }
}