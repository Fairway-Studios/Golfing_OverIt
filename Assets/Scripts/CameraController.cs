using UnityEngine;
using Unity.Cinemachine;

public class CameraController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform ball;
    [SerializeField] private CinemachineCamera virtualCamera;

    [Header("Look Ahead")]
    [SerializeField] private float lookAheadDistance = 4f;
    [SerializeField] private float lookAheadSmoothing = 4f;

    [Header("Vertical Deadzone")]
    [SerializeField] private float verticalDeadzone = 5f;

    [Header("Smoothing")]
    [SerializeField] private float verticalSmoothTime = 0.25f;
    [SerializeField] private float horizontalSmoothTime = 0.25f;

    [Header("Asymmetric Vertical Tracking")]
    [SerializeField] private float upwardDeadzone = 8f; // Larger deadzone when ball goes up
    [SerializeField] private float downwardDeadzone = 2f; // Smaller deadzone when ball falls
    [SerializeField] private float upwardSmoothTime = 0.5f; // Slower when tracking upward
    [SerializeField] private float downwardSmoothTime = 0.15f; // Faster when tracking downward
    [SerializeField] private float downwardLookAheadMultiplier = 1.5f; // Extra lookahead when falling
    [SerializeField] private float apexVelocityThreshold = 0.5f; // Velocity threshold to detect apex

    [Header("Manual Camera Control")]
    [SerializeField] private float manualMoveSpeed = 15f;
    [SerializeField] private float returnToTrackingDelay = 2f;

    [Header("Swing Tracking")]
    [SerializeField] private bool waitForBothPlayers = true;
    [SerializeField] private Vector3 preSwingCameraOffset = new Vector3(4f, 1f, 0f);

    [Header("Dynamic Zoom")]
    [SerializeField] private float baseOrthographicSize = 5f;
    [SerializeField] private float maxOrthographicSize = 12f;
    [SerializeField] private float speedThreshold = 2f;
    [SerializeField] private float maxSpeed = 20f;
    [SerializeField] private float zoomSmoothTime = 0.3f;

    private Transform followTarget;
    private Rigidbody2D ballRb;

    private Vector3 currentLookAhead;
    private float velX;
    private float velY;

    // Manual camera
    private bool isManual = false;
    private Vector2 manualInput;
    private float lastManualInputTime;
    private Vector3 manualOffset;

    private float lastTargetY;

    // Swing tracking state
    private bool isWaitingForSwings = false;
    private bool player1HasSwung = false;
    private bool player2HasSwung = false;
    private Vector3 frozenCameraPosition;
    private bool isMultiplayer = false;
    private GolfBallController lastHitBall = null;

    // Ball cycling
    private GolfBallController[] allBalls;
    private int currentBallIndex = 0;

    // Dynamic zoom
    private float currentZoomVelocity;
    private float targetOrthographicSize;
    private Vector3 lastCameraPosition;

    // Vertical tracking state
    private float previousBallVelocityY;
    private bool isBallDescending = false;

    void Start()
    {
        // Create dynamic follow target
        GameObject t = new GameObject("CameraTarget");
        followTarget = t.transform;

        if (virtualCamera != null)
        {
            virtualCamera.Follow = followTarget;

            // Initialize zoom
            targetOrthographicSize = baseOrthographicSize;
            if (virtualCamera.Lens.Orthographic)
            {
                virtualCamera.Lens.OrthographicSize = baseOrthographicSize;
            }
        }

        if (ball != null)
        {
            ballRb = ball.GetComponent<Rigidbody2D>();
            followTarget.position = ball.position;
            lastTargetY = ball.position.y;
            lastCameraPosition = followTarget.position;
            previousBallVelocityY = 0f;
        }

        // Detect if multiplayer
        DetectGameMode();

        // Cache all balls
        allBalls = Object.FindObjectsByType<GolfBallController>(FindObjectsSortMode.None);

        PrepareForSwings();
    }

    void DetectGameMode()
    {
        InputController[] controllers = Object.FindObjectsByType<InputController>(FindObjectsSortMode.None);
        isMultiplayer = controllers.Length >= 2;
    }

    void LateUpdate()
    {
        if (ball == null || followTarget == null)
            return;

        if (isManual && Time.time - lastManualInputTime > returnToTrackingDelay)
        {
            isManual = false;
            manualOffset = Vector3.zero;
        }

        UpdateCamera();
        UpdateDynamicZoom();
    }

    private void UpdateCamera()
    {
        Vector2 ballVelocity = ballRb ? ballRb.linearVelocity : Vector2.zero;

        // Detect if ball is descending (has passed apex)
        if (ballRb != null)
        {
            // Ball is descending if velocity changed from positive to negative or is negative and slowing
            if (ballVelocity.y < -apexVelocityThreshold)
            {
                isBallDescending = true;
            }
            else if (ballVelocity.y > apexVelocityThreshold)
            {
                isBallDescending = false;
            }
            // Near apex (small velocity), maintain previous state

            previousBallVelocityY = ballVelocity.y;
        }

        // Manual Mode overrides everything
        if (isManual)
        {
            manualOffset += new Vector3(manualInput.x, manualInput.y, 0f) * manualMoveSpeed * Time.deltaTime;
            Vector3 mpos = ball.position + manualOffset;
            mpos.z = 0;
            followTarget.position = mpos;
            return;
        }

        // If waiting for both players to swing
        if (isWaitingForSwings && waitForBothPlayers && isMultiplayer)
        {
            // Smooth transition to frozen position
            float smoothX = Mathf.SmoothDamp(followTarget.position.x, frozenCameraPosition.x, ref velX, horizontalSmoothTime);
            float smoothY = Mathf.SmoothDamp(followTarget.position.y, frozenCameraPosition.y, ref velY, verticalSmoothTime);
            followTarget.position = new Vector3(smoothX, smoothY, 0);
            return;
        }

        Vector2 targetLookAhead = Vector2.zero;

        // Lookahead based on ball velocity
        if (ballVelocity.magnitude > 0.1f)
        {
            targetLookAhead.x = ballVelocity.x != 0 ? Mathf.Sign(ballVelocity.x) * lookAheadDistance : 0f;

            // Enhanced downward lookahead
            if (ballVelocity.y != 0)
            {
                float yLookAhead = Mathf.Sign(ballVelocity.y) * lookAheadDistance;

                // Apply extra lookahead when ball is descending
                if (isBallDescending && ballVelocity.y < 0)
                {
                    yLookAhead *= downwardLookAheadMultiplier;
                }

                targetLookAhead.y = yLookAhead;
            }
        }

        currentLookAhead.x = Mathf.Lerp(currentLookAhead.x, targetLookAhead.x, Time.deltaTime * lookAheadSmoothing);
        currentLookAhead.y = Mathf.Lerp(currentLookAhead.y, targetLookAhead.y, Time.deltaTime * lookAheadSmoothing);

        Vector3 target = ball.position + currentLookAhead;
        target.z = 0;

        // Horizontal tracking (unchanged)
        float smoothX2 = Mathf.SmoothDamp(followTarget.position.x, target.x, ref velX, horizontalSmoothTime);

        // Asymmetric vertical tracking
        float dy = target.y - lastTargetY;
        bool movingUp = dy > 0;

        // Choose deadzone and smooth time based on direction
        float activeDeadzone = movingUp ? upwardDeadzone : downwardDeadzone;
        float activeSmoothTime = movingUp ? upwardSmoothTime : downwardSmoothTime;

        if (Mathf.Abs(dy) > activeDeadzone)
        {
            lastTargetY = Mathf.SmoothDamp(lastTargetY, target.y, ref velY, activeSmoothTime);
        }

        float smoothY2 = lastTargetY;

        followTarget.position = new Vector3(smoothX2, smoothY2, 0);
    }

    private void UpdateDynamicZoom()
    {
        if (virtualCamera == null || !virtualCamera.Lens.Orthographic)
            return;

        // Calculate camera speed
        Vector3 currentCameraPosition = followTarget.position;
        float cameraSpeed = (currentCameraPosition - lastCameraPosition).magnitude / Time.deltaTime;
        lastCameraPosition = currentCameraPosition;

        // Calculate target zoom based on speed
        if (cameraSpeed < speedThreshold)
        {
            targetOrthographicSize = baseOrthographicSize;
        }
        else
        {
            // Normalize speed between threshold and max speed
            float normalizedSpeed = Mathf.Clamp01((cameraSpeed - speedThreshold) / (maxSpeed - speedThreshold));
            targetOrthographicSize = Mathf.Lerp(baseOrthographicSize, maxOrthographicSize, normalizedSpeed);
        }

        // Smoothly interpolate to target zoom
        float newSize = Mathf.SmoothDamp(
            virtualCamera.Lens.OrthographicSize,
            targetOrthographicSize,
            ref currentZoomVelocity,
            zoomSmoothTime
        );

        virtualCamera.Lens.OrthographicSize = newSize;
    }

    // Called when players are ready to take their shots
    public void PrepareForSwings()
    {
        if (!waitForBothPlayers || !isMultiplayer)
            return;

        isWaitingForSwings = true;
        player1HasSwung = false;
        player2HasSwung = false;
        lastHitBall = null;

        allBalls = Object.FindObjectsByType<GolfBallController>(FindObjectsSortMode.None);

        frozenCameraPosition = ball.position + preSwingCameraOffset;
        frozenCameraPosition.z = 0;

        // Reset velocities for smooth transition
        velX = 0f;
        velY = 0f;

        // Reset vertical tracking state
        isBallDescending = false;
        previousBallVelocityY = 0f;
    }

    // Called by InputController when a player swings
    public void OnPlayerSwung(int playerIndex)
    {
        if (!isWaitingForSwings)
            return;

        // Find and track the ball that was just hit
        foreach (var b in allBalls)
        {
            if (b.GetOwnerIndex() == playerIndex)
            {
                lastHitBall = b;
                break;
            }
        }

        if (playerIndex == 0)
            player1HasSwung = true;
        else if (playerIndex == 1)
            player2HasSwung = true;

        // Check if both players have swung
        if (player1HasSwung && player2HasSwung)
        {
            StartTracking();
        }
    }

    private void StartTracking()
    {
        isWaitingForSwings = false;

        // Switch to tracking the last hit ball
        if (lastHitBall != null)
        {
            SetBall(lastHitBall.transform);

            // Update current ball index to match
            for (int i = 0; i < allBalls.Length; i++)
            {
                if (allBalls[i] == lastHitBall)
                {
                    currentBallIndex = i;
                    break;
                }
            }
        }

        currentLookAhead = Vector3.zero;
        lastTargetY = followTarget.position.y;
        velX = 0f;
        velY = 0f;
        isBallDescending = false;
        previousBallVelocityY = 0f;
    }

    public void CycleTargetBall()
    {
        allBalls = Object.FindObjectsByType<GolfBallController>(FindObjectsSortMode.None);

        if (allBalls.Length == 0)
            return;

        if (isWaitingForSwings)
            return;

        // Cycle to next ball
        currentBallIndex = (currentBallIndex + 1) % allBalls.Length;

        GolfBallController targetBall = allBalls[currentBallIndex];

        if (targetBall != null)
        {
            SetBall(targetBall.transform);
            lastHitBall = targetBall;

            currentLookAhead = Vector3.zero;
        }
    }

    public void OnCameraMove(Vector2 input)
    {
        manualInput = input;

        if (input.magnitude > 0.1f)
        {
            if (!isManual)
            {
                manualOffset = followTarget.position - ball.position;
                isManual = true;
            }
            lastManualInputTime = Time.time;
        }
    }

    public void ResetToAutomatic()
    {
        isManual = false;
        manualOffset = Vector3.zero;
        manualInput = Vector2.zero;
    }

    public void SetBall(Transform newBall)
    {
        ball = newBall;
        if (ball != null)
        {
            ballRb = ball.GetComponent<Rigidbody2D>();
            lastTargetY = ball.position.y;
        }
    }

    public void SetWaitForBothPlayers(bool wait)
    {
        waitForBothPlayers = wait;
    }

    public bool IsWaitingForSwings()
    {
        return isWaitingForSwings;
    }
}