using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Rigidbody2D))]
public class InputController : MonoBehaviour
{
    private const float BASE_MOUSE_SENS = 100f;
    private const float BASE_CONTROLLER_SENS = 2000f;

    [Header("Player Assignment")]
    [SerializeField] private int playerIndex = 0;

    [Header("References")]
    public Transform playerOrigin;
    public TextMeshProUGUI feedbackText;
    public SceneMGR sceneManager;

    [Header("Swing Settings")]
    public float controllerSens = BASE_CONTROLLER_SENS;
    public float mouseSens = BASE_MOUSE_SENS;
    public float maxDistance = 2f;

    [Header("Club Settings")]
    [SerializeField] private GolfClubSettings[] availableClubs;
    [SerializeField] private int currentClubIndex = 1;

    [Header("Ball Hit Settings")]
    public float minSwingSpeed = 1f;
    public float maxSwingSpeed = 12f;
    public float hitRadius = 0.5f;
    [SerializeField] private float readyDistance = 1f;

    [Header("Sound Effects")]
    public AudioClip[] hitSounds;

    private Rigidbody2D rb;
    private CameraController cameraController;
    private GameManager gameManager;
    private Vector2 moveInput;
    private Vector2 previousPosition;
    private PlayerInput playerInput;
    private GolfClubSettings currentClub;
    private bool canSwing = true;
    private GolfBallController[] allBalls;
    private string sceneName => SceneManager.GetActiveScene().name;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerInput = GetComponentInParent<PlayerInput>();
        cameraController = Object.FindFirstObjectByType<CameraController>();
        gameManager = Object.FindFirstObjectByType<GameManager>();

        if (availableClubs != null && availableClubs.Length > 0)
        {
            currentClub = availableClubs[currentClubIndex];
            if (feedbackText != null)
                feedbackText.text = "Current Club: " + currentClub.clubName;
        }

        allBalls = Object.FindObjectsByType<GolfBallController>(FindObjectsSortMode.None);
    }

    void Start()
    {
        previousPosition = rb.position;
    }
    void ApplySinglePlayerAnaglyphOverride()
    {
        AnaglyphRenderingController[] anaglyphElements =
            Object.FindObjectsByType<AnaglyphRenderingController>(FindObjectsSortMode.None);

        foreach (var element in anaglyphElements)
        {
            element.ApplySinglePlayerColorOverride();
        }
    }

    public void OnSwing(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    public void OnCycleClub(InputAction.CallbackContext context)
    {
        if (context.performed && availableClubs != null && availableClubs.Length > 0)
        {
            currentClubIndex = (currentClubIndex + 1) % availableClubs.Length;
            currentClub = availableClubs[currentClubIndex];
            if (feedbackText != null)
                feedbackText.text = "Current Club: " + currentClub.clubName;
        }
    }

    public void OnCycleCamTarget(InputAction.CallbackContext context)
    {
        if (cameraController != null && context.performed)
            cameraController.CycleTargetBall();
    }

    public void OnMoveCamera(InputAction.CallbackContext context)
    {
        if (cameraController != null)
            cameraController.OnCameraMove(context.ReadValue<Vector2>());
    }

    public void OnSelectBallA(InputAction.CallbackContext context)
    {
        bool held = (context.phase == InputActionPhase.Performed);
        if (playerIndex == 0)
            gameManager.OnPlayer1VoteA(held);
        else if (playerIndex == 1)
            gameManager.OnPlayer2VoteA(held);
    }

    public void OnSelectBallB(InputAction.CallbackContext context)
    {
        bool held = (context.phase == InputActionPhase.Performed);
        if (playerIndex == 0)
            gameManager.OnPlayer1VoteB(held);
        else if (playerIndex == 1)
            gameManager.OnPlayer2VoteB(held);
    }

    void FixedUpdate()
    {
        Vector2 currentVelocity = (rb.position - previousPosition) / Time.fixedDeltaTime;

        string scheme = playerInput.currentControlScheme;

        if (scheme == "Mouse")
        {
            HandleMouseMovement();
        }
        else if (scheme == "Controller")
        {
            HandleControllerMovement();
        }

        if (sceneName == "SingleplayerScene")
        {
            ApplySinglePlayerAnaglyphOverride();
        }

        if (!canSwing)
        {
            foreach (GolfBallController ball in allBalls)
            {
                if (ball.GetOwnerIndex() != playerIndex)
                    continue;

                float dist = Vector2.Distance(rb.position, ball.transform.position);
                if (dist > readyDistance)
                {
                    canSwing = true;
                    break;
                }
            }
        }

        if (canSwing)
            CheckBallHit(currentVelocity);

        previousPosition = rb.position;
    }

    private void HandleControllerMovement()
    {
        Vector2 inputDir = moveInput.normalized;
        Vector2 force = inputDir * controllerSens * moveInput.magnitude;
        rb.AddForce(force);
        ClampToPlayerRadius();
    }

    private void HandleMouseMovement()
    {
        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        Vector2 target = mouseWorld;
        Vector2 direction = target - (Vector2)playerOrigin.position;

        if (direction.magnitude > maxDistance)
            direction = direction.normalized * maxDistance;

        Vector2 clamped = (Vector2)playerOrigin.position + direction;
        Vector2 delta = (clamped - rb.position) * mouseSens;
        rb.linearVelocity = delta;
    }

    private void ClampToPlayerRadius()
    {
        Vector2 offset = rb.position - (Vector2)playerOrigin.position;
        if (offset.magnitude > maxDistance)
            rb.position = (Vector2)playerOrigin.position + offset.normalized * maxDistance;
    }

    private void CheckBallHit(Vector2 clubVelocity)
    {
        foreach (GolfBallController ball in allBalls)
        {
            if (ball.GetOwnerIndex() != playerIndex)
                continue;

            if (ball.IsLocked())
                continue;

            float dist = Vector2.Distance(rb.position, ball.transform.position);
            if (dist < hitRadius)
            {
                float swing = clubVelocity.magnitude;
                if (swing >= minSwingSpeed)
                {
                    Vector2 impulse = clubVelocity;
                    if (swing > maxSwingSpeed)
                        impulse = clubVelocity.normalized * maxSwingSpeed;

                    HitBall(ball.gameObject, impulse);
                }
            }
        }
    }

    private void HitBall(GameObject ball, Vector2 velocity)
    {
        if (currentClub == null || sceneManager.IsGamePaused())
            return;

        Rigidbody2D ballRb = ball.GetComponent<Rigidbody2D>();
        if (ballRb != null && ballRb.linearVelocity.magnitude < 0.2f)
        {
            Vector2 impulse = velocity * currentClub.impulseMultiplier;
            impulse.y += Mathf.Abs(impulse.x) * currentClub.upwardBias;

            ballRb.AddForce(impulse, ForceMode2D.Impulse);

            PlayHitSound();

            // Notify camera that this player has swung
            if (cameraController != null)
            {
                cameraController.OnPlayerSwung(playerIndex);
            }

            canSwing = false;
        }
    }

    void PlayHitSound()
    {
        if (hitSounds.Length == 0)
            return;

        AudioClip clip;

        if (currentClub.clubName == "Putter")
            clip = hitSounds[0];
        else
            clip = hitSounds[Random.Range(1, hitSounds.Length)];

        SFXManager.Instance.PlaySFX(clip);
    }

    public void OnPlayerTeleported()
    {
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        foreach (GolfBallController ball in allBalls)
        {
            if (ball.GetOwnerIndex() != playerIndex)
                continue;

            float dist = Vector2.Distance(rb.position, ball.transform.position);
            Vector2 dir = (rb.position - (Vector2)ball.transform.position).normalized;
            if (dir.sqrMagnitude < 0.01f)
                dir = Vector2.right;

            rb.position = (Vector2)ball.transform.position + dir * (readyDistance + 0.5f);
        }

        canSwing = false;
    }

    public int GetPlayerIndex()
    {
        return playerIndex;
    }

    public void SetSensitivity(float sensitivity)
    {
        controllerSens = BASE_CONTROLLER_SENS * sensitivity;
        mouseSens = BASE_MOUSE_SENS * sensitivity;
    }

    public void InvertSticks(bool swap)
    {
        if (swap)
        {
            playerInput.actions["MoveCamera"].ApplyBindingOverride("<Gamepad>/leftStick");
            playerInput.actions["Swing"].ApplyBindingOverride("<Gamepad>/rightStick");
        }
        else
        {
            playerInput.actions.RemoveAllBindingOverrides();
        }
    }
}