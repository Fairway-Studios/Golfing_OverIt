using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class ClubController2D : MonoBehaviour
{
    [Header("References")]
    public Transform playerOrigin;
    public GameObject golfBallPrefab;

    // modifiable physics components to tweak swing responsiveness
    [Header("Swing Settings")]
    public float maxForce = 500f;       
    public float torqueForce = 300f;    
    public float maxDistance = 2.5f;    

    [Header("Ball Spawn Settings")]
    public Vector2 ballOffset = new Vector2(0, -0.5f);

    private Rigidbody2D rb;
    private Vector2 moveInput;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        if (Gamepad.current != null)
        {
            moveInput = Gamepad.current.leftStick.ReadValue();

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