using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
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
    private Vector2 moveInput;
    private Vector2 previousPosition;
    private PlayerInput playerInput;
    private bool usingMouse = false;
    private GolfClubSettings currentClub;
    private bool canSwing = true;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerInput = GetComponentInParent<PlayerInput>();

        if (playerInput == null)
        {
            return;
        }

        if (availableClubs != null && availableClubs.Length > 0)
        {
            currentClub = availableClubs[currentClubIndex];
            if (feedbackText != null)
                feedbackText.text = "Current Club: " + currentClub.clubName;
        }

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
                var gamepad = gamepads[playerIndex];
                playerInput.SwitchCurrentControlScheme("Controller", gamepad);
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

    public void OnSwing(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    public void OnCycleClub(InputValue value)
    {
        if (availableClubs == null || availableClubs.Length == 0) return;

        currentClubIndex = (currentClubIndex + 1) % availableClubs.Length;
        currentClub = availableClubs[currentClubIndex];
        if (feedbackText != null)
            feedbackText.text = "Current Club: " + currentClub.clubName;
    }

    void FixedUpdate()
    {
        if (playerOrigin == null) return;

        Vector2 currentVelocity = (rb.position - previousPosition) / Time.fixedDeltaTime;

        if (usingMouse)
        {
            HandleMouseMovement();
        }
        else
        {
            HandleControllerMovement();
        }

        // Check if club has moved far enough to enable swinging
        if (!canSwing)
        {
            GameObject ball = GameObject.FindGameObjectWithTag("GolfBall");
            if (ball != null)
            {
                float distanceFromBall = Vector2.Distance(rb.position, ball.transform.position);

                // Debug every frame when not ready
                if (Time.frameCount % 30 == 0)
                {
                    Debug.Log($"[{transform.root.name}] NOT READY - Distance from ball: {distanceFromBall:F2}, need {readyDistance:F2}");
                }

                if (distanceFromBall > readyDistance)
                {
                    canSwing = true;
                    Debug.Log($"[{transform.root.name}] SWING READY! Distance: {distanceFromBall:F2}");
                }
            }
            else
            {
                Debug.LogWarning($"[{transform.root.name}] Can't find ball with tag 'GolfBall'!");
            }
        }

        // Only check for ball hits if swing is ready
        if (canSwing)
        {
            CheckBallHit(currentVelocity);
        }
        else
        {
            // Debug why we can't swing
            if (Time.frameCount % 60 == 0)
            {
                Debug.Log($"[{transform.root.name}] Skipping CheckBallHit - canSwing is false");
            }
        }

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
        if (Mouse.current == null) return;

        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        Vector2 targetPos = (Vector2)mouseWorldPos;
        Vector2 direction = targetPos - (Vector2)playerOrigin.position;

        if (direction.magnitude > maxDistance)
        {
            direction = direction.normalized * maxDistance;
        }

        Vector2 clampedTarget = (Vector2)playerOrigin.position + direction;
        Vector2 moveDelta = (clampedTarget - rb.position) * mouseSens;
        rb.linearVelocity = moveDelta;
    }

    private void ClampToPlayerRadius()
    {
        Vector2 offset = rb.position - (Vector2)playerOrigin.position;
        if (offset.magnitude > maxDistance)
        {
            rb.position = (Vector2)playerOrigin.position + offset.normalized * maxDistance;
        }
    }

    private void CheckBallHit(Vector2 clubVelocity)
    {
        GameObject ball = GameObject.FindGameObjectWithTag("GolfBall");
        if (ball == null) return;

        float distance = Vector2.Distance(rb.position, ball.transform.position);

        if (distance < hitRadius)
        {
            float swingSpeed = clubVelocity.magnitude;

            if (swingSpeed >= minSwingSpeed)
            {
                Vector2 clampedVelocity = clubVelocity;
                if (swingSpeed > maxSwingSpeed)
                {
                    clampedVelocity = clubVelocity.normalized * maxSwingSpeed;
                }

                HitBall(ball, clampedVelocity);
            }
        }
    }

    private void HitBall(GameObject ball, Vector2 clubVelocity)
    {
        if (currentClub == null)
        {
            Debug.LogWarning("No club selected!");
            return;
        }

        Rigidbody2D ballRb = ball.GetComponent<Rigidbody2D>();
        if (ballRb != null && ballRb.linearVelocity.magnitude < 0.2)
        {
            Vector2 impulse = clubVelocity * currentClub.impulseMultiplier;
            impulse.y += Mathf.Abs(impulse.x) * currentClub.upwardBias;

            ballRb.AddForce(impulse, ForceMode2D.Impulse);

            PlayRandomHitSound();

            Debug.Log($"[{transform.root.name}] Ball hit! Swing disabled until moved away.");
            canSwing = false;
        }
    }

    void PlayRandomHitSound()
    {
        if (hitSounds.Length == 0) return;

        int index = Random.Range(0, hitSounds.Length);
        AudioClip clip = hitSounds[index];

        SFXManager.Instance.PlaySFX(clip);
    }

    // Called when player teleports
    public void OnPlayerTeleported()
    {
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        GameObject ball = GameObject.FindGameObjectWithTag("GolfBall");
        if (ball != null)
        {
            float currentDistance = Vector2.Distance(rb.position, ball.transform.position);

            Vector2 dir = (rb.position - (Vector2)ball.transform.position).normalized;
            if (dir.sqrMagnitude < 0.01f)
                dir = Vector2.right; // fallback safety

            rb.position = (Vector2)ball.transform.position + dir * (readyDistance + 0.5f);

            Debug.Log(
                $"[{transform.root.name}] TELEPORTED - " +
                $"Club forced away from ball. New Distance: {Vector2.Distance(rb.position, ball.transform.position):F2}"
            );
        }

        // Disable swinging until movement reached
        canSwing = false;
    }
}