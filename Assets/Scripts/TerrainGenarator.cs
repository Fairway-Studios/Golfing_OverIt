using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor; // for EditorApplication.delayCall
#endif

/// <summary>
/// Safe v2: 2D Perlin-based mountain + cave outline generator.
/// - Grey closed LineRenderer for mountain outline (optional PolygonCollider2D)
/// - Red LineRenderer for cave/tunnel hints
/// Works in Unity 6000.0.60f1.
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

    [Header("Outline look")]
    public float outlineWidth = 0.12f;
    public Color mountainColor = new Color(0.65f, 0.68f, 0.72f);
    public bool addPolygonCollider2D = true;

    [Header("Caves / Tunnels (red outlines)")]
    [Min(0)] public int cavesPerMountain = 3;
    public float caveStartInset = 0.6f;
    public float caveStepLength = 0.7f;
    [Min(4)] public int caveSteps = 35;
    public float caveWiggleFreq = 0.25f;
    public float caveWiggleAmp = 0.9f;
    public float caveDownBias = 0.45f;
    public float caveLineWidth = 0.08f;
    public Color caveColor = Color.red;

    // NEW: spacing controls
    [Tooltip("Total vertical thickness (in normal space) shared by all lanes under the surface.")]
    public float caveLaneTotalThickness = 1.6f;   // ↓ smaller = lanes closer
    [Tooltip("Gap fraction between lanes (0..0.3 typical).")]
    [Range(0f, 0.3f)] public float caveLaneGapFrac = 0.04f; // ↓ smaller = lanes closer
    [Tooltip("Normal distance of the first lane's roof below the surface.")]
    public float caveLaneTopInset0 = 0.45f;       // ↓ smaller = lanes closer to surface

    [Tooltip("Minimum horizontal distance between cave start/end X positions.")]
    public float caveMinSpanX = 8f;               // ↓ smaller = endpoints can be closer
    [Tooltip("Extra margin used when reserving X-intervals to avoid overlap.")]
    public float caveXMargin = 0.6f;              // ↓ smaller = intervals can sit closer


    [Header("Editor")]
    [Tooltip("If on, the generator will refresh when you tweak values. Uses a deferred call to avoid warnings.")]
    public bool autoRegenerateOnValidate = true;

    // Keep references to what we spawned so we can clear them later
    readonly List<GameObject> _generated = new();

    // ----------------- PUBLIC BUTTON -----------------
    [ContextMenu("Generate Now")]
    public void GenerateNow()
    {
        Regenerate();
    }

    // ----------------- MAIN BUILD -----------------
    void Regenerate()
    {
        // Seed
        if (useRandomSeed)
            seed = Random.Range(int.MinValue / 2, int.MaxValue / 2);
        Random.InitState(seed);

        // Clear any previous children we created
        ClearGenerated();

        float cursorX = 0f;
        for (int m = 0; m < mountainCount; m++)
        {
            var mountainGO = new GameObject($"Mountain_{m}");
            mountainGO.transform.SetParent(transform, false);

            var surface = BuildSurfacePoints(cursorX, baseY, mountainWidth, stepX, amplitude, frequency, seed + 100 * m);
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

            DrawCaves(mountainGO.transform, surface, m);

            _generated.Add(mountainGO);
            cursorX += mountainWidth + mountainSpacing;
        }
    }

    // ----------------- HELPERS -----------------
    void ClearGenerated()
    {
        for (int i = _generated.Count - 1; i >= 0; i--)
        {
            var go = _generated[i];
            if (!go) continue;

            if (Application.isPlaying)
                Destroy(go);              // safe at runtime
            else
                DestroyImmediate(go);     // safe in Edit Mode (not during callbacks)
        }

        _generated.Clear();

        // Also clear any leftover children we didn’t track (safety)
        // Useful if script recompiled and list got reset.
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

        for (int i = 0; i <= steps; i++)
        {
            float x = startX + i * step;
            float n = Mathf.PerlinNoise((x * freq) + noiseOffset, noiseOffset);
            float y = groundY + n * amp;
            pts.Add(new Vector3(x, y, 0f));
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

    void DrawCaves(Transform parent, List<Vector3> surface, int mountainIndex)
    {
        if (cavesPerMountain <= 0) return;

        // How far below the surface the lanes start and how thick the total band is
        float laneTopInset0 = Mathf.Max(caveLaneTopInset0, caveStartInset);
        float totalBandThickness = Mathf.Max(0.2f, caveLaneTotalThickness);
        int laneCount = Mathf.Max(1, cavesPerMountain);
        float laneHeight = totalBandThickness / laneCount;
        float laneGap = Mathf.Clamp01(caveLaneGapFrac) * laneHeight;
        float usableLane = Mathf.Max(0.01f, laneHeight - laneGap);

        // Bézier sampling
        int entryArcSteps = 6, exitArcSteps = 6;
        int midSteps = Mathf.Max(10, caveSteps - (entryArcSteps + exitArcSteps + 2));

        // Ensure caves use different X-intervals to avoid crossing
        List<Vector2> reservedX = new(); // each = (xMin, xMax), expanded with margin
        float minSpanX = Mathf.Max(2f, caveMinSpanX);
        float xMargin = Mathf.Max(0f, caveXMargin);

        float minX = surface[0].x, maxX = surface[^1].x;
        int minIdx = Mathf.Max(2, Mathf.FloorToInt(surface.Count * 0.12f));
        int maxIdx = Mathf.Min(surface.Count - 3, Mathf.CeilToInt(surface.Count * 0.88f));

        for (int c = 0; c < cavesPerMountain; c++)
        {
            int lane = c; // 0..(n-1). (You can shuffle for variety.)
            float laneTopInset = laneTopInset0 + lane * laneHeight; // normal distance for the lane's roof
            float laneBottomInset = laneTopInset + usableLane;      // deeper normal distance (bottom of lane)

            // --- pick start/end indices that: (1) are far enough apart, (2) don't overlap prior X-intervals ---
            int iA = Random.Range(minIdx, maxIdx);
            int iB = iA;
            int guard = 0;
            bool ok = false;

            while (guard++ < 200)
            {
                iB = Random.Range(minIdx, maxIdx);
                float xa = surface[iA].x, xb = surface[iB].x;
                if (Mathf.Abs(xb - xa) < minSpanX) { iA = Random.Range(minIdx, maxIdx); continue; }
                if (xb < xa) { var t = xa; xa = xb; xb = t; } // ensure xa < xb

                // check against reserved
                bool intersects = false;
                foreach (var seg in reservedX)
                {
                    if (!(xb + xMargin < seg.x || xa - xMargin > seg.y)) { intersects = true; break; }
                }
                if (!intersects) { reservedX.Add(new Vector2(xa, xb)); ok = true; break; }
                iA = Random.Range(minIdx, maxIdx);
            }
            if (!ok) continue; // give up this cave if we fail to find a clean interval

            // exact surface points (start/end)
            Vector2 sA = new Vector2(surface[iA].x, SurfaceYAtX(surface, surface[iA].x));
            Vector2 sB = new Vector2(surface[iB].x, SurfaceYAtX(surface, surface[iB].x));
            if (sB.x < sA.x) { var t = sA; sA = sB; sB = t; }

            // local tangents and inward normals at endpoints
            Vector2 tanA = TangentAtX(surface, sA.x);
            Vector2 tanB = TangentAtX(surface, sB.x);
            Vector2 inA = new Vector2(tanA.y, -tanA.x).normalized;
            Vector2 inB = new Vector2(tanB.y, -tanB.x).normalized;

            // Bézier control points (push into the mountain along inward, and forward along tangent)
            // depth for the lane is somewhere between its roof and bottom
            float ctrlDepth = Mathf.Lerp(laneTopInset, laneBottomInset, 0.55f);
            float run = Mathf.Max(minSpanX * 0.42f, (sB.x - sA.x) * 0.42f); // forward distance for controls

            Vector2 c1 = sA + inA * ctrlDepth + tanA * run;
            Vector2 c2 = sB + inB * ctrlDepth - tanB * run;

            // ---- sample the full curve: start -> entry arc -> mid -> exit arc -> end ----
            var path = new List<Vector3>();
            path.Add(sA); // on-surface start

            // Entry arc (smoothly leave surface)
            for (int k = 1; k <= entryArcSteps; k++)
            {
                float t = k / (float)(entryArcSteps + 1);
                // quadratic ease for softness
                float te = t * t;
                Vector2 q = Vector2.Lerp(sA, Bezier(sA, c1, c2, sB, te * 0.15f), 0.75f); // shallow start
                                                                                         // project into lane along inward normal (no vertical clamp!)
                q = ProjectIntoLane(surface, q, laneTopInset, laneBottomInset);
                path.Add(q);
            }

            // Mid samples
            for (int i = 1; i <= midSteps; i++)
            {
                float t = i / (float)(midSteps + 1);
                Vector2 q = Bezier(sA, c1, c2, sB, t);
                q = ProjectIntoLane(surface, q, laneTopInset, laneBottomInset);
                // keep X inside mountain bounds
                q.x = Mathf.Clamp(q.x, minX + 0.05f, maxX - 0.05f);
                path.Add(q);
            }

            // Exit arc (curve back to surface)
            for (int k = exitArcSteps; k >= 1; k--)
            {
                float t = 1f - (k / (float)(exitArcSteps + 1));
                float te = 1f - (t * t);
                Vector2 q = Vector2.Lerp(Bezier(sA, c1, c2, sB, 1f - te * 0.15f), sB, 0.75f);
                q = ProjectIntoLane(surface, q, laneTopInset, laneBottomInset);
                path.Add(q);
            }

            path.Add(sB); // on-surface end

            // Draw
            var go = new GameObject($"Cave_{mountainIndex}_lane{lane}");
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.loop = false;
            lr.positionCount = path.Count;
            lr.useWorldSpace = false;
            lr.widthMultiplier = caveLineWidth;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = caveColor;
            lr.endColor = caveColor;
            lr.SetPositions(path.ToArray());

            _generated.Add(go);
        }
    }

    // Cubic Bézier
    static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t)
    {
        float u = 1f - t;
        return (u * u * u) * a + 3f * (u * u) * t * b + 3f * u * (t * t) * c + (t * t * t) * d;
    }





    /// <summary>
    /// Linear-interpolate the surface height at a given X.
    /// Assumes 'surface' points are ordered by X.
    /// </summary>
    float SurfaceYAtX(List<Vector3> surface, float x)
    {
        // clamp outside
        if (x <= surface[0].x) return surface[0].y;
        if (x >= surface[^1].x) return surface[^1].y;

        // find segment [i, i+1] containing x (linear scan is fine for small lists)
        for (int i = 0; i < surface.Count - 1; i++)
        {
            Vector3 a = surface[i];
            Vector3 b = surface[i + 1];
            if (x >= a.x && x <= b.x)
            {
                float t = Mathf.InverseLerp(a.x, b.x, x);
                return Mathf.Lerp(a.y, b.y, t);
            }
        }
        return surface[^1].y;
    }

    // Numerical tangent from the surface slope at x (finite difference using your stepX)
    Vector2 TangentAtX(List<Vector3> surface, float x)
    {
        float dx = Mathf.Max(0.001f, stepX * 0.5f);
        float y0 = SurfaceYAtX(surface, x - dx);
        float y1 = SurfaceYAtX(surface, x + dx);
        Vector2 t = new Vector2(1f, (y1 - y0) / (2f * dx));
        if (t.sqrMagnitude < 1e-6f) t = Vector2.right;
        return t.normalized;
    }

    // Clamp a point to a lane defined by [topInset, bottomInset] *along the inward normal* at that x.
    Vector2 ProjectIntoLane(List<Vector3> surface, Vector2 p, float laneTopInset, float laneBottomInset)
    {
        // surface point at same x
        float surfY = SurfaceYAtX(surface, p.x);
        Vector2 s = new Vector2(p.x, surfY);

        // inward normal at x
        Vector2 tan = TangentAtX(surface, p.x);
        Vector2 inward = new Vector2(tan.y, -tan.x);
        if (inward.sqrMagnitude < 1e-6f) inward = Vector2.down;
        inward.Normalize();

        // signed distance from surface along inward
        float d = Vector2.Dot(p - s, inward);
        d = Mathf.Clamp(d, laneTopInset, laneBottomInset); // clamp ALONG THE NORMAL (no horizontal shelf)

        return s + inward * d;
    }



    // ----------------- EDITOR LIFECYCLE -----------------
    void OnEnable()
    {
#if UNITY_EDITOR
        // Defer generation so we don't mutate hierarchy during the enable pass
        if (!Application.isPlaying)
            EditorApplication.delayCall += () => { if (this) GenerateNow(); };
#endif
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!autoRegenerateOnValidate) return;
        // Defer regeneration until after OnValidate ends
        EditorApplication.delayCall += () =>
        {
            if (this) GenerateNow();
        };
    }
#endif
}
