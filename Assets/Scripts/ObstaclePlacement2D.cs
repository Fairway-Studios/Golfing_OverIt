using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Spawns obstacle prefabs along a generated 2D terrain surface.
/// - Uses a local System.Random so it does NOT mess with UnityEngine.Random state.
/// - Spawns on the surface Y at random X positions, with spacing / edge padding / spawn avoidance.
/// - Randomly assigns red or blue anaglyph settings to spawned obstacles.
/// - Makes opposite player's ball pass through.
/// </summary>
public class ObstaclePlacement2D : MonoBehaviour
{
    [Header("Enable")]
    public bool spawnObstacles = true;

    [Header("Prefabs")]
    [Tooltip("Randomly pick from these prefabs when spawning obstacles.")]
    public List<GameObject> obstaclePrefabs = new List<GameObject>();

    [Header("Counts")]
    [Min(0)] public int obstaclesPerMountain = 12;

    [Header("Placement")]
    [Tooltip("Don't spawn too close to the start/end of each mountain.")]
    public float edgePaddingX = 3f;

    [Tooltip("Lift obstacle slightly above the surface so it doesn't clip.")]
    public float yOffset = 0.15f;

    [Tooltip("Minimum spacing between spawned obstacles (world units).")]
    public float minSpacing = 1.25f;

    [Tooltip("Try this many random positions before giving up on each obstacle.")]
    [Range(1, 200)] public int maxAttemptsPerObstacle = 40;

    [Tooltip("If true, rotate obstacle to match the surface slope.")]
    public bool alignToSlope = false;

    [Header("Avoid Spawn Points")]
    [Tooltip("Avoid spawning obstacles near players/balls (world units).")]
    public float avoidSpawnPointsRadius = 3.5f;

    [Header("Overlap Safety")]
    [Tooltip("Optional: if your obstacles have colliders, this helps avoid overlaps.")]
    public bool useOverlapCheck = true;

    [Tooltip("Radius used for overlap checking (world units).")]
    public float overlapCheckRadius = 0.25f;

    public LayerMask overlapMask = ~0;

    [Header("Parenting")]
    [Tooltip("Obstacles will be parented here (auto-created if null).")]
    public Transform obstaclesRoot;

    [Header("Anaglyph Settings")]
    [Tooltip("Assign: Blue Anaglyph Settings (Anaglyph Color Settings)")]
    public AnaglyphColorSettings blueAnaglyphSettings;

    [Tooltip("Assign: Red Blue Anaglyph Settings (Anaglyph Color Settings)")]
    public AnaglyphColorSettings redAnaglyphSettings;

    [Header("Optional Layers")]
    [Tooltip("Obstacle layer name for blue obstacles.")]
    public string blueObstacleLayerName = "BlueLayer";

    [Tooltip("Obstacle layer name for red obstacles.")]
    public string redObstacleLayerName = "RedLayer";

    readonly List<GameObject> _spawned = new();
    readonly List<ObstacleBallCollision2D> _spawnedCollisionControllers = new();

    [Header("Teleport Safety")]
    [SerializeField] private float sharedSafeStepDistance = 0.2f;
    [SerializeField] private int sharedSafeMaxSteps = 24;
    [SerializeField] private float sharedSafeClearance = 0.08f;

    private GolfBallController blueBall;
    private GolfBallController redBall;

    /// <summary>
    /// Call this from your terrain generator once you have the mountain surface.
    /// surface points are in generator-local space.
    /// </summary>
    public void SpawnOnMountain(
        List<Vector3> surfaceLocal,
        float mountainStartX,
        float mountainWidth,
        int seed,
        Transform generatorTransform,
        Transform playerOneSpawn,
        Transform ballOneSpawn,
        Transform playerTwoSpawn,
        Transform ballTwoSpawn)
    {
        if (!spawnObstacles) return;
        if (obstaclePrefabs == null || obstaclePrefabs.Count == 0) return;
        if (surfaceLocal == null || surfaceLocal.Count < 2) return;
        if (obstaclesPerMountain <= 0) return;
        if (generatorTransform == null) return;

        EnsureRoot(generatorTransform);
        FindBallsByOwnerIndex();

        if (blueBall == null || redBall == null)
        {
            Debug.LogWarning("[ObstaclePlacement2D] Could not find both golf balls by owner index.");
            return;
        }

        float minX = mountainStartX + edgePaddingX;
        float maxX = mountainStartX + mountainWidth - edgePaddingX;
        if (maxX <= minX) return;

        var rng = new System.Random(seed);

        Vector3 p1 = playerOneSpawn ? playerOneSpawn.position : new Vector3(float.PositiveInfinity, 0, 0);
        Vector3 p2 = playerTwoSpawn ? playerTwoSpawn.position : new Vector3(float.PositiveInfinity, 0, 0);
        Vector3 b1 = ballOneSpawn ? ballOneSpawn.position : new Vector3(float.PositiveInfinity, 0, 0);
        Vector3 b2 = ballTwoSpawn ? ballTwoSpawn.position : new Vector3(float.PositiveInfinity, 0, 0);

        for (int i = 0; i < obstaclesPerMountain; i++)
        {
            bool placed = false;

            for (int attempt = 0; attempt < maxAttemptsPerObstacle; attempt++)
            {
                float x = Lerp(minX, maxX, (float)rng.NextDouble());

                float yLocal = SampleSurfaceY_Local(surfaceLocal, x);
                float angleDeg = alignToSlope ? SampleSurfaceAngleDeg_Local(surfaceLocal, x) : 0f;

                Vector3 worldPos = generatorTransform.TransformPoint(new Vector3(x, yLocal + yOffset, 0f));

                if (Vector2.Distance(worldPos, p1) < avoidSpawnPointsRadius) continue;
                if (Vector2.Distance(worldPos, p2) < avoidSpawnPointsRadius) continue;
                if (Vector2.Distance(worldPos, b1) < avoidSpawnPointsRadius) continue;
                if (Vector2.Distance(worldPos, b2) < avoidSpawnPointsRadius) continue;

                if (IsTooClose(worldPos, minSpacing)) continue;

                if (useOverlapCheck)
                {
                    if (Physics2D.OverlapCircle(worldPos, overlapCheckRadius, overlapMask) != null)
                        continue;
                }

                GameObject prefab = obstaclePrefabs[rng.Next(0, obstaclePrefabs.Count)];
                if (!prefab) continue;

                Quaternion rot = Quaternion.identity;
                if (alignToSlope) rot = Quaternion.Euler(0f, 0f, angleDeg);

                GameObject obj = Instantiate(prefab, worldPos, rot, obstaclesRoot);

                ObstacleBallCollision2D.ObstacleColor color = AssignRandomAnaglyphSettings(obj, rng);
                ApplyObstacleLayer(obj, color);
                SetupObstacleCollision(obj, color);

                _spawned.Add(obj);

                placed = true;
                break;
            }
        }
    }

    void FindBallsByOwnerIndex()
    {
        blueBall = null;
        redBall = null;

        GolfBallController[] balls = FindObjectsByType<GolfBallController>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < balls.Length; i++)
        {
            GolfBallController ball = balls[i];
            if (ball == null) continue;

            if (ball.GetOwnerIndex() == 0)
                blueBall = ball;
            else if (ball.GetOwnerIndex() == 1)
                redBall = ball;
        }
    }

    ObstacleBallCollision2D.ObstacleColor AssignRandomAnaglyphSettings(GameObject obj, System.Random rng)
    {
        if (obj == null) return ObstacleBallCollision2D.ObstacleColor.Blue;

        AnaglyphRenderingController controller = obj.GetComponentInChildren<AnaglyphRenderingController>(true);
        bool chooseBlue = rng.Next(0, 2) == 0;

        if (controller != null)
        {
            AnaglyphColorSettings chosenSettings = chooseBlue ? blueAnaglyphSettings : redAnaglyphSettings;

            if (chosenSettings != null)
            {
                FieldInfo colorSettingsField = typeof(AnaglyphRenderingController).GetField(
                    "colorSettings",
                    BindingFlags.Instance | BindingFlags.NonPublic
                );

                if (colorSettingsField != null)
                    colorSettingsField.SetValue(controller, chosenSettings);

                MethodInfo cacheRenderersMethod = typeof(AnaglyphRenderingController).GetMethod(
                    "CacheRenderers",
                    BindingFlags.Instance | BindingFlags.NonPublic
                );

                if (cacheRenderersMethod != null)
                    cacheRenderersMethod.Invoke(controller, null);

                controller.ApplyHSVAdjustment();
            }
        }

        return chooseBlue
            ? ObstacleBallCollision2D.ObstacleColor.Blue
            : ObstacleBallCollision2D.ObstacleColor.Red;
    }

    void ApplyObstacleLayer(GameObject obj, ObstacleBallCollision2D.ObstacleColor color)
    {
        string layerName = color == ObstacleBallCollision2D.ObstacleColor.Blue
            ? blueObstacleLayerName
            : redObstacleLayerName;

        int layer = LayerMask.NameToLayer(layerName);
        if (layer == -1) return;

        SetLayerRecursively(obj, layer);
    }

    void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;

        for (int i = 0; i < obj.transform.childCount; i++)
            SetLayerRecursively(obj.transform.GetChild(i).gameObject, layer);
    }

    void SetupObstacleCollision(GameObject obj, ObstacleBallCollision2D.ObstacleColor color)
    {
        if (obj == null || blueBall == null || redBall == null) return;

        ObstacleBallCollision2D controller = obj.GetComponent<ObstacleBallCollision2D>();
        if (controller == null)
            controller = obj.AddComponent<ObstacleBallCollision2D>();

        controller.Setup(color, blueBall, redBall);
        _spawnedCollisionControllers.Add(controller);
    }

    /// <summary>
    /// Call this after best-ball teleport / reposition.
    /// </summary>
    public Vector3 ResolveSharedSafeBallPosition(Vector3 desiredPos)
    {
        if (_spawnedCollisionControllers.Count == 0)
            return desiredPos;

        if (IsSharedBallPositionSafe(desiredPos))
            return desiredPos;

        // 1) Try left/right first so we do not place the ball on top of the obstacle edge
        for (int i = 1; i <= sharedSafeMaxSteps; i++)
        {
            float dist = sharedSafeStepDistance * i;

            Vector3 left = desiredPos + Vector3.left * dist;
            if (IsSharedBallPositionSafe(left))
                return left;

            Vector3 right = desiredPos + Vector3.right * dist;
            if (IsSharedBallPositionSafe(right))
                return right;
        }

        // 2) Then try upward
        for (int i = 1; i <= sharedSafeMaxSteps; i++)
        {
            Vector3 up = desiredPos + Vector3.up * (sharedSafeStepDistance * i);
            if (IsSharedBallPositionSafe(up))
                return up;
        }

        return desiredPos;
    }

    bool IsSharedBallPositionSafe(Vector3 testPos)
    {
        for (int i = 0; i < _spawnedCollisionControllers.Count; i++)
        {
            ObstacleBallCollision2D controller = _spawnedCollisionControllers[i];
            if (controller == null) continue;

            if (controller.IsPositionBlockedForBlockingBall(testPos, sharedSafeClearance))
                return false;
        }

        return true;
    }

    public void Clear()
    {
        for (int i = _spawned.Count - 1; i >= 0; i--)
        {
            var go = _spawned[i];
            if (!go) continue;

            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }

        _spawned.Clear();
        _spawnedCollisionControllers.Clear();

        if (obstaclesRoot != null)
        {
            var rootGO = obstaclesRoot.gameObject;
            obstaclesRoot = null;

            if (rootGO != null)
            {
                if (Application.isPlaying) Destroy(rootGO);
                else DestroyImmediate(rootGO);
            }
        }
    }

    void EnsureRoot(Transform generatorTransform)
    {
        if (obstaclesRoot != null) return;

        var rootGO = new GameObject("Obstacles_Root");
        rootGO.transform.SetParent(generatorTransform, false);
        obstaclesRoot = rootGO.transform;
    }

    bool IsTooClose(Vector3 worldPos, float minDist)
    {
        float minDistSqr = minDist * minDist;

        for (int i = 0; i < _spawned.Count; i++)
        {
            var go = _spawned[i];
            if (!go) continue;

            if ((go.transform.position - worldPos).sqrMagnitude < minDistSqr)
                return true;
        }

        return false;
    }

    static float Lerp(float a, float b, float t) => a + (b - a) * Mathf.Clamp01(t);

    float SampleSurfaceY_Local(List<Vector3> surface, float x)
    {
        bool found = false;
        float bestY = float.NegativeInfinity;

        for (int i = 1; i < surface.Count; i++)
        {
            Vector3 a = surface[i - 1];
            Vector3 b = surface[i];

            float dx = b.x - a.x;
            if (dx <= 0.0001f) continue;

            if (a.x <= x && x <= b.x)
            {
                float t = Mathf.InverseLerp(a.x, b.x, x);
                float y = Mathf.Lerp(a.y, b.y, t);

                if (!found || y > bestY)
                {
                    bestY = y;
                    found = true;
                }
            }
        }

        if (found)
            return bestY;

        bestY = surface[0].y;
        float bestD = Mathf.Abs(surface[0].x - x);

        for (int i = 1; i < surface.Count; i++)
        {
            float d = Mathf.Abs(surface[i].x - x);
            if (d < bestD)
            {
                bestD = d;
                bestY = surface[i].y;
            }
        }

        return bestY;
    }

    float SampleSurfaceAngleDeg_Local(List<Vector3> surface, float x)
    {
        for (int i = 1; i < surface.Count; i++)
        {
            Vector3 a = surface[i - 1];
            Vector3 b = surface[i];

            float dx = b.x - a.x;
            if (dx <= 0.0001f) continue;

            if (a.x <= x && x <= b.x)
            {
                float dy = b.y - a.y;
                return Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
            }
        }

        return 0f;
    }
}