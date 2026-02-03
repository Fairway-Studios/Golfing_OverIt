using System.Collections.Generic;
using UnityEngine;
using LibTessDotNet;

#if UNITY_EDITOR
using UnityEditor;
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

    [Header("Fill")]
    public bool fillMountain = true;
    public Color fillColor = new Color(0.25f, 0.28f, 0.33f, 1f);
    public int fillSortingOrder = -5; // behind outline

    [Header("Ground hookup")]
    [Tooltip("Root transform of your ground prefab (children will follow this).")]
    public Transform groundRoot;

    [Tooltip("Extra offset added to the matched height (world Y).")]
    public float groundYOffset = 0f;

    [Tooltip("If true, also scale ground height to match the vertical wall height.")]
    public bool scaleGroundHeight = false;

    [Header("Spawners")]
    [Tooltip("Empty child on Ground where the player should spawn.")]
    public Transform playerOneSpawnPoint;
    [Tooltip("Empty child on Ground where the golf ball should spawn.")]
    public Transform ballOneSpawnPoint;
    [Tooltip("Empty child on Ground where the player should spawn.")]
    public Transform playerTwoSpawnPoint;
    [Tooltip("Empty child on Ground where the golf ball should spawn.")]
    public Transform ballTwoSpawnPoint;

    [Header("Prefabs")]
    public GameObject playerOnePrefab;
    public GameObject ballOnePrefab;

    public GameObject playerTwoPrefab;
    public GameObject ballTwoPrefab;

    [Header("Editor")]
    [Tooltip("If on, the generator will refresh when you tweak values in the inspector.")]
    public bool autoRegenerateOnValidate = true;

    readonly List<GameObject> _generated = new(); // mountains

    [Header("Caves")]
    public CaveGenerator caveGenerator;

    // -------- LibTess settings --------
    [Header("LibTess Fill Settings")]
    [Tooltip("How much to scale positions before tessellating (avoids floating precision issues).")]
    public float tessScale = 1000f;

    [Tooltip("If your fill gets inverted, toggle this.")]
    public bool tessInvertWinding = false;

#if UNITY_EDITOR
    bool _prefabChecked;
    bool _isPrefabAsset;

    bool IsPrefabAsset()
    {
        if (!_prefabChecked)
        {
            _isPrefabAsset = !gameObject.scene.IsValid(); // true when editing the asset itself
            _prefabChecked = true;
        }
        return _isPrefabAsset;
    }
#endif

    [ContextMenu("Generate Now")]
    public void GenerateNow()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying && IsPrefabAsset()) return;
#endif
        Regenerate();
    }

    void Regenerate()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying && IsPrefabAsset()) return;
#endif

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

            if (caveGenerator != null)
            {
                caveGenerator.ApplyFakeCaves(surface, baseY, cursorX, mountainWidth, seed + 100 * m);
            }

            var polygon = BuildClosedPolygon(surface, baseY);

            if (fillMountain)
            {
                AddFillMesh_LibTess(mountainGO, polygon, fillColor, fillSortingOrder);
            }

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

            if (m == 0)
            {
                AlignGroundToMountainStart(surface);
                SpawnGameplayObjects();
            }

            _generated.Add(mountainGO);
            cursorX += mountainWidth + mountainSpacing;
        }
    }

    void ClearGenerated()
    {
        for (int i = _generated.Count - 1; i >= 0; i--)
        {
            var go = _generated[i];
            if (!go) continue;

            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }
        _generated.Clear();

        var toDestroy = new List<Transform>();
        foreach (Transform child in transform) toDestroy.Add(child);

        foreach (var t in toDestroy)
        {
            if (Application.isPlaying) Destroy(t.gameObject);
            else DestroyImmediate(t.gameObject);
        }
    }

    List<Vector3> BuildSurfacePoints(float startX, float groundY, float width, float step, float amp, float freq, int seedLocal)
    {
        var pts = new List<Vector3>();
        int steps = Mathf.Max(2, Mathf.CeilToInt(width / Mathf.Max(0.01f, step)));
        float noiseOffset = seedLocal * 0.00137f;

        float minYAllowed = groundY - Mathf.Abs(baseMargin);

        bool initialized = false;
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
                prevY = y;
                continue;
            }

            if (useStairSteps)
            {
                pts.Add(new Vector3(x, prevY, 0f));
                pts.Add(new Vector3(x, y, 0f));
            }
            else
            {
                pts.Add(new Vector3(x, y, 0f));
            }

            prevY = y;
        }

        return pts;
    }

    List<Vector3> BuildClosedPolygon(List<Vector3> surface, float groundY)
    {
        var poly = new List<Vector3>(surface.Count + 4);
        poly.AddRange(surface);

        var last = surface[surface.Count - 1];
        var first = surface[0];

        poly.Add(new Vector3(last.x, groundY, 0f));
        poly.Add(new Vector3(first.x, groundY, 0f));

        return poly;
    }

    void AlignGroundToMountainStart(List<Vector3> surface)
    {
        if (groundRoot == null || surface == null || surface.Count == 0)
            return;

        Vector3 localTop = surface[0];
        Vector3 worldTop = transform.TransformPoint(localTop);
        Vector3 localBase = new Vector3(localTop.x, baseY, 0f);
        Vector3 worldBase = transform.TransformPoint(localBase);

        Vector3 pos = groundRoot.position;
        pos.y = worldTop.y + groundYOffset;
        groundRoot.position = pos;

        if (scaleGroundHeight)
        {
            float wallHeightWorld = worldTop.y - worldBase.y;
            Vector3 scale = groundRoot.localScale;
            scale.y = wallHeightWorld;
            groundRoot.localScale = scale;
        }
    }

    void SpawnGameplayObjects()
    {
        if (playerOneSpawnPoint != null && playerOnePrefab != null)
        {
            Transform t = playerOnePrefab.transform;
            t.position = playerOneSpawnPoint.position;
            t.rotation = playerOneSpawnPoint.rotation;
        }

        if (ballOneSpawnPoint != null && ballOnePrefab != null)
        {
            Transform t = ballOnePrefab.transform;
            t.position = ballOneSpawnPoint.position;
            t.rotation = ballOneSpawnPoint.rotation;
        }

        if (playerTwoSpawnPoint != null && playerTwoPrefab != null)
        {
            Transform t = playerTwoPrefab.transform;
            t.position = playerTwoSpawnPoint.position;
            t.rotation = playerTwoSpawnPoint.rotation;
        }

        if (ballTwoSpawnPoint != null && ballTwoPrefab != null)
        {
            Transform t = ballTwoPrefab.transform;
            t.position = ballTwoSpawnPoint.position;
            t.rotation = ballTwoSpawnPoint.rotation;
        }
    }

    void OnEnable()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying && IsPrefabAsset())
            return;

        if (!Application.isPlaying)
        {
            EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                if (IsPrefabAsset()) return;
                GenerateNow();
            };
        }
#endif
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!autoRegenerateOnValidate) return;
        if (Application.isPlaying) return;
        if (IsPrefabAsset()) return;

        EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            if (IsPrefabAsset()) return;
            GenerateNow();
        };
    }
#endif

    // ------------------ LibTess Fill ------------------
    void AddFillMesh_LibTess(GameObject mountainGO, List<Vector3> polygon, Color color, int sortingOrder)
    {
        var mf = mountainGO.AddComponent<MeshFilter>();
        var mr = mountainGO.AddComponent<MeshRenderer>();

        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = color;
        mr.sharedMaterial = mat;
        mr.sortingOrder = sortingOrder;

        // Work on a local copy so we never mutate your outline/collider polygon.
        var work = new List<Vector3>(polygon);
        RemoveNearDuplicates(polygon, 0.0001f);
        RemoveCollinear(polygon, 0.0001f);
        RemoveBacktrackingSpikes(polygon); // ✅ ADD THIS


        if (work.Count < 3)
            return;

        // LibTess works best if we scale up coordinates a bit (reduces precision issues).
        float s = Mathf.Max(1f, tessScale);

        var tess = new Tess();

        // Add one contour (your polygon). LibTess can handle weird cases much better than ear clipping.
        // We tessellate on X/Y (Z not used).
        var contour = new ContourVertex[work.Count];
        for (int i = 0; i < work.Count; i++)
        {
            var p = work[i];
            contour[i].Position = new Vec3(p.x * s, p.y * s, 0);
            contour[i].Data = i; // optional
        }

        // Winding: your polygon is typically CCW, but caves can flip it.
        // If you see inverted fill, toggle tessInvertWinding in inspector.
        var winding = tessInvertWinding ? ContourOrientation.Clockwise : ContourOrientation.CounterClockwise;
        tess.AddContour(contour, winding);

        // Tessellate into triangles
        // WindingRule.EvenOdd is usually safest for funky self-touching/looping shapes.
        tess.Tessellate(WindingRule.EvenOdd, ElementType.Polygons, 3);

        if (tess.ElementCount <= 0 || tess.Vertices == null || tess.Vertices.Length < 3)
        {
            Debug.LogWarning($"LibTess failed to tessellate {mountainGO.name} (points={work.Count}).");
            return;
        }

        // Convert tess vertices back to Unity verts
        var verts = new Vector3[tess.Vertices.Length];
        for (int i = 0; i < verts.Length; i++)
        {
            var v = tess.Vertices[i].Position;
            verts[i] = new Vector3((float)(v.X / s), (float)(v.Y / s), 0f);
        }

        // Elements are indices (triangles)
        // Each element is 3 indices because we requested polygons of size 3
        int triCount = tess.ElementCount * 3;
        var tris = new int[triCount];

        int k = 0;
        for (int e = 0; e < tess.ElementCount; e++)
        {
            int i0 = tess.Elements[e * 3 + 0];
            int i1 = tess.Elements[e * 3 + 1];
            int i2 = tess.Elements[e * 3 + 2];

            // LibTess can output -1 for unused vertices in some modes,
            // but for ElementType.Polygons with size 3, it should be valid.
            if (i0 < 0 || i1 < 0 || i2 < 0)
                continue;

            tris[k++] = i0;
            tris[k++] = i1;
            tris[k++] = i2;
        }

        if (k < 3)
        {
            Debug.LogWarning($"LibTess produced no valid triangles for {mountainGO.name}.");
            return;
        }

        if (k != tris.Length)
        {
            // shrink array if we skipped any invalid entries
            var trimmed = new int[k];
            for (int i = 0; i < k; i++) trimmed[i] = tris[i];
            tris = trimmed;
        }

        var mesh = new UnityEngine.Mesh();
        mesh.name = "MountainFill_LibTess";
        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();

        mf.sharedMesh = mesh;
    }

    // ------------------ cleanup helpers ------------------

    static void RemoveBacktrackingSpikes(List<Vector3> pts, float eps = 0.0001f)
    {
        if (pts.Count < 4) return;

        for (int i = pts.Count - 1; i >= 2; i--)
        {
            Vector2 a = pts[i - 2];
            Vector2 b = pts[i - 1];
            Vector2 c = pts[i];

            Vector2 ab = (b - a).normalized;
            Vector2 bc = (c - b).normalized;

            // If directions are almost opposite → spike
            if (Vector2.Dot(ab, bc) < -0.999f)
            {
                pts.RemoveAt(i - 1);
            }
        }
    }


    static void RemoveNearDuplicates(List<Vector3> pts, float eps = 0.0001f)
    {
        for (int i = pts.Count - 2; i >= 0; i--)
        {
            if ((pts[i + 1] - pts[i]).sqrMagnitude <= eps * eps)
                pts.RemoveAt(i + 1);
        }

        if (pts.Count > 2 && (pts[0] - pts[pts.Count - 1]).sqrMagnitude <= eps * eps)
            pts.RemoveAt(pts.Count - 1);
    }

    static void RemoveCollinear(List<Vector3> pts, float eps = 0.0001f)
    {
        if (pts.Count < 3) return;

        int guard = 0;
        while (pts.Count >= 3 && guard++ < 5000)
        {
            bool removedAny = false;

            for (int i = 0; i < pts.Count; i++)
            {
                Vector2 a = pts[(i - 1 + pts.Count) % pts.Count];
                Vector2 b = pts[i];
                Vector2 c = pts[(i + 1) % pts.Count];

                Vector2 ab = b - a;
                Vector2 bc = c - b;

                float cross = ab.x * bc.y - ab.y * bc.x;

                if (Mathf.Abs(cross) <= eps)
                {
                    pts.RemoveAt(i);
                    removedAny = true;
                    break;
                }
            }

            if (!removedAny) break;
        }
    }
}
