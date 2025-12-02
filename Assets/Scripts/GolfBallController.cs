using UnityEngine;

public class GolfBallController : MonoBehaviour
{
    [Header("Ownership")]
    [SerializeField] private int ownerPlayerIndex = 0;

    [Header("Stop Detection")]
    [SerializeField] private float stoppedVelocityThreshold = 0.2f;
    [SerializeField] private float stoppedCheckDuration = 0.5f;

    private Rigidbody2D rb;
    private float timeStationary = 0f;
    private bool hasStopped = false;
    private bool isLocked = false;
    private Vector3 hitStartPosition;
    private bool hasRecordedHitStart = false;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void FixedUpdate()
    {
        float speed = rb.linearVelocity.magnitude;

        // Record starting position when ball starts moving
        if (!hasRecordedHitStart && speed > stoppedVelocityThreshold)
        {
            hitStartPosition = transform.position;
            hasRecordedHitStart = true;
            hasStopped = false;
            isLocked = true; 
        }

        // Check if ball is moving
        if (speed > stoppedVelocityThreshold)
        {
            timeStationary = 0f;
            hasStopped = false;
        }
        else if (isLocked)
        {
            // Ball is slow/stopped
            timeStationary += Time.fixedDeltaTime;

            if (timeStationary >= stoppedCheckDuration && !hasStopped)
            {
                rb.linearVelocity = Vector2.zero;
                hasStopped = true;

                // Log distance
                if (hasRecordedHitStart)
                {
                    float distance = Vector2.Distance(hitStartPosition, transform.position);
                    Debug.Log($"[Player {ownerPlayerIndex + 1}] Shot Distance: {distance:F2}m");
                }
            }
        }
    }

    // Reset for next shot after teleport
    public void ResetForNextShot()
    {
        hasStopped = false;
        hasRecordedHitStart = false;
        timeStationary = 0f;
        isLocked = false;
    }

    // Public methods for other systems
    public int GetOwnerIndex()
    {
        return ownerPlayerIndex;
    }

    public Vector3 GetPosition()
    {
        return transform.position;
    }

    public Rigidbody2D GetRigidbody()
    {
        return rb;
    }

    public bool IsStopped()
    {
        return hasStopped;
    }

    public bool IsLocked()
    {
        return isLocked;
    }
}