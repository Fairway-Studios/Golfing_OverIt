using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlaytestTeleportManager : MonoBehaviour
{
    [Header("Spawn Points In Order")]
    [SerializeField] private Transform[] spawnPoints;

    [Header("Optional Runtime Finish Teleport")]
    [SerializeField] private string finishFlagObjectName = "Finish Flag";
    [SerializeField] private string finishTeleportChildName = "FinishTeleport";

    [Header("Ball References")]
    [SerializeField] private Rigidbody2D ball1;
    [SerializeField] private Rigidbody2D ball2;

    [Header("Keys")]
    [SerializeField] private KeyCode nextSpawnKey = KeyCode.P;
    [SerializeField] private KeyCode resetToFirstKey = KeyCode.R;
    [SerializeField] private KeyCode previousShotCycleKey = KeyCode.G;

    [Header("Shot History")]
    [SerializeField] private bool enableShotHistory = true;
    [SerializeField] private int maxShotHistoryEntries = 100;
    [SerializeField] private float movingVelocityThreshold = 0.08f;
    [SerializeField] private float minHistoryPositionDifference = 0.2f;

    private int currentSpawnIndex = 0;

    private GameManager gameManager;
    private CameraController cameraController;
    private ObstaclePlacement2D obstaclePlacement;
    private Transform[] players;
    private Transform cameraTransform;

    private List<Transform> runtimeSpawnPoints = new List<Transform>();

    private readonly List<Vector3> shotHistoryPositions = new List<Vector3>();
    private int historyBrowseIndex = -1;
    private bool wasAnyBallMovingLastFrame = false;
    private bool suppressHistoryCaptureUntilBallsStop = false;

    private bool isBrowsingHistory = false;

    private void Start()
    {
        gameManager = Object.FindFirstObjectByType<GameManager>();
        cameraController = Object.FindFirstObjectByType<CameraController>();
        obstaclePlacement = Object.FindFirstObjectByType<ObstaclePlacement2D>();

        if (Camera.main != null)
            cameraTransform = Camera.main.transform;

        FindPlayers();
        FindBalls();
        BuildRuntimeSpawnList();
    }

    private void FindPlayers()
    {
        InputController[] controllers = Object.FindObjectsByType<InputController>(FindObjectsSortMode.None);

        System.Array.Sort(controllers, (a, b) => a.transform.root.name.CompareTo(b.transform.root.name));

        players = new Transform[controllers.Length];

        for (int i = 0; i < controllers.Length; i++)
        {
            players[i] = controllers[i].transform.root;
        }
    }

    private void FindBalls()
    {
        if (ball1 != null && ball2 != null)
            return;

        GolfBallController[] balls = FindObjectsByType<GolfBallController>(FindObjectsSortMode.None);

        foreach (GolfBallController ball in balls)
        {
            Rigidbody2D rb = ball.GetComponent<Rigidbody2D>();
            if (rb == null) continue;

            if (ball.GetOwnerIndex() == 0 && ball1 == null)
                ball1 = rb;
            else if (ball.GetOwnerIndex() == 1 && ball2 == null)
                ball2 = rb;
        }

        if (ball1 == null && balls.Length > 0)
            ball1 = balls[0].GetComponent<Rigidbody2D>();

        if (ball2 == null && balls.Length > 1)
            ball2 = balls[1].GetComponent<Rigidbody2D>();
    }

    private void BuildRuntimeSpawnList()
    {
        runtimeSpawnPoints.Clear();

        if (spawnPoints != null)
        {
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                if (spawnPoints[i] != null)
                    runtimeSpawnPoints.Add(spawnPoints[i]);
            }
        }

        GameObject finishFlag = GameObject.Find(finishFlagObjectName);
        if (finishFlag != null)
        {
            Transform finishTeleport = FindChildRecursive(finishFlag.transform, finishTeleportChildName);
            if (finishTeleport != null)
            {
                runtimeSpawnPoints.Add(finishTeleport);
                Debug.Log("[PlaytestTeleportManager] Added FinishTeleport from Finish Flag to end of spawn list.");
            }
            else
            {
                Debug.LogWarning("[PlaytestTeleportManager] Finish Flag found, but child '" + finishTeleportChildName + "' was not found.");
            }
        }
        else
        {
            Debug.LogWarning("[PlaytestTeleportManager] Could not find Finish Flag in scene.");
        }

        if (currentSpawnIndex >= runtimeSpawnPoints.Count)
            currentSpawnIndex = 0;
    }

    private void RefreshRuntimeSpawnList()
    {
        bool needsRefresh = false;

        if (runtimeSpawnPoints == null || runtimeSpawnPoints.Count == 0)
        {
            needsRefresh = true;
        }
        else
        {
            for (int i = runtimeSpawnPoints.Count - 1; i >= 0; i--)
            {
                if (runtimeSpawnPoints[i] == null)
                {
                    needsRefresh = true;
                    break;
                }
            }
        }

        if (needsRefresh)
        {
            BuildRuntimeSpawnList();

            if (runtimeSpawnPoints == null || runtimeSpawnPoints.Count == 0)
                return;

            if (currentSpawnIndex >= runtimeSpawnPoints.Count)
                currentSpawnIndex = 0;
        }
    }

    private Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent == null) return null;

        if (parent.name == childName)
            return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform result = FindChildRecursive(parent.GetChild(i), childName);
            if (result != null)
                return result;
        }

        return null;
    }

    private void Update()
    {
        if (GravityFlipZone.IsGravityFlipped())
            return;

        FindBalls();
        RefreshRuntimeSpawnList();
        TrackShotHistory();

        bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        if (shiftHeld && Input.GetKeyDown(nextSpawnKey))
        {
            if (!CanUseManualTeleport())
                return;

            isBrowsingHistory = false;
            TeleportToPreviousSpawn();
            return;
        }

        if (shiftHeld && Input.GetKeyDown(resetToFirstKey))
        {
            if (!CanUseManualTeleport())
                return;

            isBrowsingHistory = false;
            RestartScene();
            return;
        }

        if (Input.GetKeyDown(nextSpawnKey))
        {
            if (!CanUseManualTeleport())
                return;

            isBrowsingHistory = false;
            TeleportToNextSpawn();
            return;
        }

        if (Input.GetKeyDown(resetToFirstKey))
        {
            if (!CanUseManualTeleport())
                return;

            isBrowsingHistory = false;
            TeleportToFirstSpawn();
            return;
        }

        if (Input.GetKeyDown(previousShotCycleKey))
        {
            if (!CanUseManualTeleport())
                return;

            TeleportToPreviousShotHistoryPosition();
            return;
        }
    }

    private void TrackShotHistory()
    {
        if (!enableShotHistory)
            return;

        bool anyBallMoving = IsBallMoving(ball1) || IsBallMoving(ball2);

        if (suppressHistoryCaptureUntilBallsStop)
        {
            if (!anyBallMoving)
            {
                suppressHistoryCaptureUntilBallsStop = false;
            }

            wasAnyBallMovingLastFrame = anyBallMoving;
            return;
        }

        if (isBrowsingHistory)
        {
            if (!wasAnyBallMovingLastFrame && anyBallMoving)
            {
                isBrowsingHistory = false;
                SaveCurrentPositionToHistory(false);
            }

            wasAnyBallMovingLastFrame = anyBallMoving;
            return;
        }

        if (!wasAnyBallMovingLastFrame && anyBallMoving)
        {
            SaveCurrentPositionToHistory(false);
        }

        wasAnyBallMovingLastFrame = anyBallMoving;
    }

    private bool IsBallMoving(Rigidbody2D ball)
    {
        if (ball == null)
            return false;

        return ball.linearVelocity.sqrMagnitude > (movingVelocityThreshold * movingVelocityThreshold);
    }

    // NEW
    private bool AreAnyBallsMoving()
    {
        return IsBallMoving(ball1) || IsBallMoving(ball2);
    }

    // NEW
    private bool CanUseManualTeleport()
    {
        // Prevent teleporting while balls are still moving / mid-flight.
        if (AreAnyBallsMoving())
            return false;

        return true;
    }

    private void SaveCurrentPositionToHistory(bool forceSave)
    {
        Vector3 currentPos = GetCurrentSharedBallPosition();

        if (obstaclePlacement != null)
            currentPos = obstaclePlacement.ResolveSharedSafeBallPosition(currentPos);

        if (!forceSave && shotHistoryPositions.Count > 0)
        {
            float dist = Vector3.Distance(shotHistoryPositions[shotHistoryPositions.Count - 1], currentPos);
            if (dist < minHistoryPositionDifference)
                return;
        }

        shotHistoryPositions.Add(currentPos);

        if (shotHistoryPositions.Count > maxShotHistoryEntries)
            shotHistoryPositions.RemoveAt(0);

        historyBrowseIndex = shotHistoryPositions.Count;
    }

    private Vector3 GetCurrentSharedBallPosition()
    {
        if (ball1 != null)
            return ball1.position;

        if (ball2 != null)
            return ball2.position;

        return Vector3.zero;
    }

    private void TeleportToPreviousShotHistoryPosition()
    {
        if (!enableShotHistory)
            return;

        if (shotHistoryPositions == null || shotHistoryPositions.Count == 0)
            return;

        Vector3 currentPosition = GetCurrentSharedBallPosition();

        // Resolve current position too, so comparison is fair.
        if (obstaclePlacement != null)
            currentPosition = obstaclePlacement.ResolveSharedSafeBallPosition(currentPosition);

        isBrowsingHistory = true;

        int attempts = 0;
        int maxAttempts = shotHistoryPositions.Count;

        while (attempts < maxAttempts)
        {
            historyBrowseIndex--;

            if (historyBrowseIndex < 0)
                historyBrowseIndex = shotHistoryPositions.Count - 1;

            if (historyBrowseIndex >= shotHistoryPositions.Count)
                historyBrowseIndex = shotHistoryPositions.Count - 1;

            Vector3 candidatePosition = shotHistoryPositions[historyBrowseIndex];

            if (obstaclePlacement != null)
                candidatePosition = obstaclePlacement.ResolveSharedSafeBallPosition(candidatePosition);

            // Skip positions that resolve to basically the same place.
            if (Vector3.Distance(candidatePosition, currentPosition) > minHistoryPositionDifference)
            {
                TeleportToSpawn(candidatePosition);
                return;
            }

            attempts++;
        }

        // If everything resolves to the same spot, do nothing.
    }

    private void TeleportToNextSpawn()
    {
        if (runtimeSpawnPoints == null || runtimeSpawnPoints.Count == 0)
            return;

        currentSpawnIndex++;

        if (currentSpawnIndex >= runtimeSpawnPoints.Count)
            currentSpawnIndex = 0;

        if (runtimeSpawnPoints[currentSpawnIndex] == null)
        {
            RefreshRuntimeSpawnList();
            if (runtimeSpawnPoints == null || runtimeSpawnPoints.Count == 0)
                return;

            if (currentSpawnIndex >= runtimeSpawnPoints.Count)
                currentSpawnIndex = 0;

            if (runtimeSpawnPoints[currentSpawnIndex] == null)
                return;
        }

        Vector3 targetPosition = runtimeSpawnPoints[currentSpawnIndex].position;

        if (obstaclePlacement != null)
            targetPosition = obstaclePlacement.ResolveSharedSafeBallPosition(targetPosition);

        TeleportToSpawn(targetPosition);
    }

    private void TeleportToPreviousSpawn()
    {
        if (runtimeSpawnPoints == null || runtimeSpawnPoints.Count == 0)
            return;

        currentSpawnIndex--;

        if (currentSpawnIndex < 0)
            currentSpawnIndex = runtimeSpawnPoints.Count - 1;

        if (runtimeSpawnPoints[currentSpawnIndex] == null)
        {
            RefreshRuntimeSpawnList();
            if (runtimeSpawnPoints == null || runtimeSpawnPoints.Count == 0)
                return;

            if (currentSpawnIndex >= runtimeSpawnPoints.Count)
                currentSpawnIndex = runtimeSpawnPoints.Count - 1;

            if (currentSpawnIndex < 0 || runtimeSpawnPoints[currentSpawnIndex] == null)
                return;
        }

        Vector3 targetPosition = runtimeSpawnPoints[currentSpawnIndex].position;

        if (obstaclePlacement != null)
            targetPosition = obstaclePlacement.ResolveSharedSafeBallPosition(targetPosition);

        TeleportToSpawn(targetPosition);
    }

    private void TeleportToFirstSpawn()
    {
        RefreshRuntimeSpawnList();

        if (runtimeSpawnPoints == null || runtimeSpawnPoints.Count == 0)
            return;

        currentSpawnIndex = 0;

        if (runtimeSpawnPoints[currentSpawnIndex] == null)
            return;

        Vector3 targetPosition = runtimeSpawnPoints[currentSpawnIndex].position;

        if (obstaclePlacement != null)
            targetPosition = obstaclePlacement.ResolveSharedSafeBallPosition(targetPosition);

        TeleportToSpawn(targetPosition);
    }

    private void TeleportToSpawn(Vector3 targetPosition)
    {
        // NEW
        ForceCloseMultiplayerSelection();

        suppressHistoryCaptureUntilBallsStop = true;
        wasAnyBallMovingLastFrame = false;

        InputController[] controllers = Object.FindObjectsByType<InputController>(FindObjectsSortMode.None);
        foreach (var controller in controllers)
        {
            controller.OnPlayerTeleported();
        }

        TeleportBall(ball1, targetPosition);
        TeleportBall(ball2, targetPosition);

        Vector3 offset = Vector3.zero;
        if (gameManager != null)
            offset = gameManager.GetPlayerOffsetFromBall();

        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] == null) continue;

            players[i].position = targetPosition + offset;

            Rigidbody2D playerRb = players[i].GetComponent<Rigidbody2D>();
            if (playerRb != null)
            {
                playerRb.position = targetPosition + offset;
                playerRb.linearVelocity = Vector2.zero;
                playerRb.angularVelocity = 0f;
                playerRb.Sleep();
            }
        }

        if (cameraTransform != null)
        {
            cameraTransform.position = new Vector3(targetPosition.x, targetPosition.y, cameraTransform.position.z);
        }

        if (cameraController != null)
        {
            cameraController.PrepareForSwings();
        }
    }

    private void TeleportBall(Rigidbody2D ball, Vector3 targetPosition)
    {
        if (ball == null) return;

        GolfBallController controller = ball.GetComponent<GolfBallController>();

        ball.linearVelocity = Vector2.zero;
        ball.angularVelocity = 0f;
        ball.position = targetPosition;
        ball.transform.position = targetPosition;
        ball.Sleep();

        if (controller != null)
            controller.ResetForNextShot();
    }

    // NEW
    private void ForceCloseMultiplayerSelection()
    {
        if (gameManager == null)
            return;

        System.Type gmType = typeof(GameManager);
        BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;

        FieldInfo selectionActiveField = gmType.GetField("selectionActive", flags);
        FieldInfo player1VotedAField = gmType.GetField("player1VotedA", flags);
        FieldInfo player1VotedBField = gmType.GetField("player1VotedB", flags);
        FieldInfo player2VotedAField = gmType.GetField("player2VotedA", flags);
        FieldInfo player2VotedBField = gmType.GetField("player2VotedB", flags);
        FieldInfo selectionUIField = gmType.GetField("selectionUI", flags);
        FieldInfo ballIndicatorsField = gmType.GetField("ballIndicators", flags);

        if (selectionActiveField != null)
            selectionActiveField.SetValue(gameManager, false);

        if (player1VotedAField != null) player1VotedAField.SetValue(gameManager, false);
        if (player1VotedBField != null) player1VotedBField.SetValue(gameManager, false);
        if (player2VotedAField != null) player2VotedAField.SetValue(gameManager, false);
        if (player2VotedBField != null) player2VotedBField.SetValue(gameManager, false);

        if (selectionUIField != null)
        {
            GameObject selectionUI = selectionUIField.GetValue(gameManager) as GameObject;
            if (selectionUI != null)
                selectionUI.SetActive(false);
        }

        if (ballIndicatorsField != null)
        {
            object indicatorArrayObject = ballIndicatorsField.GetValue(gameManager);
            if (indicatorArrayObject is System.Array indicatorArray)
            {
                foreach (object indicatorObj in indicatorArray)
                {
                    if (indicatorObj == null)
                        continue;

                    BallIndicator indicator = indicatorObj as BallIndicator;
                    if (indicator != null)
                        indicator.Hide();
                }
            }
        }
    }


    public bool TryGetLatestSafePosition(out Vector3 safePosition)
    {
        if (shotHistoryPositions != null && shotHistoryPositions.Count > 0)
        {
            safePosition = shotHistoryPositions[shotHistoryPositions.Count - 1];

            if (obstaclePlacement != null)
                safePosition = obstaclePlacement.ResolveSharedSafeBallPosition(safePosition);

            return true;
        }

        if (runtimeSpawnPoints != null && runtimeSpawnPoints.Count > 0 && runtimeSpawnPoints[0] != null)
        {
            safePosition = runtimeSpawnPoints[0].position;

            if (obstaclePlacement != null)
                safePosition = obstaclePlacement.ResolveSharedSafeBallPosition(safePosition);

            return true;
        }

        safePosition = Vector3.zero;
        return false;
    }

    private void RestartScene()
    {
        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.name);
    }
}