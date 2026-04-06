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
    public int fillSortingOrder = -10; // behind outline

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

    [Header("Finish Flag")]
    [Tooltip("The flag prefab placed at the end of the last mountain.")]
    public GameObject finishFlagPrefab;

    [Tooltip("Offset applied after placing the finish flag.")]
    public Vector3 finishFlagOffset = Vector3.zero;

    [Tooltip("If true, old finish flags are removed before spawning a new one.")]
    public bool clearOldFinishFlag = true;

    [Tooltip("How much of the end of the mountain to search for the final vertical edge.")]
    public float finishEdgeSearchWidth = 4f;

    [Tooltip("How far away from the mountain edge the front of the flag should be.")]
    public float finishFlagFrontInset = 0.02f;

    [Header("Editor")]
    [Tooltip("If on, the generator will refresh when you tweak values in the inspector.")]
    public bool autoRegenerateOnValidate = true;

    readonly List<GameObject> _generated = new(); // mountains
    GameObject _spawnedFinishFlag;

    [Header("Caves")]
    public CaveGenerator caveGenerator;

    [Header("Shortcuts")]
    public ShortcutGenerator shortcutGenerator;

    [Header("Obstacles")]
    public ObstaclePlacement2D obstaclePlacer;

    [Header("LibTess Fill Settings")]
    [Tooltip("How much to scale positions before tessellating (avoids floating precision issues).")]
    public float tessScale = 1000f;

    [Tooltip("If your fill gets inverted, toggle this.")]
    public bool tessInvertWinding = false;

    [Header("Fill Bottom Extension")]
    public float fallbackFillDepth = 50f;    // used if no camera found / not ortho

    void Start()
    {
        if (!Application.isPlaying) return;
        GenerateNow(); // always generates a new map when the scene loads
    }

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
            seed = unchecked((int)System.DateTime.UtcNow.Ticks);

        Random.InitState(seed);

        ClearGenerated();

        // Always extend fill deep enough so camera/map preview never reveals the bottom
        float fillCloseY = baseY - fallbackFillDepth;

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

            if (shortcutGenerator != null)
            {
                shortcutGenerator.ApplyShortcuts(surface, baseY, cursorX, mountainWidth, seed + 5000 + 100 * m);
            }


            var polygon = BuildClosedPolygon(surface, fillCloseY);
            var colliderPoly = BuildClosedPolygon(surface, baseY);

            // Clean polygon for collider stability
            RemoveNearDuplicates(polygon, 0.0001f);
            RemoveCollinear(polygon, 0.0001f);
            RemoveBacktrackingSpikes(polygon, 0.0001f);

            RemoveNearDuplicates(colliderPoly, 0.0001f);
            RemoveBacktrackingSpikes(colliderPoly, 0.0001f);

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
                var v2 = new Vector2[colliderPoly.Count];
                for (int i = 0; i < colliderPoly.Count; i++) v2[i] = (Vector2)colliderPoly[i];
                col.pathCount = 1;
                col.SetPath(0, v2);
            }

            if (shortcutGenerator != null)
            {
                shortcutGenerator.SpawnTunnelForMountain(
                    surface, baseY, cursorX, mountainWidth,
                    seed + 5000 + 100 * m, mountainGO,
                    fillColor, fillSortingOrder,
                    outlineWidth, mountainColor,
                    tessScale, tessInvertWinding);
            }

            // Spawn obstacles AFTER collider is created (so overlap checks can work)
            if (obstaclePlacer != null)
            {
                obstaclePlacer.SpawnOnMountain(
                    surface,
                    cursorX,
                    mountainWidth,
                    seed + 9999 + 100 * m,
                    transform,
                    playerOneSpawnPoint,
                    ballOneSpawnPoint,
                    playerTwoSpawnPoint,
                    ballTwoSpawnPoint
                );
            }

            if (m == 0)
            {
                AlignGroundToMountainStart(surface);
                SpawnGameplayObjects();
            }

            // Place the finish flag only on the last mountain.
            if (m == mountainCount - 1)
            {
                PlaceFinishFlag(surface);
            }

            _generated.Add(mountainGO);
            cursorX += mountainWidth + mountainSpacing;
        }
    }

    void ClearGenerated()
    {
        // clear finish flag by tracked reference
        if (_spawnedFinishFlag != null)
        {
            if (Application.isPlaying) Destroy(_spawnedFinishFlag);
            else DestroyImmediate(_spawnedFinishFlag);

            _spawnedFinishFlag = null;
        }

        // also clear any leftover Finish Flag objects by name
        var extraToDestroy = new List<GameObject>();
        foreach (Transform child in transform)
        {
            if (!child) continue;

            if (child.name.StartsWith("Finish Flag"))
                extraToDestroy.Add(child.gameObject);
        }

        for (int i = 0; i < extraToDestroy.Count; i++)
        {
            var go = extraToDestroy[i];
            if (!go) continue;

            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }

        if (shortcutGenerator != null)
            shortcutGenerator.ClearGeneratedVisuals();

        var extraTunnelTops = new List<GameObject>();
        foreach (Transform child in transform)
        {
            if (!child) continue;

            if (child.name.StartsWith("TunnelTop_"))
                extraTunnelTops.Add(child.gameObject);
        }

        for (int i = 0; i < extraTunnelTops.Count; i++)
        {
            var go = extraTunnelTops[i];
            if (!go) continue;

            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }

        if (obstaclePlacer != null)
            obstaclePlacer.Clear();

        var toDestroy = new List<GameObject>();
        foreach (Transform child in transform)
        {
            if (!child) continue;

            if (child.name.StartsWith("Mountain_"))
                toDestroy.Add(child.gameObject);
        }

        for (int i = 0; i < toDestroy.Count; i++)
        {
            var go = toDestroy[i];
            if (!go) continue;

            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }

        _generated.Clear();
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
                Vector3 p1 = new Vector3(x, prevY, 0f);

                if ((pts[pts.Count - 1] - p1).sqrMagnitude > 0.0000001f)
                    pts.Add(p1);

                if (Mathf.Abs(y - prevY) > 0.0001f)
                {
                    Vector3 p2 = new Vector3(x, y, 0f);
                    if ((pts[pts.Count - 1] - p2).sqrMagnitude > 0.0000001f)
                        pts.Add(p2);
                }
            }
            else
            {
                pts.Add(new Vector3(x, y, 0f));
            }

            prevY = y;
        }

        return pts;
    }

    List<Vector3> BuildClosedPolygon(List<Vector3> surface, float closeY)
    {
        const float eps = 0.0001f;

        var poly = new List<Vector3>(surface.Count + 4);
        poly.AddRange(surface);

        var last = surface[surface.Count - 1];
        var first = surface[0];

        if (Mathf.Abs(last.y - closeY) > eps)
            poly.Add(new Vector3(last.x, closeY, 0f));

        Vector3 groundStart = new Vector3(first.x, closeY, 0f);
        if ((poly[poly.Count - 1] - groundStart).sqrMagnitude > eps * eps)
            poly.Add(groundStart);

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

    void PlaceFinishFlag(List<Vector3> surface)
    {
        if (finishFlagPrefab == null || surface == null || surface.Count < 2)
            return;

        if (clearOldFinishFlag && _spawnedFinishFlag != null)
        {
            if (Application.isPlaying) Destroy(_spawnedFinishFlag);
            else DestroyImmediate(_spawnedFinishFlag);

            _spawnedFinishFlag = null;
        }

        float maxX = surface[surface.Count - 1].x;
        float searchStartX = maxX - finishEdgeSearchWidth;

        int edgeTopIndex = -1;

        for (int i = 0; i < surface.Count - 1; i++)
        {
            Vector3 a = surface[i];
            Vector3 b = surface[i + 1];

            bool inEndZone = a.x >= searchStartX && b.x >= searchStartX;
            bool sameX = Mathf.Abs(a.x - b.x) < 0.0001f;
            bool goesDown = b.y < a.y;

            if (inEndZone && sameX && goesDown)
            {
                edgeTopIndex = i;
            }
        }

        if (edgeTopIndex < 0)
        {
            edgeTopIndex = surface.Count - 1;
        }

        Vector3 localFlagPoint = surface[edgeTopIndex];
        Vector3 worldFlagPoint = transform.TransformPoint(localFlagPoint);

        _spawnedFinishFlag = Instantiate(
            finishFlagPrefab,
            worldFlagPoint,
            finishFlagPrefab.transform.rotation,
            transform
        );

        _spawnedFinishFlag.name = "Finish Flag";

        Transform flagBase = FindChildRecursive(_spawnedFinishFlag.transform, "FlagBase");

        if (flagBase != null)
        {
            Vector3 baseWorld = flagBase.position;

            float desiredX = worldFlagPoint.x + finishFlagFrontInset;
            float desiredY = worldFlagPoint.y;

            Vector3 pos = _spawnedFinishFlag.transform.position;
            pos.x += desiredX - baseWorld.x;
            pos.y += desiredY - baseWorld.y;
            _spawnedFinishFlag.transform.position = pos;
        }
        else
        {
            // fallback if FlagBase was not added yet
            _spawnedFinishFlag.transform.position = worldFlagPoint + finishFlagOffset;
            return;
        }

        _spawnedFinishFlag.transform.position += finishFlagOffset;
    }

    Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent.name == childName)
            return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform result = FindChildRecursive(parent.GetChild(i), childName);
            if (result != null)
                return result;
        }

        return null;
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

        var work = new List<Vector3>(polygon);
        RemoveNearDuplicates(work, 0.0001f);
        RemoveCollinear(work, 0.0001f);
        RemoveBacktrackingSpikes(work, 0.0001f);

        if (work.Count < 3)
            return;

        float s = Mathf.Max(1f, tessScale);

        var tess = new Tess();

        var contour = new ContourVertex[work.Count];
        for (int i = 0; i < work.Count; i++)
        {
            var p = work[i];
            contour[i].Position = new Vec3(p.x * s, p.y * s, 0);
            contour[i].Data = i;
        }

        var winding = tessInvertWinding ? ContourOrientation.Clockwise : ContourOrientation.CounterClockwise;
        tess.AddContour(contour, winding);

        tess.Tessellate(WindingRule.EvenOdd, ElementType.Polygons, 3);

        if (tess.ElementCount <= 0 || tess.Vertices == null || tess.Vertices.Length < 3)
        {
            Debug.LogWarning($"LibTess failed to tessellate {mountainGO.name} (points={work.Count}).");
            return;
        }

        var verts = new Vector3[tess.Vertices.Length];
        for (int i = 0; i < verts.Length; i++)
        {
            var v = tess.Vertices[i].Position;
            verts[i] = new Vector3((float)(v.X / s), (float)(v.Y / s), 0f);
        }

        int triCount = tess.ElementCount * 3;
        var tris = new int[triCount];

        int k = 0;
        for (int e = 0; e < tess.ElementCount; e++)
        {
            int i0 = tess.Elements[e * 3 + 0];
            int i1 = tess.Elements[e * 3 + 1];
            int i2 = tess.Elements[e * 3 + 2];

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