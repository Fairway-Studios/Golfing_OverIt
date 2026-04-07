using UnityEngine;

/// <summary>
/// For manually placed fixed multiplayer obstacles.
/// Attach this directly to the fixed obstacle instead of ObstacleBallCollision2D.
/// 
/// Blue obstacle:
/// - blocks blue ball
/// - red ball passes through
/// 
/// Red obstacle:
/// - blocks red ball
/// - blue ball passes through
/// </summary>
public class FixedObstacleBallCollision2D : MonoBehaviour
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

    private void Start()
    {
        obstacleColliders = GetComponentsInChildren<Collider2D>(true);
        FindBallsByOwnerIndex();
        ApplyCollisionRules();
    }

    void FindBallsByOwnerIndex()
    {
        blueBall = null;
        redBall = null;

        GolfBallController[] balls = FindObjectsByType<GolfBallController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < balls.Length; i++)
        {
            GolfBallController ball = balls[i];
            if (ball == null)
                continue;

            if (ball.GetOwnerIndex() == 0)
                blueBall = ball;
            else if (ball.GetOwnerIndex() == 1)
                redBall = ball;
        }
    }

    void ApplyCollisionRules()
    {
        if (obstacleColliders == null || obstacleColliders.Length == 0)
            return;

        if (blueBall == null || redBall == null)
            return;

        Collider2D[] redCols = blueBall.GetComponentsInChildren<Collider2D>(true);
        Collider2D[] blueCols = redBall.GetComponentsInChildren<Collider2D>(true);

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
        if (ballColliders == null)
            return;

        for (int i = 0; i < obstacleColliders.Length; i++)
        {
            Collider2D obstacleCol = obstacleColliders[i];
            if (obstacleCol == null)
                continue;

            for (int j = 0; j < ballColliders.Length; j++)
            {
                Collider2D ballCol = ballColliders[j];
                if (ballCol == null)
                    continue;

                Physics2D.IgnoreCollision(obstacleCol, ballCol, ignore);
            }
        }
    }
}