using UnityEngine;

/// <summary>
/// Controls which golf ball can collide with this obstacle.
/// Also lets us test whether a given ball position is blocked by this obstacle.
/// </summary>
public class ObstacleBallCollision2D : MonoBehaviour
{
    public enum ObstacleColor
    {
        Blue,
        Red
    }

    [Header("Runtime")]
    [SerializeField] private ObstacleColor obstacleColor;

    private GolfBallController blueBall;
    private GolfBallController redBall;
    private Collider2D[] obstacleColliders;

    public void Setup(ObstacleColor color, GolfBallController blue, GolfBallController red)
    {
        obstacleColor = color;
        blueBall = blue;
        redBall = red;

        obstacleColliders = GetComponentsInChildren<Collider2D>(true);
        ApplyCollisionRules();
    }

    void ApplyCollisionRules()
    {
        if (obstacleColliders == null || obstacleColliders.Length == 0) return;
        if (blueBall == null || redBall == null) return;

        Collider2D[] blueCols = blueBall.GetComponentsInChildren<Collider2D>(true);
        Collider2D[] redCols = redBall.GetComponentsInChildren<Collider2D>(true);

        SetIgnore(blueCols, false);
        SetIgnore(redCols, false);

        if (obstacleColor == ObstacleColor.Blue)
        {
            // Red ball passes through blue obstacle
            SetIgnore(redCols, true);
        }
        else
        {
            // Blue ball passes through red obstacle
            SetIgnore(blueCols, true);
        }
    }

    void SetIgnore(Collider2D[] ballColliders, bool ignore)
    {
        if (ballColliders == null) return;

        for (int i = 0; i < obstacleColliders.Length; i++)
        {
            Collider2D obstacleCol = obstacleColliders[i];
            if (obstacleCol == null) continue;

            for (int j = 0; j < ballColliders.Length; j++)
            {
                Collider2D ballCol = ballColliders[j];
                if (ballCol == null) continue;

                Physics2D.IgnoreCollision(obstacleCol, ballCol, ignore);
            }
        }
    }

    GolfBallController GetBlockingBall()
    {
        return obstacleColor == ObstacleColor.Blue ? blueBall : redBall;
    }

    // NEW:
    // Returns true if this test position is too close to the obstacle
    // for the blocking ball.
    public bool IsPositionBlockedForBlockingBall(Vector3 testPos, float extraClearance)
    {
        GolfBallController blockingBall = GetBlockingBall();
        if (blockingBall == null) return false;

        Collider2D[] ballCols = blockingBall.GetComponentsInChildren<Collider2D>(true);
        if (ballCols == null || ballCols.Length == 0) return false;

        Rigidbody2D rb = blockingBall.GetRigidbody();

        Vector3 originalTransformPos = blockingBall.transform.position;
        Vector2 originalRbPos = rb != null ? rb.position : (Vector2)originalTransformPos;

        if (rb != null)
        {
            rb.position = testPos;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        blockingBall.transform.position = testPos;
        Physics2D.SyncTransforms();

        bool blocked = IsBallTooClose(ballCols, extraClearance);

        if (rb != null)
        {
            rb.position = originalRbPos;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        blockingBall.transform.position = originalTransformPos;
        Physics2D.SyncTransforms();

        return blocked;
    }

    bool IsBallTooClose(Collider2D[] ballCols, float extraClearance)
    {
        if (obstacleColliders == null || obstacleColliders.Length == 0) return false;

        for (int i = 0; i < obstacleColliders.Length; i++)
        {
            Collider2D obstacleCol = obstacleColliders[i];
            if (obstacleCol == null) continue;

            for (int j = 0; j < ballCols.Length; j++)
            {
                Collider2D ballCol = ballCols[j];
                if (ballCol == null) continue;

                ColliderDistance2D dist = obstacleCol.Distance(ballCol);

                // distance < 0 = overlapping
                // distance near 0 = touching / almost touching
                if (dist.isOverlapped || dist.distance <= extraClearance)
                    return true;
            }
        }

        return false;
    }
}