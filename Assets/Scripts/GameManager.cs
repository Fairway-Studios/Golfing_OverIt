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
            PlayerData data = SaveSystem.LoadPlayer();
            if (data != null)
            {
                Vector3 savedPos = new Vector3(data.position[0], data.position[1], data.position[2]);

                
                InputController[] controllers = Object.FindObjectsByType<InputController>(FindObjectsSortMode.None);

                // 1. Move Player
                if (players[0] != null)
                {
                    players[0].position = savedPos;
                    Rigidbody2D rb = players[0].GetComponent<Rigidbody2D>();
                    if (rb != null) rb.linearVelocity = Vector2.zero;
                }

                
                foreach (var c in controllers)
                {
                    c.OnPlayerTeleported();
                }

                // 2. Move Ball
                GolfBallController[] balls = Object.FindObjectsByType<GolfBallController>(FindObjectsSortMode.None);
                if (balls != null && balls.Length > 0 && balls[0] != null)
                {
                    Vector3 ballPos = savedPos - playerOffsetFromBall;
                    balls[0].transform.position = ballPos;

                    Rigidbody2D ballRb = balls[0].GetComponent<Rigidbody2D>();
                    if (ballRb != null)
                    {
                        ballRb.position = ballPos;
                        ballRb.linearVelocity = Vector2.zero;
                    }

                    balls[0].ResetForNextShot();
                }

                // 3. Move Camera
                if (cameraTransform != null)
                {
                    cameraTransform.position = new Vector3(savedPos.x, savedPos.y, cameraTransform.position.z);
                }

                Debug.Log("Game Loaded Successfully and Physics Reset!");
            }
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

    public void RecordStroke()
    {
        strokeCount++;
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
}