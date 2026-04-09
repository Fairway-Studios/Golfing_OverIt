using UnityEngine;
using TMPro;

public class GameManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CameraController cameraController;
    [SerializeField] private Vector3 playerOffsetFromBall = new Vector3(-0.1f, 1.45f, 0f);
    [SerializeField] private ObstaclePlacement2D obstaclePlacement;

    [Header("UI")]
    [SerializeField] private GameObject selectionUI;
    [SerializeField] private TextMeshProUGUI promptText;
    [SerializeField] private TextMeshProUGUI hudText;

    [Header("Settings")]
    [SerializeField] private bool forceMultiplayerMode = false;

    private bool selectionActive = false;
    private bool player1VotedA = false;
    private bool player1VotedB = false;
    private bool player2VotedA = false;
    private bool player2VotedB = false;
    private bool isMultiplayer = false;

    // --- NEW: Track individual swings for Multiplayer Achievements ---
    private bool player1HasSwung = false;
    private bool player2HasSwung = false;
    // --- NEW: Multiplayer Achievement Trackers ---
    private float lastSwingTimeP1 = -1f;
    private float lastSwingTimeP2 = -1f;
    private int lastTeleportOwner = -1;
    private int consecutiveTeleports = 0;
    private bool p1FellThisTurn = false;
    private bool p2FellThisTurn = false;
    private float lastTeleportTime = -1f;

    private int strokeCount = 0;
    private float elapsedTime = 0f;
    private bool timerRunning = false;

    [Header("Ball Indicators")]
    [SerializeField] private GameObject ballIndicatorPrefab;
    [SerializeField] private Transform indicatorParent;

    private BallIndicator[] ballIndicators;
    private Transform[] players;
    private Transform cameraTransform;

    void Start()
    {
        FindPlayers();
        FindCamera();

        if (obstaclePlacement == null)
            obstaclePlacement = Object.FindFirstObjectByType<ObstaclePlacement2D>();

        if (selectionUI != null)
            selectionUI.SetActive(false);

        DetectGameMode();
        SetupBallIndicators();
        CheckForLoadGame();

        timerRunning = true;
    }

    void CheckForLoadGame()
    {
        if (GameSession.IsLoadingGame && players != null && players.Length > 0)
        {
            // Call the cloud load, and run this block when the data arrives
            SaveSystem.LoadPlayer(data =>
            {
                if (data == null) return;

                // 1. RECONSTRUCT THE MAP FIRST
                PerlinMountain2D pcg = Object.FindFirstObjectByType<PerlinMountain2D>();

                if (pcg != null && data.pcgData != null)
                {
                    // Lock the seed and restore ALL settings
                    pcg.useRandomSeed = false;
                    pcg.seed = data.pcgData.seed;

                    pcg.mountainCount = data.pcgData.mountainCount;
                    pcg.mountainWidth = data.pcgData.mountainWidth;
                    pcg.mountainSpacing = data.pcgData.mountainSpacing;
                    pcg.baseY = data.pcgData.baseY;
                    pcg.amplitude = data.pcgData.amplitude;
                    pcg.frequency = data.pcgData.frequency;
                    pcg.stepX = data.pcgData.stepX;
                    pcg.clampToBase = data.pcgData.clampToBase;
                    pcg.baseMargin = data.pcgData.baseMargin;
                    pcg.quantizeHeights = data.pcgData.quantizeHeights;
                    pcg.heightStep = data.pcgData.heightStep;
                    pcg.useStairSteps = data.pcgData.useStairSteps;
                    pcg.groundYOffset = data.pcgData.groundYOffset;
                    pcg.scaleGroundHeight = data.pcgData.scaleGroundHeight;
                    pcg.finishFlagOffset = new Vector3(data.pcgData.finishFlagOffset[0], data.pcgData.finishFlagOffset[1], data.pcgData.finishFlagOffset[2]);
                    pcg.finishEdgeSearchWidth = data.pcgData.finishEdgeSearchWidth;
                    pcg.finishFlagFrontInset = data.pcgData.finishFlagFrontInset;

                    if (pcg.caveGenerator != null)
                    {
                        pcg.caveGenerator.cavesPerMountain = data.pcgData.cavesPerMountain;
                        pcg.caveGenerator.minDepth = data.pcgData.minDepth;
                        pcg.caveGenerator.maxDepth = data.pcgData.maxDepth;
                        pcg.caveGenerator.minLength = data.pcgData.minLength;
                        pcg.caveGenerator.maxLength = data.pcgData.maxLength;
                        pcg.caveGenerator.minSpanX = data.pcgData.minSpanX;
                        pcg.caveGenerator.maxSpanX = data.pcgData.maxSpanX;
                        pcg.caveGenerator.mouthHeight = data.pcgData.mouthHeight;
                        pcg.caveGenerator.interiorDropScale = data.pcgData.interiorDropScale;
                        pcg.caveGenerator.insideSurfaceMargin = data.pcgData.insideSurfaceMargin;
                        pcg.caveGenerator.maxMouthHeightDiff = data.pcgData.maxMouthHeightDiff;
                        pcg.caveGenerator.minExtraVerticalClear = data.pcgData.minExtraVerticalClear;
                        pcg.caveGenerator.minCaveThickness = data.pcgData.minCaveThickness;
                        pcg.caveGenerator.insetToSpanMax = data.pcgData.insetToSpanMax;
                        pcg.caveGenerator.baseClearance = data.pcgData.baseClearance;
                        pcg.caveGenerator.edgePadding = data.pcgData.edgePadding;
                        pcg.caveGenerator.edgeMarginWorld = data.pcgData.edgeMarginWorld;
                        pcg.caveGenerator.maxAttemptsPerCave = data.pcgData.maxAttemptsPerCave;
                        pcg.caveGenerator.roundCave = data.pcgData.roundCave;
                        pcg.caveGenerator.roundIterations = data.pcgData.roundIterations;
                        pcg.caveGenerator.maxSmoothedPoints = data.pcgData.maxSmoothedPoints;
                    }

                    if (pcg.shortcutGenerator != null)
                    {
                        pcg.shortcutGenerator.spawnTunnels = data.pcgData.spawnTunnels;
                        pcg.shortcutGenerator.tunnelsPerMountain = data.pcgData.tunnelsPerMountain;
                        pcg.shortcutGenerator.cutHeightFraction = data.pcgData.cutHeightFraction;
                        pcg.shortcutGenerator.cutHeightRandomness = data.pcgData.cutHeightRandomness;
                        pcg.shortcutGenerator.gapSize = data.pcgData.gapSize;
                        pcg.shortcutGenerator.archDepth = data.pcgData.archDepth;
                        pcg.shortcutGenerator.jaggedAmplitude = data.pcgData.jaggedAmplitude;
                        pcg.shortcutGenerator.jaggedPoints = data.pcgData.jaggedPoints;
                        pcg.shortcutGenerator.angledMaxTilt = data.pcgData.angledMaxTilt;
                        pcg.shortcutGenerator.minPeakHeight = data.pcgData.minPeakHeight;
                        pcg.shortcutGenerator.minTunnelWidth = data.pcgData.minTunnelWidth;
                        pcg.shortcutGenerator.maxTunnelWidth = data.pcgData.maxTunnelWidth;
                        pcg.shortcutGenerator.spawnBreakableWall = data.pcgData.spawnBreakableWall;
                        pcg.shortcutGenerator.wallEntranceOffsetX = data.pcgData.wallEntranceOffsetX;
                        pcg.shortcutGenerator.wallVerticalOffset = data.pcgData.wallVerticalOffset;
                        pcg.shortcutGenerator.overlapNudge = data.pcgData.overlapNudge;
                        pcg.shortcutGenerator.maxNudgeSteps = data.pcgData.maxNudgeSteps;
                    }

                    if (pcg.obstaclePlacer != null)
                    {
                        pcg.obstaclePlacer.spawnObstacles = data.pcgData.spawnObstacles;
                        pcg.obstaclePlacer.obstaclesPerMountain = data.pcgData.obstaclesPerMountain;
                        pcg.obstaclePlacer.edgePaddingX = data.pcgData.obsEdgePaddingX;
                        pcg.obstaclePlacer.yOffset = data.pcgData.obstacleYOffset;
                        pcg.obstaclePlacer.minSpacing = data.pcgData.minSpacing;
                        pcg.obstaclePlacer.maxAttemptsPerObstacle = data.pcgData.maxAttemptsPerObstacle;
                        pcg.obstaclePlacer.alignToSlope = data.pcgData.alignToSlope;
                        pcg.obstaclePlacer.avoidSpawnPointsRadius = data.pcgData.avoidSpawnPointsRadius;
                        pcg.obstaclePlacer.useOverlapCheck = data.pcgData.useOverlapCheck;
                        pcg.obstaclePlacer.overlapCheckRadius = data.pcgData.overlapCheckRadius;
                    }

                    // GENERATE THE EXACT MAP DETERMINISTICALLY
                    pcg.GenerateNow();
                }

                // 2. RESTORE METRICS & POSITIONS
                strokeCount = data.strokes;
                elapsedTime = data.time;

                Vector3 microOffset = new Vector3(0, 0.05f, 0);
                Vector3 savedPlayerPos = new Vector3(data.playerPosition[0], data.playerPosition[1], data.playerPosition[2]) + microOffset;
                Vector3 savedBallPos = new Vector3(data.ballPosition[0], data.ballPosition[1], data.ballPosition[2]) + microOffset;

                InputController[] controllers = Object.FindObjectsByType<InputController>(FindObjectsSortMode.None);
                GolfBallController[] balls = Object.FindObjectsByType<GolfBallController>(FindObjectsSortMode.None);

                if (balls != null && balls.Length > 0 && balls[0] != null)
                {
                    balls[0].transform.position = savedBallPos;
                    Rigidbody2D ballRb = balls[0].GetComponent<Rigidbody2D>();
                    if (ballRb != null)
                    {
                        ballRb.position = savedBallPos;
                        ballRb.linearVelocity = Vector2.zero;
                        ballRb.angularVelocity = 0f;
                        ballRb.Sleep();
                    }
                    balls[0].ResetForNextShot();
                }

                if (players[0] != null)
                {
                    players[0].position = savedPlayerPos;
                    Rigidbody2D playerRb = players[0].GetComponent<Rigidbody2D>();
                    if (playerRb != null)
                    {
                        playerRb.position = savedPlayerPos;
                        playerRb.linearVelocity = Vector2.zero;
                        playerRb.Sleep();
                    }
                }

                foreach (var c in controllers)
                {
                    c.OnPlayerTeleported();
                }

                if (cameraTransform != null)
                {
                    cameraTransform.position = new Vector3(savedBallPos.x, savedBallPos.y, cameraTransform.position.z);
                }

                Debug.Log("Game Loaded from Cloud: Exact positions restored.");
            });
        }
    }

    void FindPlayers()
    {
        InputController[] controllers = Object.FindObjectsByType<InputController>(FindObjectsSortMode.None);

        System.Array.Sort(controllers, (a, b) => a.transform.root.name.CompareTo(b.transform.root.name));

        players = new Transform[controllers.Length];

        for (int i = 0; i < controllers.Length; i++)
        {
            players[i] = controllers[i].transform.root;
        }
    }

    void FindCamera()
    {
        if (Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        if (cameraController == null)
        {
            cameraController = Object.FindFirstObjectByType<CameraController>();
        }
    }

    void DetectGameMode()
    {
        isMultiplayer = (players.Length >= 2) || forceMultiplayerMode;

        // --- NEW: Tell the Achievement Boss what mode we are playing ---
        if (AchievementManager.Instance != null)
        {
            AchievementManager.Instance.isMultiplayerModeActive = isMultiplayer;
        }
    }

    void Update()
    {
        if (timerRunning)
            elapsedTime += Time.deltaTime;

        if (!selectionActive)
        {
            UpdatePrompt();
            CheckIfBallsStopped();
        }
        else
        {
            CheckVotes();
        }

        UpdateHUD();
    }

    void UpdateHUD()
    {
        if (hudText == null) return;

        int minutes = Mathf.FloorToInt(elapsedTime / 60f);
        int seconds = Mathf.FloorToInt(elapsedTime % 60f);
        hudText.text = $"Strokes: {strokeCount}  |  {minutes:00}:{seconds:00}";

        if (elapsedTime >= 1800f && AchievementManager.Instance != null)
        {
            AchievementManager.Instance.UnlockAchievement("TIME_SINK");
        }
    }

    void SetupBallIndicators()
    {
        if (ballIndicatorPrefab == null || indicatorParent == null)
        {
            Debug.LogWarning("Ball indicator prefab or parent not assigned!");
            return;
        }

        GolfBallController[] balls = Object.FindObjectsByType<GolfBallController>(FindObjectsSortMode.None);
        ballIndicators = new BallIndicator[balls.Length];

        for (int i = 0; i < balls.Length; i++)
        {
            GameObject indicatorObj = Instantiate(ballIndicatorPrefab, indicatorParent);
            BallIndicator indicator = indicatorObj.GetComponent<BallIndicator>();

            if (indicator != null)
            {
                string label = balls[i].GetOwnerIndex() == 0 ? "A" : "B";
                indicator.Setup(balls[i].transform, label);
                indicator.Hide();
                ballIndicators[i] = indicator;
            }
        }
    }

    void CheckIfBallsStopped()
    {
        GolfBallController[] balls = Object.FindObjectsByType<GolfBallController>(FindObjectsSortMode.None);
        if (balls.Length == 0) return;

        bool allStopped = true;
        foreach (GolfBallController ball in balls)
        {
            if (!ball.IsStopped())
            {
                allStopped = false;
                break;
            }
        }

        if (allStopped)
        {
            if (isMultiplayer && balls.Length >= 2)
            {
                StartShotSelection();
            }
            else
            {
                AutoTeleportSinglePlayer(balls[0]);
            }
        }
    }

    void StartShotSelection()
    {
        selectionActive = true;

        if (ballIndicators != null)
        {
            foreach (var ind in ballIndicators)
            {
                if (ind != null)
                {
                    ind.Show();
                }
            }
        }

        if (selectionUI != null)
        {
            promptText.text = "Hold to select:\nA (LMB/South) or B (RMB/East)\nBoth players must hold the same.";
            selectionUI.SetActive(true);
        }
    }

    // PLAYER 1 INPUT
    public void OnPlayer1VoteA(bool held)
    {
        if (!selectionActive) return;
        player1VotedA = held;
        if (held) player1VotedB = false;
        UpdatePrompt();
    }

    public void OnPlayer1VoteB(bool held)
    {
        if (!selectionActive) return;
        player1VotedB = held;
        if (held) player1VotedA = false;
        UpdatePrompt();
    }

    // PLAYER 2 INPUT
    public void OnPlayer2VoteA(bool held)
    {
        if (!selectionActive) return;
        player2VotedA = held;
        if (held) player2VotedB = false;
        UpdatePrompt();
    }

    public void OnPlayer2VoteB(bool held)
    {
        if (!selectionActive) return;
        player2VotedB = held;
        if (held) player2VotedA = false;
        UpdatePrompt();
    }

    void CheckVotes()
    {
        if (!selectionActive) return;

        if (player1VotedA && player2VotedA)
        {
            TeleportToBall(0);
        }
        else if (player1VotedB && player2VotedB)
        {
            TeleportToBall(1);
        }
    }

    void UpdatePrompt()
    {
        if (promptText == null) return;

        string p1 = player1VotedA ? "A (holding)" : player1VotedB ? "B (holding)" : "Not holding";
        string p2 = player2VotedA ? "A (holding)" : player2VotedB ? "B (holding)" : "Not holding";

        promptText.text = "BOTH must HOLD the same button:\n" +
                          $"Player 1: {p1}\n" +
                          $"Player 2: {p2}";
    }

    void AutoTeleportSinglePlayer(GolfBallController ball)
    {
        Vector3 ballPosition = ball.GetPosition();

        InputController[] controllers = Object.FindObjectsByType<InputController>(FindObjectsSortMode.None);
        foreach (var controller in controllers)
            controller.OnPlayerTeleported();

        int ownerIndex = ball.GetOwnerIndex();
        if (ownerIndex < players.Length && players[ownerIndex] != null)
            players[ownerIndex].position = ballPosition + playerOffsetFromBall;
        else if (players.Length > 0)
            players[0].position = ballPosition + playerOffsetFromBall;

        ball.ResetForNextShot();

        // Prepare camera for next swing
        if (cameraController != null)
        {
            cameraController.PrepareForSwings();
        }
    }

    void TeleportToBall(int ownerIndex)
    {
        // Hide indicators
        if (ballIndicators != null)
        {
            foreach (var ind in ballIndicators)
            {
                if (ind != null)
                    ind.Hide();
            }
        }

        GolfBallController[] allBalls = Object.FindObjectsByType<GolfBallController>(FindObjectsSortMode.None);
        GolfBallController chosen = null;

        foreach (var ball in allBalls)
        {
            if (ball.GetOwnerIndex() == ownerIndex)
            {
                chosen = ball;
                break;
            }
        }

        if (chosen == null) return;

        Vector3 pos = chosen.GetPosition();

        InputController[] controllers = Object.FindObjectsByType<InputController>(FindObjectsSortMode.None);
        foreach (var c in controllers)
            c.OnPlayerTeleported();

        // First move players to chosen position
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] != null)
                players[i].position = pos + playerOffsetFromBall;
        }

        // Move both balls to chosen position
        foreach (var ball in allBalls)
        {
            ball.DisableTrail();

            Rigidbody2D rb = ball.GetRigidbody();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
                rb.position = pos;
            }

            ball.transform.position = pos;
        }

        Vector3 finalPos = pos;

        if (obstaclePlacement != null)
        {
            finalPos = obstaclePlacement.ResolveSharedSafeBallPosition(pos);
        }

        // Move both balls to the SAME final safe position
        foreach (var ball in allBalls)
        {
            Rigidbody2D rb = ball.GetRigidbody();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
                rb.position = finalPos;
                rb.Sleep();
            }

            ball.transform.position = finalPos;
        }

        // Re-align players to final position
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] != null)
                players[i].position = finalPos + playerOffsetFromBall;
        }

        // Re-align camera to final position
        if (cameraTransform != null)
            cameraTransform.position = new Vector3(finalPos.x, finalPos.y, cameraTransform.position.z);

        foreach (var ball in allBalls)
        {
            ball.ResetForNextShot();
            ball.EnableTrail();
        }

        if (selectionUI != null)
            selectionUI.SetActive(false);

        // --- NEW: Track Teleport streaks for "The Backpack" ---
        lastTeleportTime = Time.time; // Reset the Quick Draw timer
        p1FellThisTurn = false;       // Reset fall flags for the next turn
        p2FellThisTurn = false;

        if (ownerIndex == lastTeleportOwner)
        {
            consecutiveTeleports++;
            if (consecutiveTeleports >= 3 && isMultiplayer && AchievementManager.Instance != null)
            {
                AchievementManager.Instance.UnlockAchievement("THE_BACKPACK");
            }
        }
        else
        {
            lastTeleportOwner = ownerIndex;
            consecutiveTeleports = 1; // Reset streak if the other player's ball is chosen
        }
        // --------------------------------------------------------

        ResetVotes();
        selectionActive = false;

        if (cameraController != null)
        {
            cameraController.PrepareForSwings();
        }
    }

    void ResetVotes()
    {
        player1VotedA = false;
        player1VotedB = false;
        player2VotedA = false;
        player2VotedB = false;
    }

    public void SetMultiplayerMode(bool enabled)
    {
        forceMultiplayerMode = enabled;
        DetectGameMode();
    }

    public bool IsMultiplayer()
    {
        return isMultiplayer;
    }

    // --- The original, untouched Stroke method ---
    public void RecordStroke()
    {
        strokeCount++;

        // Singleplayer / Global Progression Checks
        if (strokeCount == 1 && AchievementManager.Instance != null)
        {
            AchievementManager.Instance.UnlockAchievement("FIRST_SWING");
        }
        else if (strokeCount == 100 && AchievementManager.Instance != null)
        {
            AchievementManager.Instance.UnlockAchievement("CENTURY_CLUB");
        }
    }

    // --- NEW: Silent tracker for multiplayer achievements ---
    public void NotifyPlayerSwung(int playerIndex)
    {
        if (playerIndex == 0)
        {
            player1HasSwung = true;
            lastSwingTimeP1 = Time.time;
        }
        if (playerIndex == 1)
        {
            player2HasSwung = true;
            lastSwingTimeP2 = Time.time;
        }

        if (isMultiplayer && AchievementManager.Instance != null)
        {
            // 1. SYNCHRONIZED SWING (Great Minds Think Alike)
            if (lastSwingTimeP1 > 0 && lastSwingTimeP2 > 0 && Mathf.Abs(lastSwingTimeP1 - lastSwingTimeP2) <= 0.5f)
            {
                AchievementManager.Instance.UnlockAchievement("SYNCHRONIZED_SWING");
            }

            // 2. QUICK DRAW (No Time To Think!)
            if (lastTeleportTime > 0 && (Time.time - lastTeleportTime) <= 1.0f)
            {
                AchievementManager.Instance.UnlockAchievement("QUICK_DRAW");
            }

            // 3. IMPATIENT GOLFER (Get Out Of My Way!)
            GolfBallController[] balls = Object.FindObjectsByType<GolfBallController>(FindObjectsSortMode.None);
            foreach (var ball in balls)
            {
                // If the OTHER player's ball is still moving when we swing...
                if (ball.GetOwnerIndex() != playerIndex && !ball.IsStopped())
                {
                    AchievementManager.Instance.UnlockAchievement("IMPATIENT_GOLFER");
                    break;
                }
            }

            // 4. TEAM EFFORT
            if (player1HasSwung && player2HasSwung)
            {
                AchievementManager.Instance.UnlockAchievement("TEAM_EFFORT");
            }
        }
    }

    public int GetStrokeCount() => strokeCount;
    public void StartTimer() => timerRunning = true;
    public void StopTimer() => timerRunning = false;
    public float GetElapsedTime() => elapsedTime;

    public Vector3 GetPlayerOffsetFromBall() => playerOffsetFromBall;

    public void SetPlayerOffsetFromBall(Vector3 newOffset)
    {
        playerOffsetFromBall = newOffset;
    }

    public void NotifyLongFall(int playerIndex)
    {
        if (playerIndex == 0) p1FellThisTurn = true;
        if (playerIndex == 1) p2FellThisTurn = true;

        if (isMultiplayer && p1FellThisTurn && p2FellThisTurn && AchievementManager.Instance != null)
        {
            AchievementManager.Instance.UnlockAchievement("MISERY_COMPANY");
        }
    }
}