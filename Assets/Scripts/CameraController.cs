using UnityEngine;
using Unity.Cinemachine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class CameraController : MonoBehaviour
{
    public enum TrackingMode { ClassicFollow = 0, PredictedTrajectory = 1, AnalyticArc = 2 }

    [Header("Tracking Mode")]
    [SerializeField] private TrackingMode trackingMode = TrackingMode.ClassicFollow;

    [Header("References")]
    [SerializeField] private Transform ball;
    [SerializeField] private CinemachineCamera virtualCamera;
    [SerializeField] private LineRenderer trajectoryLine;

    [Header("Look Ahead")]
    [SerializeField] private float lookAheadDistance = 4f;
    [SerializeField] private float lookAheadSmoothing = 4f;

    [Header("Smoothing")]
    [SerializeField] private float verticalSmoothTime = 1f;
    [SerializeField] private float horizontalSmoothTime = 1f;

    [Header("Asymmetric Vertical Tracking")]
    [SerializeField] private float upwardDeadzone = 8f;
    [SerializeField] private float downwardDeadzone = 3f;
    [SerializeField] private float upwardSmoothTime = 0.5f;
    [SerializeField] private float downwardSmoothTime = 0.5f;
    [SerializeField] private float downwardLookAheadMultiplier = 3f;
    [SerializeField] private float apexVelocityThreshold = 0.5f;

    [Header("Manual Camera Control")]
    [SerializeField] private float manualMoveSpeed = 15f;
    [SerializeField] private float returnToTrackingDelay = 2f;

    [Header("Swing Tracking")]
    [SerializeField] private bool waitForBothPlayers = true;
    [SerializeField] private Vector3 preSwingCameraOffset = new Vector3(4f, 1f, 0f);

    [Header("Mode 0 — Classic Follow")]
    [SerializeField] private float restingVerticalFraction = 0.1f;
    [SerializeField] private float verticalBiasSpeedScale = 0.05f;
    [SerializeField] private float verticalBiasSmoothTime = 0.4f;
    [SerializeField] private Transform targetTransform;

    [Header("Trajectory Prediction")]
    [SerializeField] private int predictionSteps = 1000;
    [SerializeField] private float predictionTimeStep = 0.01f;
    [SerializeField] private float minimumVelocityForPrediction = 1f;
    [SerializeField] private float trajectoryLineDuration = 7f;
    [SerializeField] private bool enableCollisionPrediction = true;
    [SerializeField] private LayerMask collisionLayers;
    [SerializeField] private int maxBounces = 100;

    [Header("Prediction-Based Framing")]
    [SerializeField] private float baseOrthographicSize = 12f;
    [SerializeField] private float minOrthographicSize = 8f;
    [SerializeField] private float maxOrthographicSize = 30f;
    [SerializeField] private float zoomSmoothTime = 1f;
    [SerializeField] private float framingSmoothTime = 0.5f;

    [Header("Line Renderer Settings")]
    [SerializeField] private float lineWidth = 0.1f;
    [SerializeField] private Gradient lineColor;

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

    // Zoom
    private float currentZoomVelocity;
    private float targetOrthographicSize;

    // Trajectory (Modes 1 & 2)
    private List<Vector3> predictedTrajectory = new List<Vector3>();
    private bool isTrajectoryActive = false;
    private float trajectoryStartTime;
    private Bounds trajectoryBounds;
    private Vector3 targetFramingCenter;
    private float framingVelX;
    private float framingVelY;

    // Mode 2 — Analytic arc targets
    private Vector3 analyticArcTarget;
    private float analyticTargetZoom;
    private bool analyticArcActive = false;

    // Mode 0 — vertical bias (90/10 rule)
    private float currentVerticalBias;
    private float verticalBiasVelocity;

    // Vertical tracking state
    private bool isBallDescending = false;

    void Start()
    {
        GameObject t = new GameObject("CameraTarget");
        followTarget = t.transform;

        if (virtualCamera != null)
        {
            virtualCamera.Follow = followTarget;
            targetOrthographicSize = baseOrthographicSize;
            if (virtualCamera.Lens.Orthographic)
                virtualCamera.Lens.OrthographicSize = baseOrthographicSize;
        }

        if (ball != null)
        {
            ballRb = ball.GetComponent<Rigidbody2D>();
            followTarget.position = ball.position;
            lastTargetY = ball.position.y;
        }

        SetupLineRenderer();
        DetectGameMode();
        allBalls = Object.FindObjectsByType<GolfBallController>(FindObjectsSortMode.None);
        PrepareForSwings();
    }

    void SetupLineRenderer()
    {
        if (trajectoryLine == null) return;

        trajectoryLine.enabled = false;
        trajectoryLine.startWidth = lineWidth;
        trajectoryLine.endWidth = lineWidth;

        if (lineColor == null || lineColor.colorKeys.Length == 0)
        {
            lineColor = new Gradient();
            lineColor.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.3f, 1f)
                }
            );
        }
        trajectoryLine.colorGradient = lineColor;
    }

    void DetectGameMode()
    {
        InputController[] controllers = Object.FindObjectsByType<InputController>(FindObjectsSortMode.None);
        isMultiplayer = controllers.Length >= 2;
    }

    void LateUpdate()
    {
        if (ball == null || followTarget == null) return;

        if (isManual && Time.time - lastManualInputTime > returnToTrackingDelay)
        {
            isManual = false;
            manualOffset = Vector3.zero;
        }

        UpdateCamera();
        UpdateZoom();
        UpdateTrajectoryLine();
    }

    private void UpdateCamera()
    {
        Vector2 ballVelocity = ballRb ? ballRb.linearVelocity : Vector2.zero;

        if (ballRb != null)
        {
            if (ballVelocity.y < -apexVelocityThreshold) isBallDescending = true;
            else if (ballVelocity.y > apexVelocityThreshold) isBallDescending = false;
        }

        // Manual mode always overrides
        if (isManual)
        {
            manualOffset += new Vector3(manualInput.x, manualInput.y, 0f) * manualMoveSpeed * Time.deltaTime;
            Vector3 mpos = ball.position + manualOffset;
            mpos.z = 0;
            followTarget.position = mpos;
            return;
        }

        // Pre-swing freeze (multiplayer)
        if (isWaitingForSwings && waitForBothPlayers && isMultiplayer)
        {
            followTarget.position = new Vector3(
                Mathf.SmoothDamp(followTarget.position.x, frozenCameraPosition.x, ref velX, horizontalSmoothTime),
                Mathf.SmoothDamp(followTarget.position.y, frozenCameraPosition.y, ref velY, verticalSmoothTime),
                0
            );
            return;
        }

        switch (trackingMode)
        {
            case TrackingMode.ClassicFollow: UpdateClassicFollow(ballVelocity); break;
            case TrackingMode.PredictedTrajectory: UpdatePredictedTrajectory(ballVelocity); break;
            case TrackingMode.AnalyticArc: UpdateAnalyticArc(ballVelocity); break;
        }
    }

    private void UpdateClassicFollow(Vector2 ballVelocity)
    {
        float speed = ballVelocity.magnitude;

        float orthoSize = virtualCamera != null ? virtualCamera.Lens.OrthographicSize : baseOrthographicSize;
        float screenHeight = 2f * orthoSize;

        float restingBias = screenHeight * (0.5f - restingVerticalFraction);

        float speedT = Mathf.Clamp01(speed * verticalBiasSpeedScale);
        float targetBias = Mathf.Lerp(restingBias, 0f, speedT);

        currentVerticalBias = Mathf.SmoothDamp(
            currentVerticalBias, targetBias,
            ref verticalBiasVelocity, verticalBiasSmoothTime
        );

        Vector2 targetLookAhead = Vector2.zero;

        if (speed > 0.1f)
        {
            targetLookAhead.x = ballVelocity.x != 0
                ? Mathf.Sign(ballVelocity.x) * lookAheadDistance : 0f;

            if (ballVelocity.y != 0)
            {
                float yLookAhead = Mathf.Sign(ballVelocity.y) * lookAheadDistance;
                if (isBallDescending && ballVelocity.y < 0)
                    yLookAhead *= downwardLookAheadMultiplier;
                targetLookAhead.y = yLookAhead;
            }
        }

        currentLookAhead.x = Mathf.Lerp(currentLookAhead.x, targetLookAhead.x, Time.deltaTime * lookAheadSmoothing);
        currentLookAhead.y = Mathf.Lerp(currentLookAhead.y, targetLookAhead.y, Time.deltaTime * lookAheadSmoothing);

        Vector3 frameOrigin = ball.position;
        if (speed < 1f && targetTransform != null)
        {
            frameOrigin = (ball.position + targetTransform.position) / 2f;
        }

        Vector3 target = frameOrigin + currentLookAhead;
        target.y += currentVerticalBias;
        target.z = 0;

        // Horizontal: straightforward smooth damp
        float smoothX = Mathf.SmoothDamp(followTarget.position.x, target.x, ref velX, horizontalSmoothTime);

        // Vertical: asymmetric deadzone
        float dy = target.y - lastTargetY;
        bool movingUp = dy > 0;
        float deadzone = movingUp ? upwardDeadzone : downwardDeadzone;
        float smoothTimeV = movingUp ? upwardSmoothTime : downwardSmoothTime;

        if (Mathf.Abs(dy) > deadzone)
            lastTargetY = Mathf.SmoothDamp(lastTargetY, target.y, ref velY, smoothTimeV);

        followTarget.position = new Vector3(smoothX, lastTargetY, 0);
    }

    private void UpdatePredictedTrajectory(Vector2 ballVelocity)
    {
        if (!isTrajectoryActive)
        {
            UpdateClassicFollow(ballVelocity);
            return;
        }

        Vector3 ballPos = ball.position;
        Vector3 targetCenter = ballPos;

        if (predictedTrajectory.Count > 1)
        {
            float closestDist = float.MaxValue;
            int closestIndex = 0;

            for (int i = 0; i < predictedTrajectory.Count; i++)
            {
                float dist = Vector3.Distance(ballPos, predictedTrajectory[i]);
                if (dist < closestDist) { closestDist = dist; closestIndex = i; }
            }

            int lookAheadSteps = Mathf.Min(15, predictedTrajectory.Count - closestIndex - 1);
            if (lookAheadSteps > 0 && closestIndex + lookAheadSteps < predictedTrajectory.Count)
            {
                Vector3 futurePoint = predictedTrajectory[closestIndex + lookAheadSteps];
                targetCenter = (ballPos + futurePoint) / 2f;
            }
        }

        targetCenter.z = 0;

        followTarget.position = new Vector3(
            Mathf.SmoothDamp(followTarget.position.x, targetCenter.x, ref framingVelX, framingSmoothTime),
            Mathf.SmoothDamp(followTarget.position.y, targetCenter.y, ref framingVelY, framingSmoothTime),
            0
        );
    }

    private void UpdateAnalyticArc(Vector2 ballVelocity)
    {
        if (!analyticArcActive)
        {
            UpdateClassicFollow(ballVelocity);
            return;
        }

        followTarget.position = new Vector3(
            Mathf.SmoothDamp(followTarget.position.x, analyticArcTarget.x, ref framingVelX, framingSmoothTime),
            Mathf.SmoothDamp(followTarget.position.y, analyticArcTarget.y, ref framingVelY, framingSmoothTime),
            0
        );
    }

    private void UpdateZoom()
    {
        if (virtualCamera == null || !virtualCamera.Lens.Orthographic) return;

        switch (trackingMode)
        {
            case TrackingMode.ClassicFollow:
                {
                    float speed = ballRb != null ? ballRb.linearVelocity.magnitude : 0f;
                    float speedFactor = Mathf.Clamp01(speed / 20f);
                    targetOrthographicSize = Mathf.Lerp(baseOrthographicSize, maxOrthographicSize, speedFactor);
                    break;
                }

            case TrackingMode.PredictedTrajectory:
                {
                    if (!isTrajectoryActive)
                    {
                        targetOrthographicSize = baseOrthographicSize;
                    }
                    else
                    {
                        float speed = ballRb != null ? ballRb.linearVelocity.magnitude : 0f;
                        float speedFactor = Mathf.Clamp01(speed / 20f);
                        targetOrthographicSize = Mathf.Lerp(minOrthographicSize, maxOrthographicSize, speedFactor);
                    }
                    break;
                }

            case TrackingMode.AnalyticArc:
                {
                    if (!analyticArcActive)
                        targetOrthographicSize = baseOrthographicSize;
                    break;
                }
        }

        virtualCamera.Lens.OrthographicSize = Mathf.SmoothDamp(
            virtualCamera.Lens.OrthographicSize,
            targetOrthographicSize,
            ref currentZoomVelocity,
            zoomSmoothTime
        );
    }

    private void UpdateTrajectoryLine()
    {
        bool shouldShowLine = trackingMode == TrackingMode.PredictedTrajectory
                           || trackingMode == TrackingMode.AnalyticArc;

        if (!shouldShowLine)
        {
            if (trajectoryLine != null) trajectoryLine.enabled = false;
            return;
        }

        if (isTrajectoryActive && Time.time - trajectoryStartTime > trajectoryLineDuration)
        {
            isTrajectoryActive = false;
            analyticArcActive = false;
            if (trajectoryLine != null) trajectoryLine.enabled = false;
        }

        if (isTrajectoryActive && trajectoryLine != null && predictedTrajectory.Count > 0)
        {
            trajectoryLine.positionCount = predictedTrajectory.Count;
            trajectoryLine.SetPositions(predictedTrajectory.ToArray());
        }
    }

    private List<Vector3> PredictTrajectory(Vector2 startPosition, Vector2 startVelocity, LayerMask collisionMask)
    {
        List<Vector3> trajectory = new List<Vector3>();

        Vector2 position = startPosition;
        Vector2 velocity = startVelocity;
        float gravityScale = ballRb != null ? ballRb.gravityScale : 1f;
        float linearDrag = ballRb != null ? ballRb.linearDamping : 0f;

        float ballRadius = 0.5f;
        CircleCollider2D col = ballRb != null ? ballRb.GetComponent<CircleCollider2D>() : null;
        if (col != null)
            ballRadius = col.radius * Mathf.Max(ball.transform.lossyScale.x, ball.transform.lossyScale.y);

        Vector2 gravity = Physics2D.gravity * gravityScale;
        float dt = Time.fixedDeltaTime;
        int steps = Mathf.RoundToInt(predictionSteps * predictionTimeStep / dt);

        trajectory.Add(new Vector3(position.x, position.y, 0));

        int bounceCount = 0;
        float distanceSinceLastCollision = 0f;
        float minDistanceBetweenCollisions = ballRadius * 3f;

        for (int i = 0; i < steps; i++)
        {
            velocity /= 1f + linearDrag * dt;
            velocity += gravity * dt;

            Vector2 nextPosition = position + velocity * dt;
            float stepDistance = Vector2.Distance(position, nextPosition);
            distanceSinceLastCollision += stepDistance;

            RaycastHit2D hit = default;

            if (enableCollisionPrediction && i > 2 &&
                stepDistance > 0.001f &&
                velocity.magnitude > 0.001f &&
                collisionMask.value != 0 &&
                distanceSinceLastCollision > minDistanceBetweenCollisions &&
                bounceCount < maxBounces)
            {
                hit = Physics2D.CircleCast(position, ballRadius, velocity.normalized, stepDistance, collisionMask);
            }

            if (hit.collider != null && hit.collider.gameObject != ball.gameObject)
            {
                bounceCount++;
                distanceSinceLastCollision = 0f;
                position = hit.point + hit.normal * (ballRadius * 1.1f);

                float bounciness = 0f;
                float friction = 0f;

                PhysicsMaterial2D ballMat = ballRb != null ? ballRb.sharedMaterial : null;
                PhysicsMaterial2D surfaceMat = hit.collider.sharedMaterial;

                if (ballMat != null && surfaceMat != null)
                {
                    bounciness = Mathf.Max(ballMat.bounciness, surfaceMat.bounciness);
                    friction = (ballMat.friction + surfaceMat.friction) * 0.5f;
                }
                else if (ballMat != null) { bounciness = ballMat.bounciness; friction = ballMat.friction; }
                else if (surfaceMat != null) { bounciness = surfaceMat.bounciness; friction = surfaceMat.friction; }

                float normalSpeed = Vector2.Dot(velocity, hit.normal);
                Vector2 tangential = velocity - hit.normal * normalSpeed;

                if (normalSpeed < 0)
                {
                    velocity = hit.normal * (-normalSpeed * bounciness)
                             + tangential * Mathf.Clamp01(1f - friction);
                }

                trajectory.Add(new Vector3(position.x, position.y, 0));
                if (velocity.magnitude < 1f || bounceCount >= maxBounces) break;
            }
            else
            {
                position = nextPosition;
                trajectory.Add(new Vector3(position.x, position.y, 0));
            }
        }

        return trajectory;
    }

    private void ComputeAnalyticArc(Vector2 launchPosition, Vector2 launchVelocity)
    {
        float gravityScale = ballRb != null ? ballRb.gravityScale : 1f;
        float g = Mathf.Abs(Physics2D.gravity.y) * gravityScale;

        if (g < 0.001f) return;

        float timeToApex = Mathf.Max(0f, launchVelocity.y / g);

        float apexX = launchPosition.x + launchVelocity.x * timeToApex;
        float apexY = launchPosition.y
                    + launchVelocity.y * timeToApex
                    - 0.5f * g * timeToApex * timeToApex;

        float timeToLand = launchVelocity.y > 0f ? (2f * launchVelocity.y) / g : 0f;
        float landX = launchPosition.x + launchVelocity.x * timeToLand;
        float landY = launchPosition.y;

        Vector2 apex = new Vector2(apexX, apexY);
        Vector2 landing = new Vector2(landX, landY);

        analyticArcTarget = (
            new Vector3(launchPosition.x, launchPosition.y) +
            new Vector3(apex.x, apex.y) +
            new Vector3(landing.x, landing.y)
        ) / 3f;
        analyticArcTarget.z = 0;

        predictedTrajectory.Clear();
        if (trajectoryLine != null)
        {
            float drag = ballRb != null ? ballRb.linearDamping : 0f;
            float dt = Time.fixedDeltaTime;
            int steps = Mathf.RoundToInt(predictionSteps * predictionTimeStep / dt);

            Vector2 pos = launchPosition;
            Vector2 vel = launchVelocity;
            Vector2 grav = Physics2D.gravity * gravityScale;

            predictedTrajectory.Add(new Vector3(pos.x, pos.y, 0));
            for (int i = 0; i < steps; i++)
            {
                vel /= 1f + drag * dt;
                vel += grav * dt;
                pos += vel * dt;
                predictedTrajectory.Add(new Vector3(pos.x, pos.y, 0));
                if (vel.magnitude < 0.5f) break;
            }
        }

        float arcSpan = Vector2.Distance(launchPosition, landing);
        float arcHeight = Mathf.Max(0f, apexY - launchPosition.y);
        float requiredSize = Mathf.Max(arcSpan * 0.5f, arcHeight * 1.5f);

        analyticTargetZoom = Mathf.Clamp(requiredSize, minOrthographicSize, maxOrthographicSize);
        targetOrthographicSize = analyticTargetZoom;

        analyticArcActive = true;
    }

    private Bounds CalculateTrajectoryBounds(List<Vector3> trajectory)
    {
        if (trajectory.Count == 0) return new Bounds(ball.position, Vector3.one * 10f);
        if (trajectory.Count == 1) return new Bounds(trajectory[0], Vector3.one * 10f);

        Vector3 min = trajectory[0], max = trajectory[0];
        foreach (Vector3 p in trajectory) { min = Vector3.Min(min, p); max = Vector3.Max(max, p); }

        Vector3 size = max - min;
        size.x = Mathf.Max(size.x, 10f);
        size.y = Mathf.Max(size.y, 8f);
        return new Bounds((min + max) / 2f, size);
    }

    public void OnBallHit(Vector2 launchPosition, Vector2 launchVelocity)
    {
        if (ballRb == null) return;
        if (launchVelocity.magnitude < minimumVelocityForPrediction) return;

        if (isWaitingForSwings) return;

        // Always hide line first
        if (trajectoryLine != null) trajectoryLine.enabled = false;
        isTrajectoryActive = false;
        analyticArcActive = false;

        switch (trackingMode)
        {
            case TrackingMode.ClassicFollow:
                break;

            case TrackingMode.PredictedTrajectory:
                {
                    LayerMask mask = collisionLayers;
                    if (ball != null) mask &= ~(1 << ball.gameObject.layer);

                    predictedTrajectory = PredictTrajectory(launchPosition, launchVelocity, mask);

                    if (predictedTrajectory.Count > 1)
                    {
                        isTrajectoryActive = true;
                        trajectoryStartTime = Time.time;

                        trajectoryBounds = CalculateTrajectoryBounds(predictedTrajectory);
                        targetFramingCenter = trajectoryBounds.center;
                        targetFramingCenter.z = 0;

                        if (trajectoryLine != null)
                        {
                            trajectoryLine.enabled = true;
                            trajectoryLine.positionCount = predictedTrajectory.Count;
                            trajectoryLine.SetPositions(predictedTrajectory.ToArray());
                        }
                    }
                    break;
                }

            case TrackingMode.AnalyticArc:
                {
                    ComputeAnalyticArc(launchPosition, launchVelocity);

                    if (analyticArcActive)
                    {
                        isTrajectoryActive = true;
                        trajectoryStartTime = Time.time;

                        if (trajectoryLine != null && predictedTrajectory.Count > 1)
                        {
                            trajectoryLine.enabled = true;
                            trajectoryLine.positionCount = predictedTrajectory.Count;
                            trajectoryLine.SetPositions(predictedTrajectory.ToArray());
                        }
                    }
                    break;
                }
        }
    }

    public void PrepareForSwings()
    {
        if (!waitForBothPlayers || !isMultiplayer) return;

        isWaitingForSwings = true;
        player1HasSwung = false;
        player2HasSwung = false;
        lastHitBall = null;

        allBalls = Object.FindObjectsByType<GolfBallController>(FindObjectsSortMode.None);

        frozenCameraPosition = ball.position + preSwingCameraOffset;
        frozenCameraPosition.z = 0;

        velX = velY = 0f;
        isBallDescending = false;

        isTrajectoryActive = false;
        analyticArcActive = false;
        if (trajectoryLine != null) trajectoryLine.enabled = false;
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
            StartTracking();
    }

    private void StartTracking()
    {
        isWaitingForSwings = false;

        if (lastHitBall != null)
        {
            SetBall(lastHitBall.transform);
            for (int i = 0; i < allBalls.Length; i++)
            {
                if (allBalls[i] == lastHitBall) { currentBallIndex = i; break; }
            }
        }

        currentLookAhead = Vector3.zero;
        lastTargetY = followTarget.position.y;
        velX = velY = 0f;
        isBallDescending = false;
    }

    public void CycleTargetBall()
    {
        allBalls = Object.FindObjectsByType<GolfBallController>(FindObjectsSortMode.None);
        if (allBalls.Length == 0 || isWaitingForSwings) return;

        currentBallIndex = (currentBallIndex + 1) % allBalls.Length;
        GolfBallController target = allBalls[currentBallIndex];

        if (target != null)
        {
            SetBall(target.transform);
            lastHitBall = target;
            currentLookAhead = Vector3.zero;
        }
    }

    public void OnCameraMove(Vector2 input)
    {
        manualInput = input;
        if (input.magnitude > 0.1f)
        {
            if (!isManual) { manualOffset = followTarget.position - ball.position; isManual = true; }
            lastManualInputTime = Time.time;
        }
    }

    public void ResetToAutomatic()
    {
        isManual = false; manualOffset = Vector3.zero; manualInput = Vector2.zero;
    }

    public void SetBall(Transform newBall)
    {
        ball = newBall;
        if (ball != null) { ballRb = ball.GetComponent<Rigidbody2D>(); lastTargetY = ball.position.y; }
    }
    public void SetTrackingMode(TrackingMode mode) => trackingMode = mode;
    public TrackingMode GetTrackingMode() => trackingMode;
    public void SetWaitForBothPlayers(bool wait) => waitForBothPlayers = wait;
    public bool IsWaitingForSwings() => isWaitingForSwings;
}