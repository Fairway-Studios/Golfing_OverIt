using UnityEngine;
using Unity.Cinemachine;

public class CameraController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform ball;
    [SerializeField] private Camera mainCamera;

    [Header("Smoothing")]
    [SerializeField] private float verticalSmoothTime = 1f;
    [SerializeField] private float horizontalSmoothTime = 1f;

    [Header("Manual Camera Control")]
    [SerializeField] private float manualMoveSpeed = 20f;
    [SerializeField] private float returnToTrackingDelay = 2f;

    [Header("Swing Tracking")]
    [SerializeField] private bool waitForBothPlayers = true;

    [Header("Dynamic Zoom")]
    [SerializeField] private float baseOrthographicSize = 12f;
    [SerializeField] private float maxOrthographicSize = 25f;
    [SerializeField] private float speedThreshold = 8f;
    [SerializeField] private float maxSpeed = 20f;
    [SerializeField] private float zoomSmoothTime = 1f;
    [SerializeField] private float zoomSuppressDuration = 3f;

    [Header("Directional Bias Cameras")]
    [SerializeField] private CinemachineCamera vcamRight;
    [SerializeField] private CinemachineCamera vcamLeft;

    [Header("Dynamic Framing — Offsets")]
    [SerializeField] private Vector2 groundedOffset = new Vector2(-0.3f, 0.2f);
    [SerializeField] private Vector2 flightOffset = new Vector2(-0.3f, 0.0f);
    [SerializeField] private float flightVelocityThreshold = 10f;

    [SerializeField] private float maxFallFramingOffset = 0.25f;
    [SerializeField] private float maxFallSpeed = 20f;
    [SerializeField] private float fallOffsetSmoothTime = 1f;

    [SerializeField] private bool startFacingRight = true;

    [Header("Dynamic Framing — Blend")]
    [SerializeField] private float blendDuration = 2f;
    [SerializeField] private AnimationCurve blendCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private float landingBlendDuration = 0.5f;

    private Transform followTarget;
    private Rigidbody2D ballRb;
    private float velX;
    private float velY;

    // Manual camera
    private bool isManual;
    private Vector2 manualInput;
    private float lastManualInputTime;
    private Vector3 manualOffset;

    // Swing tracking
    private bool isWaitingForSwings;
    private bool player1HasSwung;
    private bool player2HasSwung;
    private Vector3 frozenCameraPosition;
    private bool isMultiplayer;
    private GolfBallController lastHitBall;

    // Ball cycling
    private GolfBallController[] allBalls;
    private int currentBallIndex;

    // Dynamic zoom
    private float currentZoomVelocity;
    private float targetOrthographicSize;
    private Vector3 lastCameraPosition;
    private bool suppressZoom;
    private float suppressZoomUntil;

    // Bias
    private bool isFacingRight;
    private bool isGravityNormal = true;
    private bool isBallInFlight;

    private CinemachineCamera activeCam;
    private CinemachinePositionComposer composer;

    public bool IsManualMoving() => isManual;

    private enum BlendTrigger { Standard, Landing }

    private struct BlendState
    {
        public bool active;
        public Vector2 from;
        public Vector2 to;
        public float elapsed;
        public float duration;
    }

    private BlendState blend;
    private Vector2 currentFramingOffset;

    private float currentFallOffset;
    private float fallOffsetVelocity;

    private float GravityDir() => isGravityNormal ? -1f : 1f;

    void Start()
    {
        GameObject t = new GameObject("CameraTarget");
        followTarget = t.transform;

        targetOrthographicSize = baseOrthographicSize;
        isFacingRight = startFacingRight;

        if (ball != null)
        {
            ballRb = ball.GetComponent<Rigidbody2D>();
            followTarget.position = ball.position;
        }

        lastCameraPosition = mainCamera != null
            ? mainCamera.transform.position
            : followTarget.position;

        InitialiseCameras();
        DetectGameMode();

        allBalls = Object.FindObjectsByType<GolfBallController>(FindObjectsSortMode.None);

        PrepareForSwings();
    }

    void LateUpdate()
    {
        if (ball == null || followTarget == null) return;

        if (isManual && Time.time - lastManualInputTime > returnToTrackingDelay)
        {
            isManual = false;
            manualOffset = Vector3.zero;
        }

        if (suppressZoom && Time.time >= suppressZoomUntil)
            suppressZoom = false;

        UpdateFlightState();
        UpdateCamera();
        UpdateFramingBlend();
        UpdateDynamicZoom();
    }

    private void InitialiseCameras()
    {
        if (vcamRight != null) vcamRight.Follow = followTarget;
        if (vcamLeft != null) vcamLeft.Follow = followTarget;

        currentFramingOffset = GetBaseTargetOffset();

        ApplyActiveCamera();
        RefreshComposer();

        if (composer != null)
            composer.Composition.ScreenPosition = currentFramingOffset;
    }

    private void ApplyActiveCamera()
    {
        CinemachineCamera next = isFacingRight ? vcamRight : vcamLeft;
        if (next == null) return;

        if (vcamRight != null) vcamRight.Priority = 0;
        if (vcamLeft != null) vcamLeft.Priority = 0;

        next.Priority = 10;
        activeCam = next;
    }

    private void RefreshComposer()
    {
        composer = activeCam != null
            ? activeCam.GetComponentInChildren<CinemachinePositionComposer>()
            : null;
    }

    private void UpdateFlightState()
    {
        if (ballRb == null) return;

        bool wasInFlight = isBallInFlight;
        isBallInFlight = Mathf.Abs(ballRb.linearVelocity.y) > flightVelocityThreshold;

        if (isBallInFlight && !wasInFlight)
            StartBlend(BlendTrigger.Standard);
        else if (!isBallInFlight && wasInFlight)
            StartBlend(BlendTrigger.Landing);
    }

    private Vector2 GetBaseTargetOffset()
    {
        float g = GravityDir();

        Vector2 target = isBallInFlight ? flightOffset : groundedOffset;

        if (!isBallInFlight)
            target.y *= -g;

        if (!isFacingRight)
            target.x = -target.x;

        return target;
    }

    private void StartBlend(BlendTrigger trigger)
    {
        Vector2 targetOffset = GetBaseTargetOffset();

        if (Mathf.Sign(currentFramingOffset.x) != Mathf.Sign(targetOffset.x))
        {
            currentFramingOffset.x = targetOffset.x;
        }

        blend = new BlendState
        {
            active = true,
            from = currentFramingOffset,
            to = targetOffset,
            elapsed = 0f,
            duration = trigger == BlendTrigger.Landing ? landingBlendDuration : blendDuration,
        };
    }

    private void UpdateFramingBlend()
    {
        if (composer == null) return;
        if (isWaitingForSwings && waitForBothPlayers && isMultiplayer) return;

        if (blend.active)
        {
            blend.elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(blend.elapsed / blend.duration);
            currentFramingOffset = Vector2.Lerp(blend.from, blend.to, blendCurve.Evaluate(t));

            if (t >= 1f)
            {
                currentFramingOffset = blend.to;
                blend.active = false;
            }
        }

        float targetFallOffset = 0f;

        if (ballRb != null)
        {
            float fallSpeed = GravityDir() * ballRb.linearVelocity.y;

            if (fallSpeed > 0f)
                targetFallOffset = Mathf.Clamp01(fallSpeed / maxFallSpeed) * maxFallFramingOffset;
        }

        currentFallOffset = Mathf.SmoothDamp(
            currentFallOffset,
            targetFallOffset,
            ref fallOffsetVelocity,
            fallOffsetSmoothTime);

        Vector2 finalOffset = currentFramingOffset;
        finalOffset.y += GravityDir() * currentFallOffset;

        composer.Composition.ScreenPosition = finalOffset;
    }

    private void UpdateCamera()
    {
        if (isManual)
        {
            manualOffset += new Vector3(manualInput.x, manualInput.y, 0f)
                            * manualMoveSpeed * Time.deltaTime;

            Vector3 mpos = ball.position + manualOffset;
            mpos.z = 0f;
            followTarget.position = mpos;
            return;
        }

        if (isWaitingForSwings && waitForBothPlayers && isMultiplayer)
        {
            float sx = Mathf.SmoothDamp(followTarget.position.x,
                frozenCameraPosition.x, ref velX, horizontalSmoothTime);

            float sy = Mathf.SmoothDamp(followTarget.position.y,
                frozenCameraPosition.y, ref velY, verticalSmoothTime);

            followTarget.position = new Vector3(sx, sy, 0f);
            return;
        }

        Vector3 target = ball.position;
        target.z = 0f;

        float smoothX = Mathf.SmoothDamp(followTarget.position.x, target.x, ref velX, horizontalSmoothTime);
        float smoothY = Mathf.SmoothDamp(followTarget.position.y, target.y, ref velY, verticalSmoothTime);

        followTarget.position = new Vector3(smoothX, smoothY, 0f);
    }

    private void UpdateDynamicZoom()
    {
        if (activeCam == null || !activeCam.Lens.Orthographic) return;

        if ((isWaitingForSwings && waitForBothPlayers && isMultiplayer) || suppressZoom)
        {
            lastCameraPosition = mainCamera != null
                ? mainCamera.transform.position
                : followTarget.position;

            float resetSize = Mathf.SmoothDamp(
                activeCam.Lens.OrthographicSize,
                baseOrthographicSize,
                ref currentZoomVelocity,
                zoomSmoothTime);

            if (vcamRight != null) vcamRight.Lens.OrthographicSize = resetSize;
            if (vcamLeft != null) vcamLeft.Lens.OrthographicSize = resetSize;
            return;
        }

        Vector3 currentCamPos = mainCamera != null
            ? mainCamera.transform.position
            : followTarget.position;

        float cameraSpeed = (currentCamPos - lastCameraPosition).magnitude / Time.deltaTime;
        lastCameraPosition = currentCamPos;

        targetOrthographicSize = cameraSpeed < speedThreshold
            ? baseOrthographicSize
            : Mathf.Lerp(baseOrthographicSize, maxOrthographicSize,
                Mathf.Clamp01((cameraSpeed - speedThreshold) / (maxSpeed - speedThreshold)));

        float newSize = Mathf.SmoothDamp(
            activeCam.Lens.OrthographicSize,
            targetOrthographicSize,
            ref currentZoomVelocity,
            zoomSmoothTime);

        if (vcamRight != null) vcamRight.Lens.OrthographicSize = newSize;
        if (vcamLeft != null) vcamLeft.Lens.OrthographicSize = newSize;
    }

    public void PrepareForSwings()
    {
        if (!waitForBothPlayers || !isMultiplayer) return;

        isWaitingForSwings = true;
        player1HasSwung = false;
        player2HasSwung = false;
        lastHitBall = null;

        allBalls = Object.FindObjectsByType<GolfBallController>(FindObjectsSortMode.None);

        frozenCameraPosition = ball.position;
        frozenCameraPosition.z = 0f;

        velX = 0f;
        velY = 0f;
    }

    public void OnPlayerSwung(int playerIndex)
    {
        if (!isWaitingForSwings) return;

        foreach (var b in allBalls)
        {
            if (b.GetOwnerIndex() == playerIndex) { lastHitBall = b; break; }
        }

        if (playerIndex == 0) player1HasSwung = true;
        else if (playerIndex == 1) player2HasSwung = true;

        if (player1HasSwung && player2HasSwung)
        {
            isWaitingForSwings = false;
            SetBall(lastHitBall.transform);
        }
    }

    public void CycleTargetBall()
    {
        allBalls = Object.FindObjectsByType<GolfBallController>(FindObjectsSortMode.None);
        if (allBalls.Length == 0 || isWaitingForSwings) return;

        currentBallIndex = (currentBallIndex + 1) % allBalls.Length;
        SetBall(allBalls[currentBallIndex].transform);
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

    public void SwapHorizontalBias()
    {
        SetFacingDirection(!isFacingRight);
    }

    public void SetFacingDirection(bool faceRight)
    {
        if (isFacingRight == faceRight) return;

        isFacingRight = faceRight;

        ApplyActiveCamera();
        RefreshComposer();

        currentFramingOffset = GetBaseTargetOffset();

        if (composer != null)
            composer.Composition.ScreenPosition = currentFramingOffset;

        StartBlend(BlendTrigger.Standard);

        SuppressZoomFor(zoomSuppressDuration);
    }

    public void SwapVerticalBias()
    {
        isGravityNormal = !isGravityNormal;
        StartBlend(BlendTrigger.Standard);
    }

    public void SetBall(Transform newBall)
    {
        ball = newBall;
        if (ball == null) return;
        ballRb = ball.GetComponent<Rigidbody2D>();
    }

    private void SuppressZoomFor(float seconds)
    {
        suppressZoom = true;
        suppressZoomUntil = Time.time + seconds;
        currentZoomVelocity = 0f;
        targetOrthographicSize = baseOrthographicSize;
    }

    private void DetectGameMode()
    {
        InputController[] controllers =
            Object.FindObjectsByType<InputController>(FindObjectsSortMode.None);
        isMultiplayer = controllers.Length >= 2;
    }
}