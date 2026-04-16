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
            SetIgnore(redCols, true);
        }
        else
        {
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

    /// <summary>
    /// Returns the radius of the blocking ball's largest collider.
    /// Cached after first call for performance.
    /// </summary>
    private float _cachedBallRadius = -1f;

    float GetBlockingBallRadius()
    {
        if (_cachedBallRadius >= 0f) return _cachedBallRadius;

        GolfBallController blockingBall = GetBlockingBall();
        if (blockingBall == null) { _cachedBallRadius = 0f; return 0f; }

        Collider2D[] ballCols = blockingBall.GetComponentsInChildren<Collider2D>(true);
        float maxRadius = 0f;

        for (int j = 0; j < ballCols.Length; j++)
        {
            if (ballCols[j] == null) continue;

            // For CircleCollider2D, use radius directly. Otherwise use bounds extents.
            CircleCollider2D circle = ballCols[j] as CircleCollider2D;
            float r;
            if (circle != null)
            {
                r = circle.radius * Mathf.Max(
                    blockingBall.transform.lossyScale.x,
                    blockingBall.transform.lossyScale.y);
            }
            else
            {
                r = Mathf.Max(ballCols[j].bounds.extents.x, ballCols[j].bounds.extents.y);
            }

            if (r > maxRadius) maxRadius = r;
        }

        _cachedBallRadius = maxRadius;
        return maxRadius;
    }

    /// <summary>
    /// Returns true if placing a ball at testPos would overlap or be too close to this obstacle.
    /// Does NOT move any physics objects — purely geometric check.
    /// </summary>
    public bool IsPositionBlockedForBlockingBall(Vector3 testPos, float extraClearance)
    {
        GolfBallController blockingBall = GetBlockingBall();
        if (blockingBall == null) return false;
        if (obstacleColliders == null || obstacleColliders.Length == 0) return false;

        float ballRadius = GetBlockingBallRadius();
        float totalClearance = ballRadius + extraClearance;

        Vector2 testPos2D = new Vector2(testPos.x, testPos.y);

        for (int i = 0; i < obstacleColliders.Length; i++)
        {
            Collider2D obstacleCol = obstacleColliders[i];
            if (obstacleCol == null || !obstacleCol.enabled) continue;

            // 1. Check if the point is inside the collider
            if (obstacleCol.OverlapPoint(testPos2D))
                return true;

            // 2. Check if ball edge would overlap (point is outside but ball radius reaches in)
            Vector2 closestPoint = obstacleCol.ClosestPoint(testPos2D);
            float dist = Vector2.Distance(closestPoint, testPos2D);

            if (dist <= totalClearance)
                return true;
        }

        return false;
    }

    // Keep the old method around in case anything else references it,
    // but it just delegates to the new approach.
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

                if (dist.isOverlapped || dist.distance <= extraClearance)
                    return true;
            }
        }

        return false;
    }


}