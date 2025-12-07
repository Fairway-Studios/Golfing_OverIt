using UnityEngine;

public class GolfBallController : MonoBehaviour
{
    [Header("Ownership")]
    [SerializeField] private int ownerPlayerIndex = 0;

    [Header("Stop Detection")]
    [SerializeField] private float stoppedVelocityThreshold = 1.5f;
    [SerializeField] private float stoppedCheckDuration = 2f;

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

        if (!hasRecordedHitStart && speed > 0.01f)
        {
            hitStartPosition = transform.position;
            hasRecordedHitStart = true;
            hasStopped = false;
            isLocked = true;
        }

        if (speed > stoppedVelocityThreshold)
        {
            timeStationary = 0f;
            hasStopped = false;
        }
        else if (hasRecordedHitStart)
        {
            timeStationary += Time.fixedDeltaTime;

            if (timeStationary >= stoppedCheckDuration && !hasStopped)
            {
                hasStopped = true;

                // Freeze ball position and velocity to prevent glitch bounce
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
                rb.position = rb.position;

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

    public SpriteRenderer GetSpriteRenderer()
    {
        return this.GetComponentInParent<SpriteRenderer>();
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