using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor; // for EditorApplication.delayCall
#endif

/// <summary>
/// 2D Perlin-based mountain outline generator.
/// - Grey closed LineRenderer for mountain outline (optional PolygonCollider2D)
/// - Handles ground alignment to start of first mountain.
/// - Can spawn player & ball at spawn points on the ground.
/// </summary>
[ExecuteAlways]
public class PerlinMountain2D : MonoBehaviour
{
    [Header("Random / Seed")]
    public bool useRandomSeed = true;
    public int seed = 12345;

    [Header("Mountains")]
    [Min(1)] public int mountainCount = 1;
    public float mountainWidth = 60f;
    [Min(0)] public float mountainSpacing = 12f;
    public float baseY = -6f;
    public float amplitude = 12f;
    public float frequency = 0.06f;
    public float stepX = 0.5f;

    [Header("Shape controls")]
    [Tooltip("Clamp all terrain so it never dips below baseY - margin.")]
    public bool clampToBase = true;
    public float baseMargin = 0f;

    [Tooltip("Snap heights to a grid to get a 'steppy' look.")]
    public bool quantizeHeights = false;
    [Min(0.01f)] public float heightStep = 0.5f;

    [Tooltip("If true, draw 'stairs' (horizontal then vertical) instead of diagonals between samples.")]
    public bool useStairSteps = true;

    [Header("Outline look")]
    public float outlineWidth = 0.12f;
    public Color mountainColor = new Color(0.65f, 0.68f, 0.72f);
    public bool addPolygonCollider2D = true;

    // ---------- GROUND HOOKUP ----------
    [Header("Ground hookup")]
    [Tooltip("Root transform of your ground prefab (children will follow this).")]
    public Transform groundRoot;

    [Tooltip("Extra offset added to the matched height (world Y).")]
    public float groundYOffset = 0f;

    [Tooltip("If true, also scale ground height to match the vertical wall height.")]
    public bool scaleGroundHeight = false;
    // -----------------------------------

    // ---------- SPAWNING ----------
    [Header("Spawners")]
    [Tooltip("Empty child on Ground where the player should spawn.")]
    public Transform playerSpawnPoint;
    [Tooltip("Empty child on Ground where the golf ball should spawn.")]
    public Transform ballSpawnPoint;

    [Header("Prefabs")]
    public GameObject playerPrefab;
    public GameObject ballPrefab;
    // -------------------------------

    [Header("Editor")]
    [Tooltip("If on, the generator will refresh when you tweak values. Uses a deferred call to avoid warnings.")]
    public bool autoRegenerateOnValidate = true;

    readonly List<GameObject> _generated = new(); // mountains
    readonly List<GameObject> _spawned = new();   // player/ball

    [ContextMenu("Generate Now")]
    public void GenerateNow()
    {
        Regenerate();
    }

    void Regenerate()
    {
        if (useRandomSeed)
            seed = Random.Range(int.MinValue / 2, int.MaxValue / 2);
        Random.InitState(seed);

        ClearGenerated();

        float cursorX = 0f;
        for (int m = 0; m < mountainCount; m++)
        {
            var mountainGO = new GameObject($"Mountain_{m}");
            mountainGO.transform.SetParent(transform, false);

            var surface = BuildSurfacePoints(
                cursorX,
                baseY,
                mountainWidth,
                stepX,
                amplitude,
                frequency,
                seed + 100 * m
            );

            var polygon = BuildClosedPolygon(surface, baseY);

            var lr = mountainGO.AddComponent<LineRenderer>();
            lr.loop = true;
            lr.positionCount = polygon.Count;
            lr.useWorldSpace = false;
            lr.widthMultiplier = outlineWidth;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = mountainColor;
            lr.endColor = mountainColor;
            lr.SetPositions(polygon.ToArray());

            if (addPolygonCollider2D)
            {
                var col = mountainGO.AddComponent<PolygonCollider2D>();
                var v2 = new Vector2[polygon.Count];
                for (int i = 0; i < polygon.Count; i++) v2[i] = (Vector2)polygon[i];
                col.pathCount = 1;
                col.SetPath(0, v2);
            }

            // First mountain: align ground + spawn gameplay objects
            if (m == 0)
            {
                AlignGroundToMountainStart(surface);
                SpawnGameplayObjects();
            }

            _generated.Add(mountainGO);
            cursorX += mountainWidth + mountainSpacing;
        }
    }

    // ----------------- HELPERS -----------------
    void ClearGenerated()
    {
        // destroy mountains
        for (int i = _generated.Count - 1; i >= 0; i--)
        {
            var go = _generated[i];
            if (!go) continue;

            if (Application.isPlaying)
                Object.Destroy(go);
            else
                Object.DestroyImmediate(go);
        }
        _generated.Clear();

        // destroy previously spawned player/ball (if any)
        for (int i = _spawned.Count - 1; i >= 0; i--)
        {
            var go = _spawned[i];
            if (!go) continue;

            if (Application.isPlaying)
                Object.Destroy(go);
            else
                Object.DestroyImmediate(go);
        }
        _spawned.Clear();

        // Also clear any leftover children under this generator (safety)
        var toDestroy = new List<Transform>();
        foreach (Transform child in transform) toDestroy.Add(child);
        foreach (var t in toDestroy)
        {
            if (Application.isPlaying) Object.Destroy(t.gameObject);
            else Object.DestroyImmediate(t.gameObject);
        }
    }

    List<Vector3> BuildSurfacePoints(
        float startX,
        float groundY,
        float width,
        float step,
        float amp,
        float freq,
        int seedLocal
    )
    {
        var pts = new List<Vector3>();
        int steps = Mathf.Max(2, Mathf.CeilToInt(width / Mathf.Max(0.01f, step)));
        float noiseOffset = seedLocal * 0.00137f;

        float minYAllowed = groundY - Mathf.Abs(baseMargin);

        bool initialized = false;
        float prevX = startX;
        float prevY = groundY;

        for (int i = 0; i <= steps; i++)
        {
            float x = startX + i * step;
            float n = Mathf.PerlinNoise((x * freq) + noiseOffset, noiseOffset);
            float y = groundY + n * amp;

            if (clampToBase)
                y = Mathf.Max(y, minYAllowed);

            if (quantizeHeights && heightStep > 0.0001f)
                y = Mathf.Round(y / heightStep) * heightStep;

            if (!initialized)
            {
                pts.Add(new Vector3(x, y, 0f));
                initialized = true;
                prevX = x;
                prevY = y;
                continue;
            }

            if (useStairSteps)
            {
                // horizontal segment at previous height
                pts.Add(new Vector3(x, prevY, 0f));
                // vertical step to new height
                pts.Add(new Vector3(x, y, 0f));
            }
            else
            {
                // direct diagonal
                pts.Add(new Vector3(x, y, 0f));
            }

            prevX = x;
            prevY = y;
        }

        return pts;
    }

    List<Vector3> BuildClosedPolygon(List<Vector3> surface, float groundY)
    {
        var poly = new List<Vector3>(surface.Count + 3);
        poly.AddRange(surface);

        var last = surface[surface.Count - 1];
        poly.Add(new Vector3(last.x, groundY, 0f));
        var first = surface[0];
        poly.Add(new Vector3(first.x, groundY, 0f));

        return poly;
    }

    void AlignGroundToMountainStart(List<Vector3> surface)
    {
        if (groundRoot == null || surface == null || surface.Count == 0)
            return;

        Vector3 localTop = surface[0];
        Vector3 worldTop = transform.TransformPoint(localTop);

        Vector3 pos = groundRoot.position;
        pos.y = worldTop.y + groundYOffset;
        groundRoot.position = pos;

        if (scaleGroundHeight)
        {
            Vector3 localBase = new Vector3(localTop.x, baseY, 0f);
            Vector3 worldBase = transform.TransformPoint(localBase);
            float wallHeightWorld = worldTop.y - worldBase.y;

            Vector3 scale = groundRoot.localScale;
            scale.y = wallHeightWorld;
            groundRoot.localScale = scale;
        }
    }

    // ---------- SPAWN PLAYER & BALL ----------
    void SpawnGameplayObjects()
    {
        // Only spawn while the game is running
        if (!Application.isPlaying)
            return;

        if (playerSpawnPoint != null && playerPrefab != null)
        {
            var player = Object.Instantiate(
                playerPrefab,
                playerSpawnPoint.position,
                playerSpawnPoint.rotation
            );
            _spawned.Add(player);
        }

        if (ballSpawnPoint != null && ballPrefab != null)
        {
            var ball = Object.Instantiate(
                ballPrefab,
                ballSpawnPoint.position,
                ballSpawnPoint.rotation
            );
            _spawned.Add(ball);
        }
    }
    // ----------------------------------------

    void OnEnable()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
            EditorApplication.delayCall += () => { if (this) GenerateNow(); };
#endif
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!autoRegenerateOnValidate) return;
        EditorApplication.delayCall += () =>
        {
            if (this) GenerateNow();
        };
    }
#endif
}
