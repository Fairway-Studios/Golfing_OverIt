using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class InputController : MonoBehaviour
{
    [Header("Player Assignment")]
    [SerializeField] private int playerIndex = 0;
    [SerializeField] private bool preferMouse = false;

    [Header("References")]
    public Transform playerOrigin;
    public TextMeshProUGUI feedbackText;

    [Header("Swing Settings")]
    public float controllerSens = 2000f;
    public float mouseSens = 100f;
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
    private bool usingMouse = false;
    private GolfClubSettings currentClub;
    private bool canSwing = true;
    private GolfBallController[] allBalls;

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

            AssignDevice();
    }

    void AssignDevice()
    {
        var gamepads = Gamepad.all;
        playerInput.neverAutoSwitchControlSchemes = true;

        if (preferMouse)
        {
            AssignMouse();
        }
        else
        {
            if (playerIndex < gamepads.Count)
            {
                playerInput.SwitchCurrentControlScheme("Controller", gamepads[playerIndex]);
                usingMouse = false;
            }
            else
            {
                AssignMouse();
            }
        }
    }

    void AssignMouse()
    {
        if (Mouse.current != null)
        {
            playerInput.SwitchCurrentControlScheme("Mouse", Mouse.current);
            usingMouse = true;
        }
    }

    void Start()
    {
        previousPosition = rb.position;
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

        if (usingMouse)
            HandleMouseMovement();
        else
            HandleControllerMovement();

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
        if (currentClub == null)
            return;

        Rigidbody2D ballRb = ball.GetComponent<Rigidbody2D>();
        if (ballRb != null && ballRb.linearVelocity.magnitude < 0.2f)
        {
            Vector2 impulse = velocity * currentClub.impulseMultiplier;
            impulse.y += Mathf.Abs(impulse.x) * currentClub.upwardBias;

            ballRb.AddForce(impulse, ForceMode2D.Impulse);

            PlayHitSound();

            // TESTING change ball color to black on hit
            /*SpriteRenderer ballRenderer = ball.GetComponent<SpriteRenderer>();
            ballRenderer.color = Color.black;*/

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

            // TESTING anaglyph rendering for golf ball
            /*SpriteRenderer ballRenderer = ball.GetSpriteRenderer();
            SpriteRenderer clubRenderer = this.GetComponent<SpriteRenderer>();
            Color color = clubRenderer.color;
            ballRenderer.color = color;*/

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
}
