using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class InputController : MonoBehaviour
{
    [Header("References")]
    public Transform playerOrigin;
    public GameObject golfBallPrefab;

    [Header("Swing Settings")]
    public float maxForce;
    public float maxDistance;

    [Header("Ball Spawn Settings")]
    public Vector2 ballOffset;

    private Rigidbody2D rb;
    private Vector2 moveInput;
    private bool usingMouse = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        if (Gamepad.current != null)
        {
            usingMouse = false;

            moveInput = Gamepad.current.leftStick.ReadValue();

            if (Gamepad.current.buttonSouth.wasPressedThisFrame)
                SpawnGolfBall();

            return;
        }

        usingMouse = true;
        moveInput = Vector2.zero;

        if (Mouse.current.leftButton.wasPressedThisFrame)
            SpawnGolfBall();

    }

    void FixedUpdate()
    {
        if (playerOrigin == null) return;

        if (usingMouse)
        {
            HandleMouseMovement();
        }
        else
        {
            HandleControllerMovement();
        }
    }

    private void HandleControllerMovement()
    {
        Vector2 inputDir = moveInput.normalized;
        Vector2 force = inputDir * maxForce * moveInput.magnitude;
        rb.AddForce(force);

        ClampToPlayerRadius();
    }

    private void HandleMouseMovement()
    {
        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        Vector2 targetPos = (Vector2)mouseWorldPos;

        Vector2 direction = targetPos - (Vector2)playerOrigin.position;

        if (direction.magnitude > maxDistance)
        {
            direction = direction.normalized * maxDistance;
        }

        Vector2 clampedTarget = (Vector2)playerOrigin.position + direction;

        Vector2 moveDelta = (clampedTarget - rb.position) * 30f;
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

    private void SpawnGolfBall()
    {
        if (golfBallPrefab == null || playerOrigin == null)
        {
            Debug.LogWarning("Missing golfBallPrefab or playerOrigin reference!");
            return;
        }

        Vector2 spawnPos = (Vector2)playerOrigin.position + ballOffset;
        Instantiate(golfBallPrefab, spawnPos, Quaternion.identity);
    }
}
