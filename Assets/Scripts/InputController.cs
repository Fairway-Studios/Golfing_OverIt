using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class InputController : MonoBehaviour
{
    [Header("References")]
    public Transform playerOrigin;

    [Header("Swing Settings")]
    public float maxForce = 1000f;
    public float maxDistance = 2f;

    [Header("Ball Hit Settings")]
    public float impulseMultiplier = 1f;
    public float upwardBias = 0.5f; 
    public float minSwingSpeed = 1f;
    public float hitRadius = 0.5f; 

    private Rigidbody2D rb;
    private Vector2 moveInput;
    private bool usingMouse = false;
    private Vector2 previousPosition;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Start()
    {
        previousPosition = rb.position;
    }

    void Update()
    {
        if (Gamepad.current != null)
        {
            usingMouse = false;
            moveInput = Gamepad.current.leftStick.ReadValue();
            return;
        }
        usingMouse = true;
        moveInput = Vector2.zero;
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

        CheckBallHit(currentVelocity);

        previousPosition = rb.position;
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
        Vector2 moveDelta = (clampedTarget - rb.position) * 80f;
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
                HitBall(ball, clubVelocity);
            }
        }
    }

    private void HitBall(GameObject ball, Vector2 clubVelocity)
    {
        Rigidbody2D ballRb = ball.GetComponent<Rigidbody2D>();
        if (ballRb != null)
        {
            // Add horizontal and vertical impulse to provide more satisfying launch
            Vector2 impulse = clubVelocity * impulseMultiplier;
            impulse.y += Mathf.Abs(impulse.x) * upwardBias;

            ballRb.AddForce(impulse, ForceMode2D.Impulse);
        }
    }
}