using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class GolfBallHit : MonoBehaviour
{
    public float powerMultiplier;

    private Rigidbody2D rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (!collision.collider.CompareTag("Club"))
            return;

        Rigidbody2D clubRb = collision.collider.attachedRigidbody;
        if (clubRb == null)
            return;

        Vector2 hitDir = (rb.position - clubRb.position).normalized;


        float clubSpeed = clubRb.linearVelocity.magnitude;
        float hitStrength = clubSpeed * powerMultiplier;

        rb.AddForce(hitDir * hitStrength, ForceMode2D.Impulse);
    }
}
