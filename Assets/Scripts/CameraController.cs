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
    [SerializeField] private float heightFollowSpeed = 1f;
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
    private float horizontalVelocity;
    private float verticalVelocity;
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

            //set the balls position at game start 
            baseHeight = ball.position.y;
        }
        if (cameraFollowTarget != null && ball != null)
        {
            cameraFollowTarget.position = new Vector3(ball.position.x, ball.position.y, 0);
        }
    }

    void LateUpdate()
    {
        if (ball == null || cameraFollowTarget == null) return;

        // Check if automatic tracking should resume
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

        Vector2 targetLookAhead = Vector2.zero;
        if (ballIsMoving)
        {
            targetLookAhead = ballVelocity.normalized * lookAheadDistance;
            ballWasMoving = true;
        }
        else if (ballWasMoving)
        {
            targetLookAhead = Vector2.zero;
        }

        currentLookAhead.x = Mathf.Lerp(currentLookAhead.x, targetLookAhead.x, lookAheadSmoothing * Time.deltaTime);
        currentLookAhead.y = Mathf.Lerp(currentLookAhead.y, targetLookAhead.y, lookAheadSmoothing * Time.deltaTime);

        Vector3 targetPos = ball.position + currentLookAhead;
        targetPos.y = Mathf.Clamp(targetPos.y, baseHeight, baseHeight + maxHeightOffset);
        targetPos.z = 0;

        float smoothX = Mathf.SmoothDamp(cameraFollowTarget.position.x, targetPos.x, ref horizontalVelocity, horizontalSmoothTime);

        float verticalSmoothTime = ballIsMoving ? (1f / heightFollowSpeed) : (1f / heightReturnSpeed);
        float smoothY = Mathf.SmoothDamp(cameraFollowTarget.position.y, targetPos.y, ref verticalVelocity, verticalSmoothTime);

        cameraFollowTarget.position = new Vector3(smoothX, smoothY, 0);

        if (!ballIsMoving && ballWasMoving)
        {
            ballWasMoving = false;
        }
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