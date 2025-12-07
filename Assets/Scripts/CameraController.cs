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
    [SerializeField] private float verticalSmoothTime = 0.25f;

    [Header("Horizontal Smoothing")]
    [SerializeField] private float horizontalSmoothTime = 0.25f;

    [Header("Manual Camera Control")]
    [SerializeField] private float manualMoveSpeed = 15f;
    [SerializeField] private float returnToTrackingDelay = 2f;

    [Header("Swing Tracking")]
    [SerializeField] private bool waitForBothPlayers = true;
    [SerializeField] private Vector3 preSwingCameraOffset = new Vector3(4f, 1f, 0f);

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

    // Vertical deadzone tracking
    private float lastTargetY;

    // Swing tracking state
    private bool isWaitingForSwings = false;
    private bool player1HasSwung = false;
    private bool player2HasSwung = false;
    private Vector3 frozenCameraPosition;
    private bool isMultiplayer = false;
    private GolfBallController lastHitBall = null;

    void Start()
    {
        // Create dynamic follow target
        GameObject t = new GameObject("CameraTarget");
        followTarget = t.transform;

        if (virtualCamera != null)
            virtualCamera.Follow = followTarget;

        if (ball != null)
        {
            ballRb = ball.GetComponent<Rigidbody2D>();
            followTarget.position = ball.position;
            lastTargetY = ball.position.y;
        }

        // Detect if multiplayer
        DetectGameMode();
        
        // Prepare for first shot
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
    }

    private void UpdateCamera()
    {

        Vector2 ballVelocity = ballRb ? ballRb.linearVelocity : Vector2.zero;

        // Manual Mode overrides everything
        if (isManual)
        {
            manualOffset += new Vector3(manualInput.x, manualInput.y, 0f) * manualMoveSpeed * Time.deltaTime;
            Vector3 mpos = ball.position + manualOffset;
            mpos.z = 0;
            followTarget.position = mpos;
            return;
        }

        // If waiting for both players to swing, smoothly move to frozen position
        if (isWaitingForSwings && waitForBothPlayers && isMultiplayer)
        {
            // Smooth transition to frozen position
            float smoothX = Mathf.SmoothDamp(followTarget.position.x, frozenCameraPosition.x, ref velX, horizontalSmoothTime);
            float smoothY = Mathf.SmoothDamp(followTarget.position.y, frozenCameraPosition.y, ref velY, verticalSmoothTime);
            followTarget.position = new Vector3(smoothX, smoothY, 0);
            return;
        }

        // === VELOCITY LOOKAHEAD ===
        Vector2 targetLookAhead = Vector2.zero;

        if (ballVelocity.magnitude > 0.1f)
        {
            targetLookAhead.x = ballVelocity.x != 0 ? Mathf.Sign(ballVelocity.x) * lookAheadDistance : 0f;
            targetLookAhead.y = ballVelocity.y != 0 ? Mathf.Sign(ballVelocity.y) * lookAheadDistance : 0f;
        }

        // Smooth lookahead
        currentLookAhead.x = Mathf.Lerp(currentLookAhead.x, targetLookAhead.x, Time.deltaTime * lookAheadSmoothing);
        currentLookAhead.y = Mathf.Lerp(currentLookAhead.y, targetLookAhead.y, Time.deltaTime * lookAheadSmoothing);

        Vector3 target = ball.position + currentLookAhead;
        target.z = 0;

        // === HORIZONTAL SMOOTHING ===
        float smoothX2 = Mathf.SmoothDamp(followTarget.position.x, target.x, ref velX, horizontalSmoothTime);

        // === VERTICAL DEADZONE + SMOOTHING WITH LOOKAHEAD ===
        float dy = target.y - lastTargetY;

        if (Mathf.Abs(dy) > verticalDeadzone)
        {
            lastTargetY = Mathf.SmoothDamp(lastTargetY, target.y, ref velY, verticalSmoothTime);
        }

        float smoothY2 = lastTargetY;

        followTarget.position = new Vector3(smoothX2, smoothY2, 0);
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

        // Position camera to show both players
        // Find a good position between both balls
        GolfBallController[] balls = Object.FindObjectsByType<GolfBallController>(FindObjectsSortMode.None);
        if (balls.Length >= 2)
        {
            Vector3 midpoint = (balls[0].transform.position + balls[1].transform.position) / 2f;
            frozenCameraPosition = midpoint + preSwingCameraOffset;
        }
        else if (ball != null)
        {
            frozenCameraPosition = ball.position + preSwingCameraOffset;
        }
        
        frozenCameraPosition.z = 0;

        // Reset velocities for smooth transition
        velX = 0f;
        velY = 0f;
    }

    // Called by InputController when a player swings
    public void OnPlayerSwung(int playerIndex)
    {
        if (!isWaitingForSwings)
            return;

        // Find and track the ball that was just hit
        GolfBallController[] balls = Object.FindObjectsByType<GolfBallController>(FindObjectsSortMode.None);
        foreach (var b in balls)
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
        }

        // Reset look-ahead for smooth transition
        currentLookAhead = Vector3.zero;

        // Update lastTargetY to current camera position for smooth transition
        lastTargetY = followTarget.position.y;
        
        // Reset velocities for smooth transition
        velX = 0f;
        velY = 0f;
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