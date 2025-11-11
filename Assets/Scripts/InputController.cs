using UnityEngine;
using UnityEngine.InputSystem; // For Xbox controller input

[RequireComponent(typeof(Rigidbody2D))]
public class ClubController2D : MonoBehaviour
{
    [Header("References")]
    public Transform playerOrigin;      // The golfer's torso or hand origin
    public GameObject golfBallPrefab;   // Assign your golf ball prefab here

    [Header("Swing Settings")]
    public float maxForce = 500f;       // Swing strength
    public float torqueForce = 300f;    // Rotational torque
    public float maxDistance = 2.5f;    // Max distance the club can move from the player

    [Header("Ball Spawn Settings")]
    public Vector2 ballOffset = new Vector2(0, -0.5f); // Offset below player's feet

    private Rigidbody2D rb;
    private Vector2 moveInput;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        // Read left stick input from Xbox controller
        if (Gamepad.current != null)
        {
            moveInput = Gamepad.current.leftStick.ReadValue();

            // Check for A button press (buttonSouth)
            if (Gamepad.current.buttonSouth.wasPressedThisFrame)
            {
                SpawnGolfBall();
            }
        }
        else
        {
            moveInput = Vector2.zero;
        }
    }

    void FixedUpdate()
    {
        if (playerOrigin == null) return;

        // Apply force based on joystick direction
        Vector2 inputDir = moveInput.normalized;
        Vector2 force = inputDir * maxForce * moveInput.magnitude;
        rb.AddForce(force);

        // Optionally: add rotational torque for right stick spin
        if (Gamepad.current != null)
        {
            float torqueInput = Gamepad.current.rightStick.x.ReadValue();
            rb.AddTorque(-torqueInput * torqueForce);
        }

        // Constrain distance from player (keep within swing range)
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