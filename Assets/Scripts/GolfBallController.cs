using UnityEngine;
using Unity.Cinemachine;

public class GolfBallController : MonoBehaviour
{
    [Header("Stop Detection")]
    [SerializeField] private float stoppedVelocityThreshold = 0.1f;
    [SerializeField] private float stoppedCheckDuration = 0.5f;

    [Header("Teleport Settings")]
    [SerializeField] private Vector3 playerOffsetFromBall = new Vector3(-2f, 0f, 0f);

    private Rigidbody2D rb;
    private Transform player;
    private float timeStationary = 0f;
    private bool hasTeleported = false;
    private bool ballWasHit = false;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        player = playerObj.transform;
    }

    void FixedUpdate()
    {
        if (player == null) return;

        if (rb.linearVelocity.magnitude > stoppedVelocityThreshold)
        {
            timeStationary = 0f;
            ballWasHit = true;
        }
        else if (ballWasHit && !hasTeleported)
        {
            rb.linearVelocity = Vector3.zero;
            timeStationary += Time.fixedDeltaTime;

            if (timeStationary >= stoppedCheckDuration)
            {
                TeleportToPosition();
                hasTeleported = true;
            }
        }
    }

    void TeleportToPosition()
    {
        Vector3 ballPosition = transform.position;

        player.position = ballPosition + playerOffsetFromBall;

        Invoke(nameof(ResetTeleportFlag), 0.1f);
    }

    void ResetTeleportFlag()
    {
        hasTeleported = false;
        ballWasHit = false;
        timeStationary = 0f;
    }
}