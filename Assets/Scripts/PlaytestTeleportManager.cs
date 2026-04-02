using UnityEngine;
using UnityEngine.SceneManagement;

public class PlaytestTeleportManager : MonoBehaviour
{
    [Header("Spawn Points In Order")]
    [SerializeField] private Transform[] spawnPoints;

    [Header("Ball References")]
    [SerializeField] private Rigidbody2D ball1;
    [SerializeField] private Rigidbody2D ball2;

    [Header("Keys")]
    [SerializeField] private KeyCode nextSpawnKey = KeyCode.P;
    [SerializeField] private KeyCode resetToFirstKey = KeyCode.R;

    private int currentSpawnIndex = 0;

    private GameManager gameManager;
    private CameraController cameraController;
    private Transform[] players;
    private Transform cameraTransform;

    private void Start()
    {
        gameManager = Object.FindFirstObjectByType<GameManager>();
        cameraController = Object.FindFirstObjectByType<CameraController>();

        if (Camera.main != null)
            cameraTransform = Camera.main.transform;

        FindPlayers();
        FindBalls();
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

    private void Update()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
            return;

        bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        if (shiftHeld && Input.GetKeyDown(nextSpawnKey))
        {
            TeleportToPreviousSpawn();
            return;
        }

        if (shiftHeld && Input.GetKeyDown(resetToFirstKey))
        {
            RestartScene();
            return;
        }

        if (Input.GetKeyDown(nextSpawnKey))
        {
            TeleportToNextSpawn();
        }

        if (Input.GetKeyDown(resetToFirstKey))
        {
            TeleportToFirstSpawn();
        }
    }

    private void TeleportToNextSpawn()
    {
        currentSpawnIndex++;

        if (currentSpawnIndex >= spawnPoints.Length)
            currentSpawnIndex = 0;

        TeleportToSpawn(spawnPoints[currentSpawnIndex].position);
    }

    private void TeleportToPreviousSpawn()
    {
        currentSpawnIndex--;

        if (currentSpawnIndex < 0)
            currentSpawnIndex = spawnPoints.Length - 1;

        TeleportToSpawn(spawnPoints[currentSpawnIndex].position);
    }

    private void TeleportToFirstSpawn()
    {
        currentSpawnIndex = 0;
        TeleportToSpawn(spawnPoints[currentSpawnIndex].position);
    }

    private void TeleportToSpawn(Vector3 targetPosition)
    {
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

    private void RestartScene()
    {
        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.name);
    }
}