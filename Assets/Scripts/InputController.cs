using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Rigidbody2D))]
public class InputController : MonoBehaviour
{
    private const float BASE_MOUSE_SENS = 100f;
    private const float BASE_CONTROLLER_SENS = 2000f;

    private string PREF_SENSITIVITY;
    private string PREF_INVERT_STICKS;

    [Header("Player Assignment")]
    [SerializeField] private int playerIndex = 0;

    [Header("References")]
    public Transform playerOrigin;
    public TextMeshProUGUI feedbackText;
    public SceneMGR sceneManager;
    public ParticleSystem hitParticles;

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

    [Header("Teleport Safety")]
    [SerializeField] private float postTeleportLock = 0.15f;
    [SerializeField] private float safeAngleDegrees = 120f;

    [Header("Sound Effects")]
    public AudioClip[] hitSounds;
    public AudioClip swapClubsSFX;

    private Rigidbody2D rb;
    private CameraController cameraController;
    private GameManager gameManager;
    private PlayerInput playerInput;

    private Vector2 moveInput;
    private Vector2 previousPosition;

    private GolfClubSettings currentClub;
    private GolfBallController[] allBalls;

    private bool canSwing = true;

    private bool movementLocked = false;
    private Vector2 lockedPosition;
    private float lockTimer = 0f;

    private bool anaglyphApplied = false;

    private string sceneName => SceneManager.GetActiveScene().name;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerInput = GetComponentInParent<PlayerInput>();

        cameraController = FindFirstObjectByType<CameraController>();
        gameManager = FindFirstObjectByType<GameManager>();

        RefreshBallList();

        if (availableClubs != null && availableClubs.Length > 0)
        {
            currentClub = availableClubs[currentClubIndex];

            if (feedbackText != null)
                feedbackText.text = "Current Club: " + currentClub.clubName;
        }

        BuildPrefKeys();
        LoadFromPrefs();
    }

    void Start()
    {
        previousPosition = rb.position;
    }

    private void BuildPrefKeys()
    {
        PREF_SENSITIVITY = $"Input_P{playerIndex}_Sensitivity";
        PREF_INVERT_STICKS = $"Input_P{playerIndex}_InvertSticks";
    }

    private void LoadFromPrefs()
    {
        float savedSensMultiplier = PlayerPrefs.GetFloat(PREF_SENSITIVITY, 1f);
        ApplySensitivity(savedSensMultiplier);

        bool savedInvert = PlayerPrefs.GetInt(PREF_INVERT_STICKS, 0) == 1;
        ApplyInvertSticks(savedInvert);
    }

    public void SetSensitivity(float sensitivity)
    {
        PlayerPrefs.SetFloat(PREF_SENSITIVITY, sensitivity);
        PlayerPrefs.Save();
        ApplySensitivity(sensitivity);
    }

    public void InvertSticks(bool swap)
    {
        PlayerPrefs.SetInt(PREF_INVERT_STICKS, swap ? 1 : 0);
        PlayerPrefs.Save();
        ApplyInvertSticks(swap);
    }

    private void ApplySensitivity(float sensitivity)
    {
        controllerSens = BASE_CONTROLLER_SENS * sensitivity;
        mouseSens = BASE_MOUSE_SENS * sensitivity;
    }

    private void ApplyInvertSticks(bool swap)
    {
        var swing = playerInput.actions["Swing"];
        var cam = playerInput.actions["MoveCamera"];

        swing.RemoveAllBindingOverrides();
        cam.RemoveAllBindingOverrides();

        if (!swap)
            return;

        swing.ApplyBindingOverride("<Gamepad>/rightStick");
        cam.ApplyBindingOverride("<Gamepad>/leftStick");
    }

    void RefreshBallList()
    {
        allBalls = FindObjectsByType<GolfBallController>(FindObjectsSortMode.None);
    }

    public void OnSwing(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    public void OnCycleClub(InputAction.CallbackContext context)
    {
        if (!context.performed)
            return;

        if (availableClubs == null || availableClubs.Length == 0)
            return;

        SFXManager.Instance.PlaySFX(swapClubsSFX);

        currentClubIndex = (currentClubIndex + 1) % availableClubs.Length;
        currentClub = availableClubs[currentClubIndex];

        if (feedbackText != null)
            feedbackText.text = "Current Club: " + currentClub.clubName;
    }

    public void OnCycleCamTarget(InputAction.CallbackContext context)
    {
        if (cameraController != null && context.performed && gameManager.IsMultiplayer() == true)
            cameraController.CycleTargetBall();
    }

    public void OnMoveCamera(InputAction.CallbackContext context)
    {
        if (cameraController != null)
            cameraController.OnCameraMove(context.ReadValue<Vector2>());
    }

    public void OnSelectBallA(InputAction.CallbackContext context)
    {
        bool held = context.phase == InputActionPhase.Performed;
        if (gameManager == null) return;
        if (playerIndex == 0) gameManager.OnPlayer1VoteA(held);
        else gameManager.OnPlayer2VoteA(held);
    }

    public void OnSelectBallB(InputAction.CallbackContext context)
    {
        bool held = context.phase == InputActionPhase.Performed;
        if (gameManager == null) return;
        if (playerIndex == 0) gameManager.OnPlayer1VoteB(held);
        else gameManager.OnPlayer2VoteB(held);
    }

    void FixedUpdate()
    {
        if (allBalls == null || allBalls.Length == 0)
            RefreshBallList();

        if (movementLocked)
        {
            lockTimer -= Time.fixedDeltaTime;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.position = lockedPosition;
            previousPosition = rb.position;
            if (lockTimer <= 0f) movementLocked = false;
            return;
        }

        Vector2 velocity = (rb.position - previousPosition) / Time.fixedDeltaTime;
        string scheme = playerInput.currentControlScheme;

        if (scheme == "Mouse") HandleMouseMovement();
        else if (scheme == "Controller") HandleControllerMovement();

        if (sceneName == "SingleplayerScene")
            ApplySinglePlayerAnaglyphOverride();

        UpdateSwingState();

        if (canSwing)
            CheckBallHit(velocity);

        previousPosition = rb.position;
    }

    void HandleControllerMovement()
    {
        Vector2 dir = moveInput.normalized;
        rb.AddForce(dir * controllerSens * moveInput.magnitude);
        ClampToRadius();
    }

    void HandleMouseMovement()
    {
        if (Mouse.current == null || Camera.main == null) return;

        Vector2 screen = Mouse.current.position.ReadValue();
        Vector2 world = Camera.main.ScreenToWorldPoint(screen);
        Vector2 dir = world - (Vector2)playerOrigin.position;

        if (dir.magnitude > maxDistance)
            dir = dir.normalized * maxDistance;

        Vector2 target = (Vector2)playerOrigin.position + dir;
        rb.linearVelocity = (target - rb.position) * mouseSens;
    }

    void ClampToRadius()
    {
        Vector2 offset = rb.position - (Vector2)playerOrigin.position;
        if (offset.magnitude > maxDistance)
            rb.position = (Vector2)playerOrigin.position + offset.normalized * maxDistance;
    }

    void UpdateSwingState()
    {
        if (!canSwing)
        {
            foreach (var ball in allBalls)
            {
                if (ball == null) continue;
                if (ball.GetOwnerIndex() != playerIndex) continue;
                if (Vector2.Distance(rb.position, ball.transform.position) > readyDistance)
                {
                    canSwing = true;
                    break;
                }
            }
        }
    }

    void CheckBallHit(Vector2 clubVelocity)
    {
        foreach (var ball in allBalls)
        {
            if (ball == null) continue;
            if (ball.GetOwnerIndex() != playerIndex) continue;
            if (ball.IsLocked()) continue;
            if (Vector2.Distance(rb.position, ball.transform.position) > hitRadius) continue;

            float speed = clubVelocity.magnitude;
            if (speed < minSwingSpeed) continue;

            Vector2 impulse = speed > maxSwingSpeed
                ? clubVelocity.normalized * maxSwingSpeed
                : clubVelocity;

            HitBall(ball.gameObject, impulse);
        }
    }

    void HitBall(GameObject ball, Vector2 velocity)
    {
        if (currentClub == null || sceneManager.IsGamePaused()) return;

        Rigidbody2D ballRb = ball.GetComponent<Rigidbody2D>();
        if (ballRb == null) return;
        if (ballRb.linearVelocity.magnitude >= 0.2f) return;

        Vector2 impulse = velocity * currentClub.impulseMultiplier;
        impulse.y += Mathf.Abs(impulse.x) * currentClub.upwardBias;
        ballRb.AddForce(impulse, ForceMode2D.Impulse);

        PlayHitSound();

        if (playerIndex == 0)
            gameManager.RecordStroke();

        if (hitParticles != null && currentClub.clubName != "Putter")
        {
            hitParticles.transform.position = ball.transform.position;
            hitParticles.Play();
        }

        if (cameraController != null)
            cameraController.OnPlayerSwung(playerIndex);

        canSwing = false;
    }

    public void OnPlayerTeleported()
    {
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        GolfBallController myBall = null;
        foreach (var ball in allBalls)
        {
            if (ball != null && ball.GetOwnerIndex() == playerIndex)
            {
                myBall = ball;
                break;
            }
        }

        if (myBall == null) return;

        Vector2 ballPos = myBall.transform.position;
        float rad = safeAngleDegrees * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)).normalized;
        Vector2 safePos = ballPos + dir * (readyDistance + 2f);

        lockedPosition = safePos;
        rb.position = safePos;
        movementLocked = true;
        lockTimer = postTeleportLock;
        canSwing = false;
        previousPosition = rb.position;

        if (Mouse.current != null && Camera.main != null && playerInput.currentControlScheme == "Mouse")
            Mouse.current.WarpCursorPosition(Camera.main.WorldToScreenPoint(safePos));
    }

    void ApplySinglePlayerAnaglyphOverride()
    {
        if (anaglyphApplied) return;

        var elements = FindObjectsByType<AnaglyphRenderingController>(FindObjectsSortMode.None);
        foreach (var e in elements)
            e.ApplySinglePlayerColorOverride();

        anaglyphApplied = true;
    }

    void PlayHitSound()
    {
        if (hitSounds == null || hitSounds.Length == 0) return;

        AudioClip clip = currentClub.clubName == "Putter"
            ? hitSounds[0]
            : hitSounds[Random.Range(1, hitSounds.Length)];

        SFXManager.Instance.PlaySFX(clip);
    }

    public int GetPlayerIndex() => playerIndex;
}