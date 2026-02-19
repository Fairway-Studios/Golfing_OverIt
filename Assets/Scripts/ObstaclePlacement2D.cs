using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns obstacle prefabs along a generated 2D terrain surface.
/// - Uses a local System.Random so it does NOT mess with UnityEngine.Random state.
/// - Spawns on the surface Y at random X positions, with spacing / edge padding / spawn avoidance.
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

    public LayerMask overlapMask = ~0; // everything by default

    [Header("Parenting")]
    [Tooltip("Obstacles will be parented here (auto-created if null).")]
    public Transform obstaclesRoot;

    readonly List<GameObject> _spawned = new();

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

                // local -> world
                Vector3 worldPos = generatorTransform.TransformPoint(new Vector3(x, yLocal + yOffset, 0f));

                // avoid spawn points
                if (Vector2.Distance(worldPos, p1) < avoidSpawnPointsRadius) continue;
                if (Vector2.Distance(worldPos, p2) < avoidSpawnPointsRadius) continue;
                if (Vector2.Distance(worldPos, b1) < avoidSpawnPointsRadius) continue;
                if (Vector2.Distance(worldPos, b2) < avoidSpawnPointsRadius) continue;

                // spacing vs other obstacles
                if (IsTooClose(worldPos, minSpacing)) continue;

                // overlap check (optional)
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
                _spawned.Add(obj);

                placed = true;
                break;
            }

            // if not placed, just skip
            if (!placed) { }
        }
    }

    public void Clear()
    {
        // destroy spawned obstacles
        for (int i = _spawned.Count - 1; i >= 0; i--)
        {
            var go = _spawned[i];
            if (!go) continue;

            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }
        _spawned.Clear();

        // also destroy the root so we don't keep parenting to a "pending destroy" object
        if (obstaclesRoot != null)
        {
            var rootGO = obstaclesRoot.gameObject;
            obstaclesRoot = null; // <- key: reset reference immediately

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

        // Create a child root under the generator so everything stays organized
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

    // ---------------- surface sampling (LOCAL space) ----------------

    float SampleSurfaceY_Local(List<Vector3> surface, float x)
    {
        bool found = false;
        float bestY = float.NegativeInfinity;

        // Scan ALL forward-x segments and take the highest Y at this X
        for (int i = 1; i < surface.Count; i++)
        {
            Vector3 a = surface[i - 1];
            Vector3 b = surface[i];

            float dx = b.x - a.x;
            if (dx <= 0.0001f) continue; // skip vertical stair segments

            // segment covers x?
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

        // fallback: nearest point
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
