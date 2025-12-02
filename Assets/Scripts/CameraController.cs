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

    [Header("Manual Camera Control")]
    [SerializeField] private float manualMoveSpeed = 10f;
    [SerializeField] private float returnToTrackingDelay = 0.5f;

    private Transform cameraFollowTarget;
    private Rigidbody2D ballRb;
    private Vector3 currentLookAhead;
    private float currentHeight;
    private float horizontalVelocity;
    private bool ballWasMoving = false;

    // Manual control
    private bool isManualControl = false;
    private Vector2 manualInput = Vector2.zero;
    private float lastManualInputTime = -999f;
    private Vector3 manualCameraOffset = Vector3.zero;

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

        // Check if we should return to automatic tracking
        if (isManualControl && Time.time - lastManualInputTime > returnToTrackingDelay)
        {
            isManualControl = false;
            manualCameraOffset = Vector3.zero;
        }

        UpdateCameraPosition();
    }

    void UpdateCameraPosition()
    {
        Vector2 ballVelocity = ballRb != null ? ballRb.linearVelocity : Vector2.zero;
        float ballSpeed = ballVelocity.magnitude;
        bool ballIsMoving = ballSpeed > ballStoppedThreshold;

        // Manual Camera Control
        if (isManualControl)
        {
            // Move camera manually based on input
            manualCameraOffset += new Vector3(manualInput.x, manualInput.y, 0) * manualMoveSpeed * Time.deltaTime;

            Vector3 manualPosition = ball.position + manualCameraOffset;
            manualPosition.z = 0;

            cameraFollowTarget.position = manualPosition;
            return;
        }

        // Automatic Camera Control

        // Calculate look-ahead based on ball's horizontal velocity
        float targetLookAhead = 0f;
        if (ballIsMoving)
        {
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

        // Camera height control
        float targetHeight = baseHeight;

        if (ballIsMoving)
        {
            float heightDifference = ball.position.y - baseHeight;
            targetHeight = baseHeight + Mathf.Min(heightDifference * 0.5f, maxHeightOffset);
            currentHeight = Mathf.Lerp(currentHeight, targetHeight, heightFollowSpeed * Time.deltaTime);
        }
        else
        {
            currentHeight = Mathf.Lerp(currentHeight, baseHeight, heightReturnSpeed * Time.deltaTime);

            if (!ballIsMoving && ballWasMoving)
            {
                ballWasMoving = false;
            }
        }

        cameraFollowTarget.position = new Vector3(smoothX, currentHeight, cameraFollowTarget.position.z);
    }

    // Called by InputController when manual camera control input is received
    public void OnCameraMove(Vector2 input)
    {
        manualInput = input;

        if (input.magnitude > 0.1f)
        {
            if (!isManualControl)
            {
                // Switching to manual control - record current offset
                manualCameraOffset = cameraFollowTarget.position - ball.position;
                isManualControl = true;
            }

            lastManualInputTime = Time.time;
        }
    }

    public void SetBaseHeight(float newHeight)
    {
        baseHeight = newHeight;
    }

    // Force return to automatic tracking
    public void ResetToAutomatic()
    {
        isManualControl = false;
        manualCameraOffset = Vector3.zero;
        manualInput = Vector2.zero;
    }
}