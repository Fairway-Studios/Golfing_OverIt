using System.Collections.Generic;
using UnityEngine;
using LibTessDotNet;

/// <summary>
/// Creates tunnel shortcuts through mountain peaks by splitting each peak
/// into two GameObjects with a gap between them.
///
/// Hook-up — add after the collider block in PerlinMountain2D.Regenerate():
///   if (shortcutGenerator != null)
///       shortcutGenerator.SpawnTunnelForMountain(
///           surface, baseY, cursorX, mountainWidth,
///           seed + 5000 + 100 * m, mountainGO,
///           fillColor, fillSortingOrder, outlineWidth, mountainColor,
///           tessScale, tessInvertWinding);
///
/// Call shortcutGenerator.ClearGeneratedVisuals() in ClearGenerated().
/// </summary>
public class ShortcutGenerator : MonoBehaviour
{
    [Header("Enable")]
    public bool spawnTunnels = true;

    [Header("Count")]
    [Tooltip("How many tunnel shortcuts to place per mountain.")]
    [Min(1)] public int tunnelsPerMountain = 1;

    [Header("Tunnel Shape")]
    [Range(0.1f, 0.8f)] public float cutHeightFraction = 0.3f;
    [Range(0f, 0.2f)] public float cutHeightRandomness = 0.08f;
    public float gapSize = 2f;

    [Header("Shape Variations (random each generation)")]
    public float archDepth = 0.8f;
    public float jaggedAmplitude = 0.5f;
    [Range(3, 20)] public int jaggedPoints = 8;
    public float angledMaxTilt = 0.8f;

    [Header("Peak Requirements")]
    public float minPeakHeight = 2f;
    public float minTunnelWidth = 2f;
    public float maxTunnelWidth = 20f;

    [Header("Sorting Orders")]
    public int topPieceSortingOrder = -8;
    public int topPieceOutlineSortingOrder = 0;

    enum TunnelShape { Arch, Jagged, Angled }

    readonly List<GameObject> _spawnedPieces = new();

    // Legacy no-op — keeps existing ApplyShortcuts call compiling
    public void ApplyShortcuts(
        List<Vector3> surface, float baseY,
        float mountainStartX, float mountainWidth, int seed)
    { }

    public void ClearGeneratedVisuals()
    {
        for (int i = _spawnedPieces.Count - 1; i >= 0; i--)
        {
            var go = _spawnedPieces[i];
            if (!go) continue;
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }
        _spawnedPieces.Clear();
    }

    // -----------------------------------------------------------------------
    // Main entry
    // -----------------------------------------------------------------------
    public void SpawnTunnelForMountain(
        List<Vector3> surfaceLocal,
        float baseY,
        float mountainStartX,
        float mountainWidth,
        int seed,
        GameObject mountainGO,
        Color fillColor,
        int fillSortingOrder,
        float outlineWidth,
        Color outlineColor,
        float tessScale,
        bool tessInvertWinding)
    {
        if (!spawnTunnels) return;
        if (surfaceLocal == null || surfaceLocal.Count < 6) return;
        if (mountainGO == null) return;

        var rng = new System.Random(seed);
        float mountainEndX = mountainStartX + mountainWidth;

        // Find all eligible peaks, sorted tallest first
        var peaks = FindAllPeaks(surfaceLocal, baseY, mountainStartX, mountainEndX);

        if (peaks.Count == 0)
        {
            Debug.Log($"[ShortcutGen] No peaks on x=[{mountainStartX:F0},{mountainEndX:F0}]. " +
                      $"Lower minPeakHeight ({minPeakHeight}).");
            return;
        }

        int placed = 0;
        foreach (int peakIdx in peaks)
        {
            if (placed >= tunnelsPerMountain) break;

            if (TryBuildTunnel(surfaceLocal, baseY, mountainStartX, mountainEndX,
                               mountainGO, fillColor, fillSortingOrder,
                               outlineWidth, outlineColor, tessScale, tessInvertWinding,
                               rng, peakIdx))
            {
                placed++;
            }
        }

        Debug.Log($"[ShortcutGen] Mountain x=[{mountainStartX:F0},{mountainEndX:F0}]: " +
                  $"placed {placed}/{tunnelsPerMountain} tunnel(s) from {peaks.Count} peaks.");
    }

    // -----------------------------------------------------------------------
    // Try to build one tunnel through a specific peak
    // -----------------------------------------------------------------------
    bool TryBuildTunnel(
        List<Vector3> surfaceLocal,
        float baseY, float mountainStartX, float mountainEndX,
        GameObject mountainGO,
        Color fillColor, int fillSortingOrder,
        float outlineWidth, Color outlineColor,
        float tessScale, bool tessInvertWinding,
        System.Random rng, int peakIdx)
    {
        float peakY = surfaceLocal[peakIdx].y;
        float peakH = peakY - baseY;

        float frac = cutHeightFraction
                     + ((float)rng.NextDouble() * 2f - 1f) * cutHeightRandomness;
        frac = Mathf.Clamp(frac, 0.1f, 0.75f);
        float cutY = baseY + peakH * frac;

        int leftIdx = FindCrossingLeft(surfaceLocal, peakIdx, cutY);
        int rightIdx = FindCrossingRight(surfaceLocal, peakIdx, cutY);
        if (leftIdx < 0 || rightIdx < 0) return false;

        float entryX = InterpolateXAtY(surfaceLocal, leftIdx, leftIdx + 1, cutY);
        float exitX = InterpolateXAtY(surfaceLocal, rightIdx, rightIdx - 1, cutY);
        if (exitX - entryX < minTunnelWidth) return false;

        var shapeValues = (TunnelShape[])System.Enum.GetValues(typeof(TunnelShape));
        var shape = shapeValues[rng.Next(0, shapeValues.Length)];

        var cutLine = BuildCutLine(entryX, exitX, cutY, shape, rng);
        var bottomSurf = BuildBottomSurface(surfaceLocal, leftIdx, rightIdx, cutLine);
        var topSurf = BuildTopSurface(surfaceLocal, leftIdx, rightIdx, cutLine, gapSize);

        ReplaceMountainWithBottom(mountainGO, bottomSurf, baseY,
            fillColor, fillSortingOrder, outlineWidth, outlineColor,
            tessScale, tessInvertWinding);

        var topGO = new GameObject($"TunnelTop_{_spawnedPieces.Count}");
        topGO.transform.SetParent(mountainGO.transform.parent, false);
        _spawnedPieces.Add(topGO);

        BuildTopPiece(topGO, topSurf,
            fillColor, topPieceSortingOrder, outlineWidth, outlineColor,
            tessScale, tessInvertWinding);

        Debug.Log($"[ShortcutGen] ? {shape} tunnel: peak=({surfaceLocal[peakIdx].x:F1},{peakY:F1}) " +
                  $"cutY={cutY:F1} width={exitX - entryX:F1}");
        return true;
    }

    // -----------------------------------------------------------------------
    // Peak detection — wider window handles stepped terrain
    // -----------------------------------------------------------------------
    List<int> FindAllPeaks(List<Vector3> surface, float baseY, float minX, float maxX)
    {
        var result = new List<(int idx, float h)>();
        const int window = 3;

        for (int i = window; i < surface.Count - window; i++)
        {
            float x = surface[i].x;
            float y = surface[i].y;

            if (x < minX + 3f || x > maxX - 3f) continue;
            if (y - baseY < minPeakHeight) continue;

            bool isLocalMax = true;
            for (int w = 1; w <= window && isLocalMax; w++)
            {
                if (surface[i - w].y > y || surface[i + w].y > y)
                    isLocalMax = false;
            }

            if (isLocalMax)
                result.Add((i, y - baseY));
        }

        // Sort tallest first
        result.Sort((a, b) => b.h.CompareTo(a.h));

        var indices = new List<int>(result.Count);
        foreach (var p in result) indices.Add(p.idx);
        return indices;
    }

    // -----------------------------------------------------------------------
    // Cut line shape variations
    // -----------------------------------------------------------------------
    List<Vector3> BuildCutLine(float entryX, float exitX, float baseCutY,
                               TunnelShape shape, System.Random rng)
    {
        var pts = new List<Vector3>();

        switch (shape)
        {
            case TunnelShape.Arch:
                {
                    int steps = 12;
                    for (int i = 0; i <= steps; i++)
                    {
                        float t = (float)i / steps;
                        float x = Mathf.Lerp(entryX, exitX, t);
                        float dy = Mathf.Sin(t * Mathf.PI) * archDepth;
                        pts.Add(new Vector3(x, baseCutY - dy, 0f));
                    }
                    break;
                }

            case TunnelShape.Jagged:
                {
                    pts.Add(new Vector3(entryX, baseCutY, 0f));

                    float prevY = baseCutY;

                    for (int i = 1; i < jaggedPoints - 1; i++)
                    {
                        float t = (float)i / (jaggedPoints - 1);
                        float x = Mathf.Lerp(entryX, exitX, t);

                        float noise = ((float)rng.NextDouble() * 2f - 1f) * jaggedAmplitude;
                        float y = baseCutY + noise;

                        // keep jagged, but less likely to make broken cap polygons
                        y = Mathf.Clamp(y,
                            prevY - jaggedAmplitude * 0.75f,
                            prevY + jaggedAmplitude * 0.75f);

                        pts.Add(new Vector3(x, y, 0f));
                        prevY = y;
                    }

                    pts.Add(new Vector3(exitX, baseCutY, 0f));
                    break;
                }

            case TunnelShape.Angled:
                {
                    float tilt = ((float)rng.NextDouble() * 2f - 1f) * angledMaxTilt;
                    pts.Add(new Vector3(entryX, baseCutY - tilt * 0.5f, 0f));
                    pts.Add(new Vector3(exitX, baseCutY + tilt * 0.5f, 0f));
                    break;
                }
        }

        RemoveNearDuplicates(pts, 0.001f);
        return pts;
    }

    // -----------------------------------------------------------------------
    // Surface building
    // -----------------------------------------------------------------------
    List<Vector3> BuildBottomSurface(List<Vector3> surface, int leftIdx, int rightIdx,
                                     List<Vector3> cutLine)
    {
        var pts = new List<Vector3>();

        for (int i = 0; i <= leftIdx; i++)
            pts.Add(surface[i]);

        pts.AddRange(cutLine);

        for (int i = rightIdx; i < surface.Count; i++)
            pts.Add(surface[i]);

        RemoveNearDuplicates(pts, 0.001f);
        return pts;
    }

    List<Vector3> BuildTopSurface(List<Vector3> surface, int leftIdx, int rightIdx,
                                  List<Vector3> cutLine, float shift)
    {
        var pts = new List<Vector3>();

        // bottom edge of top cap: left -> right
        for (int i = 0; i < cutLine.Count; i++)
        {
            var p = cutLine[i];
            pts.Add(new Vector3(p.x, p.y + shift, 0f));
        }

        // top mountain edge must come back right -> left
        for (int i = rightIdx - 1; i > leftIdx; i--)
            pts.Add(new Vector3(surface[i].x, surface[i].y + shift, 0f));

        RemoveNearDuplicates(pts, 0.001f);
        return pts;
    }

    // -----------------------------------------------------------------------
    // Replace bottom mountain mesh/collider
    // -----------------------------------------------------------------------
    void ReplaceMountainWithBottom(GameObject mountainGO, List<Vector3> bottomSurface,
        float baseY, Color fillColor, int fillSortingOrder,
        float outlineWidth, Color outlineColor, float tessScale, bool tessInvertWinding)
    {
        DestroyComponent<MeshFilter>(mountainGO);
        DestroyComponent<MeshRenderer>(mountainGO);
        DestroyComponent<LineRenderer>(mountainGO);
        DestroyComponent<PolygonCollider2D>(mountainGO);

        var fillPoly = BuildClosedPolygon(bottomSurface, baseY - 50f);
        var colPoly = BuildClosedPolygon(bottomSurface, baseY);
        CleanPolygon(fillPoly);
        CleanPolygon(colPoly);

        AddFillMesh(mountainGO, fillPoly, fillColor, fillSortingOrder, tessScale, tessInvertWinding);

        var lr = mountainGO.AddComponent<LineRenderer>();
        lr.loop = false;
        lr.positionCount = bottomSurface.Count;
        lr.useWorldSpace = false;
        lr.widthMultiplier = outlineWidth;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = outlineColor;
        lr.endColor = outlineColor;
        lr.SetPositions(bottomSurface.ToArray());

        var col = mountainGO.AddComponent<PolygonCollider2D>();
        var v2 = new Vector2[colPoly.Count];
        for (int i = 0; i < colPoly.Count; i++) v2[i] = colPoly[i];
        col.pathCount = 1;
        col.SetPath(0, v2);
    }

    // -----------------------------------------------------------------------
    // Build top piece
    // IMPORTANT:
    // topSurface is already the cap contour, so do not close it again to a flat Y
    // -----------------------------------------------------------------------
    void BuildTopPiece(GameObject topGO, List<Vector3> topSurface,
        Color fillColor, int fillSortingOrder,
        float outlineWidth, Color outlineColor, float tessScale, bool tessInvertWinding)
    {
        if (topSurface == null || topSurface.Count < 3) return;

        var fillPoly = new List<Vector3>(topSurface);
        var colPoly = new List<Vector3>(topSurface);

        CleanPolygon(fillPoly);
        CleanPolygon(colPoly);

        if (fillPoly.Count < 3) return;

        AddFillMesh(topGO, fillPoly, fillColor, fillSortingOrder, tessScale, tessInvertWinding);

        var lr = topGO.AddComponent<LineRenderer>();
        lr.loop = true;
        lr.positionCount = fillPoly.Count;
        lr.useWorldSpace = false;
        lr.widthMultiplier = outlineWidth;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = outlineColor;
        lr.endColor = outlineColor;
        lr.sortingOrder = topPieceOutlineSortingOrder;
        lr.SetPositions(fillPoly.ToArray());

        var col = topGO.AddComponent<PolygonCollider2D>();
        var v2 = new Vector2[colPoly.Count];
        for (int i = 0; i < colPoly.Count; i++) v2[i] = colPoly[i];
        col.pathCount = 1;
        col.SetPath(0, v2);
    }

    // -----------------------------------------------------------------------
    // Crossing detection
    // -----------------------------------------------------------------------
    int FindCrossingLeft(List<Vector3> surface, int fromIdx, float targetY)
    {
        float peakX = surface[fromIdx].x;
        float prevY = surface[fromIdx].y;

        for (int i = fromIdx - 1; i >= 0; i--)
        {
            float y = surface[i].y;
            float x = surface[i].x;

            if (peakX - x > maxTunnelWidth * 0.5f) return -1;
            if (y > prevY + 0.2f && y > targetY) return -1;
            if (y <= targetY) return i;

            prevY = y;
        }
        return -1;
    }

    int FindCrossingRight(List<Vector3> surface, int fromIdx, float targetY)
    {
        float peakX = surface[fromIdx].x;
        float prevY = surface[fromIdx].y;

        for (int i = fromIdx + 1; i < surface.Count; i++)
        {
            float y = surface[i].y;
            float x = surface[i].x;

            if (x - peakX > maxTunnelWidth * 0.5f) return -1;
            if (y > prevY + 0.5f && y > targetY) return -1;
            if (y <= targetY) return i;

            prevY = y;
        }
        return -1;
    }

    float InterpolateXAtY(List<Vector3> surface, int idxA, int idxB, float targetY)
    {
        if (idxA < 0 || idxB < 0 || idxA >= surface.Count || idxB >= surface.Count) return -1f;

        Vector3 a = surface[idxA];
        Vector3 b = surface[idxB];
        float dy = b.y - a.y;

        if (Mathf.Abs(dy) < 0.0001f) return a.x;

        return Mathf.Lerp(a.x, b.x, Mathf.Clamp01((targetY - a.y) / dy));
    }

    // -----------------------------------------------------------------------
    // Shared helpers
    // -----------------------------------------------------------------------
    void DestroyComponent<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        if (!c) return;
        if (Application.isPlaying) Destroy(c);
        else DestroyImmediate(c);
    }

    List<Vector3> BuildClosedPolygon(List<Vector3> surface, float closeY)
    {
        var poly = new List<Vector3>(surface);
        var last = surface[surface.Count - 1];
        var first = surface[0];

        if (Mathf.Abs(last.y - closeY) > 0.0001f)
            poly.Add(new Vector3(last.x, closeY, 0f));

        var gs = new Vector3(first.x, closeY, 0f);
        if ((poly[poly.Count - 1] - gs).sqrMagnitude > 0.0001f)
            poly.Add(gs);

        return poly;
    }

    void CleanPolygon(List<Vector3> pts)
    {
        RemoveNearDuplicates(pts, 0.0001f);
        RemoveCollinear(pts, 0.0001f);
    }

    void RemoveNearDuplicates(List<Vector3> pts, float eps)
    {
        if (pts == null || pts.Count < 2) return;

        for (int i = pts.Count - 2; i >= 0; i--)
        {
            if ((pts[i + 1] - pts[i]).sqrMagnitude <= eps * eps)
                pts.RemoveAt(i + 1);
        }

        if (pts.Count > 2 && (pts[0] - pts[pts.Count - 1]).sqrMagnitude <= eps * eps)
            pts.RemoveAt(pts.Count - 1);
    }

    void RemoveCollinear(List<Vector3> pts, float eps)
    {
        if (pts.Count < 3) return;

        int guard = 0;
        while (pts.Count >= 3 && guard++ < 5000)
        {
            bool removed = false;

            for (int i = 0; i < pts.Count; i++)
            {
                Vector2 a = pts[(i - 1 + pts.Count) % pts.Count];
                Vector2 b = pts[i];
                Vector2 c = pts[(i + 1) % pts.Count];

                float cross = (b.x - a.x) * (c.y - b.y) - (b.y - a.y) * (c.x - b.x);
                if (Mathf.Abs(cross) <= eps)
                {
                    pts.RemoveAt(i);
                    removed = true;
                    break;
                }
            }

            if (!removed) break;
        }
    }

    void AddFillMesh(GameObject go, List<Vector3> polygon, Color color,
                     int sortingOrder, float tessScale, bool tessInvertWinding)
    {
        if (polygon == null || polygon.Count < 3) return;

        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = new Material(Shader.Find("Sprites/Default")) { color = color };
        mr.sortingOrder = sortingOrder;

        float s = Mathf.Max(1f, tessScale);
        var tess = new Tess();
        var contour = new ContourVertex[polygon.Count];

        for (int i = 0; i < polygon.Count; i++)
            contour[i].Position = new Vec3(polygon[i].x * s, polygon[i].y * s, 0);

        tess.AddContour(contour,
            tessInvertWinding ? ContourOrientation.Clockwise : ContourOrientation.CounterClockwise);

        tess.Tessellate(WindingRule.EvenOdd, ElementType.Polygons, 3);
        if (tess.ElementCount <= 0) return;

        var verts = new Vector3[tess.Vertices.Length];
        for (int i = 0; i < verts.Length; i++)
        {
            verts[i] = new Vector3(
                (float)(tess.Vertices[i].Position.X / s),
                (float)(tess.Vertices[i].Position.Y / s),
                0f
            );
        }

        var tris = new List<int>();
        for (int e = 0; e < tess.ElementCount; e++)
        {
            int i0 = tess.Elements[e * 3];
            int i1 = tess.Elements[e * 3 + 1];
            int i2 = tess.Elements[e * 3 + 2];

            if (i0 >= 0 && i1 >= 0 && i2 >= 0)
            {
                tris.Add(i0);
                tris.Add(i1);
                tris.Add(i2);
            }
        }

        var mesh = new UnityEngine.Mesh { name = "Fill" };
        mesh.vertices = verts;
        mesh.triangles = tris.ToArray();
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        mf.sharedMesh = mesh;
    }
}