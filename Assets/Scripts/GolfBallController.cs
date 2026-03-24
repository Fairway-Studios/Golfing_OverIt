using UnityEngine;

public class GolfBallController : MonoBehaviour
{
    [Header("Ownership")]
    [SerializeField] private int ownerPlayerIndex = 0;

    [Header("Stop Detection")]
    [SerializeField] private float stoppedVelocityThreshold = 1.5f;
    [SerializeField] private float stoppedCheckDuration = 2f;

    [Header("Bounce Effects")]
    [SerializeField] private ParticleSystem bounceParticles;
    [SerializeField] private AudioSource bounceAudio;
    [SerializeField] private AudioClip bounceSoundClip;

    private Rigidbody2D rb;
    private float timeStationary = 0f;
    private bool hasStopped = false;
    private bool isLocked = false;
    private Vector3 hitStartPosition;
    private bool hasRecordedHitStart = false;

    private float minBounceVelocity = 2f;

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

                // Set velocity 0 to prevent glitch bounce
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
                rb.position = transform.position;

                if (hasRecordedHitStart)
                {
                    float distance = Vector2.Distance(hitStartPosition, transform.position);
                    Debug.Log($"[Player {ownerPlayerIndex + 1}] Shot Distance: {distance:F2}m");

                    // If the ball traveled more than 50 units, trigger the achievement!
                    if (distance >= 50f && AchievementManager.Instance != null)
                    {
                        // Make sure you create an AchievementData object with this exact ID!
                        AchievementManager.Instance.UnlockAchievement("LONG_DRIVE");
                    }

                    // --- ACHIEVEMENT: Surgical Precision (Tiny Shot) ---
                    if (distance < 1f && distance > 0.1f && AchievementManager.Instance != null)
                    {
                        AchievementManager.Instance.UnlockAchievement("TINY_TAP");
                    }

                    // --- ACHIEVEMENT: Gravity is Cruel (Long Fall) ---
                    // If your starting Y position was 20 units HIGHER than your ending Y position
                    if (hitStartPosition.y - transform.position.y >= 20f && AchievementManager.Instance != null)
                    {
                        AchievementManager.Instance.UnlockAchievement("LONG_FALL");
                    }
                }

                // --- ACHIEVEMENT: I Can See My House! (High Altitude) ---
                // We check this after the ball stops to see if you landed really high up
                if (transform.position.y >= 50f && AchievementManager.Instance != null)
                {
                    AchievementManager.Instance.UnlockAchievement("HIGH_ALTITUDE");
                }
            }
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        // Get vertical component of the collision impact
        float verticalImpact = Mathf.Abs(rb.linearVelocity.y);

        // Only trigger if the bounce is big enough
        if (verticalImpact >= minBounceVelocity)
        {
            Vector2 contactPoint = collision.contacts[0].point;
            TriggerBounceEffects(verticalImpact, contactPoint);
        }

        // --- ACHIEVEMENT: Massive Ricochet ---
        // Temporarily print the impact speed to the console
        Debug.Log($"IMPACT FORCE: {verticalImpact}");

        if (verticalImpact >= 30f && AchievementManager.Instance != null)
        {
            AchievementManager.Instance.UnlockAchievement("HARD_BOUNCE");
        }
    }

    void TriggerBounceEffects(float impactVelocity, Vector2 position)
    {

        bounceParticles.transform.position = position;
        bounceParticles.Play();

        // Trigger sound
        PlayBounceSound(impactVelocity);
    }

    void PlayBounceSound(float impactVelocity)
    {
        bounceAudio.volume = 0.1f;
        bounceAudio.pitch = Random.Range(0.6f, 1.1f);
        bounceAudio.PlayOneShot(bounceSoundClip);
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

    public void DisableTrail()
    {
        TrailRenderer trail = GetComponentInChildren<TrailRenderer>();
        if (trail != null)
        {
            trail.enabled = false;
        }
    }

    public void EnableTrail()
    {
        TrailRenderer trail = GetComponentInChildren<TrailRenderer>();
        if (trail != null)
        {
            trail.enabled = true;
        }
    }
}