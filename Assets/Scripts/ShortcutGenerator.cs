using System.Collections.Generic;
using UnityEngine;
using LibTessDotNet;

/// <summary>
/// Creates a tunnel shortcut through a mountain peak by splitting it into
/// two separate GameObjects:
///
///   BOTTOM PIECE — the original mountain mesh, capped flat at cutY.
///   TOP PIECE    — everything above cutY, shifted up by gapSize world units.
///
/// The gap between the two pieces IS the tunnel. The ball rolls through it.
/// Both pieces get their own MeshRenderer (fill) and PolygonCollider2D.
///
/// This replaces the original mountainGO fill mesh and collider, so it must
/// be called INSTEAD of the normal fill/collider setup for that mountain,
/// OR after — in which case it destroys and replaces them.
///
/// Hook-up: call from PerlinMountain2D AFTER BuildClosedPolygon but the
/// simplest integration is via ApplyShortcuts (before BuildClosedPolygon)
/// which modifies nothing and instead stores tunnel data, then
/// SpawnTunnelPieces is called after the mountain GO is built.
///
/// Simplest hook-up — add after the collider block in Regenerate():
///   if (shortcutGenerator != null)
///       shortcutGenerator.SpawnTunnelForMountain(
///           surface, baseY, cursorX, mountainWidth,
///           seed + 5000 + 100 * m, mountainGO,
///           fillColor, fillSortingOrder, outlineWidth, mountainColor,
///           tessScale, tessInvertWinding);
/// </summary>
public class ShortcutGenerator : MonoBehaviour
{
    [Header("Enable")]
    public bool spawnTunnels = true;

    [Header("Tunnel Shape")]
    [Tooltip("Where on the peak the cut is made. 0=base, 1=top. 0.3 = lower third.")]
    [Range(0.1f, 0.8f)]
    public float cutHeightFraction = 0.3f;

    [Tooltip("Randomise cut height each generation.")]
    [Range(0f, 0.2f)]
    public float cutHeightRandomness = 0.08f;

    [Tooltip("How far the top piece shifts UP to create the visible gap.")]
    public float gapSize = 1.5f;

    [Header("Peak Requirements")]
    [Tooltip("Peak must be at least this tall above baseY.")]
    public float minPeakHeight = 3f;

    [Tooltip("Minimum horizontal width of the peak at cutY (tunnel width).")]
    public float minTunnelWidth = 3f;

    [Header("Sorting Orders")]
    [Tooltip("Sorting order for the top piece fill mesh.")]
    public int topPieceSortingOrder = -8;

    [Tooltip("Sorting order for the top piece outline.")]
    public int topPieceOutlineSortingOrder = 0;

    [Header("Attempts")]
    [Range(1, 20)] public int maxAttempts = 10;

    readonly List<GameObject> _spawnedPieces = new();

    // -----------------------------------------------------------------------
    // Legacy no-op — keeps existing ApplyShortcuts call compiling
    // -----------------------------------------------------------------------
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
    // Main entry — call after PolygonCollider2D is added to mountainGO
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

        // Find the best peak
        int peakIdx = FindBestPeak(surfaceLocal, baseY, mountainStartX, mountainEndX);
        if (peakIdx < 0)
        {
            //Debug.Log($"[ShortcutGen] No peak found on x=[{mountainStartX:F0},{mountainEndX:F0}]. " + $"Lower minPeakHeight ({minPeakHeight}).");
            return;
        }

        float peakY = surfaceLocal[peakIdx].y;
        float peakH = peakY - baseY;

        // Randomise cut fraction
        float frac = cutHeightFraction
                     + ((float)rng.NextDouble() * 2f - 1f) * cutHeightRandomness;
        frac = Mathf.Clamp(frac, 0.1f, 0.75f);
        float cutY = baseY + peakH * frac;

        // Find left and right crossings at cutY
        int leftIdx = FindCrossingLeft(surfaceLocal, peakIdx, cutY);
        int rightIdx = FindCrossingRight(surfaceLocal, peakIdx, cutY);

        if (leftIdx < 0 || rightIdx < 0)
        {
            //Debug.Log($"[ShortcutGen] Could not find crossings at cutY={cutY:F1}");
            return;
        }

        // Exact X positions of crossings
        float entryX = InterpolateXAtY(surfaceLocal, leftIdx, leftIdx + 1, cutY);
        float exitX = InterpolateXAtY(surfaceLocal, rightIdx, rightIdx - 1, cutY);

        if (exitX - entryX < minTunnelWidth)
        {
            //Debug.Log($"[ShortcutGen] Tunnel too narrow ({exitX - entryX:F1} < {minTunnelWidth})");
            return;
        }

        //Debug.Log($"[ShortcutGen] ? Tunnel: peak=({surfaceLocal[peakIdx].x:F1},{peakY:F1}) " + $"cutY={cutY:F1} entry={entryX:F1} exit={exitX:F1} " + $"width={exitX - entryX:F1} gap={gapSize}");

        // Build bottom polygon — original surface but with peak section capped at cutY
        var bottomSurface = BuildBottomSurface(surfaceLocal, leftIdx, rightIdx, cutY, entryX, exitX);

        // Build top polygon — only the peak cap above cutY, shifted up
        var topSurface = BuildTopSurface(surfaceLocal, leftIdx, rightIdx, cutY, entryX, exitX, gapSize);

        // Replace the original mountainGO fill and collider with the bottom piece
        ReplaceMountainWithBottom(mountainGO, bottomSurface, baseY,
            fillColor, fillSortingOrder, outlineWidth, outlineColor,
            tessScale, tessInvertWinding);

        // Spawn the top piece as a sibling GameObject
        var topGO = new GameObject($"TunnelTop_{_spawnedPieces.Count}");
        topGO.transform.SetParent(mountainGO.transform.parent, false);
        _spawnedPieces.Add(topGO);

        BuildTopPiece(topGO, topSurface, cutY + gapSize,
            fillColor, fillSortingOrder, outlineWidth, outlineColor,
            tessScale, tessInvertWinding);
    }

    // -----------------------------------------------------------------------
    // Build bottom surface — original surface with peak replaced by flat cap at cutY
    // -----------------------------------------------------------------------

    List<Vector3> BuildBottomSurface(
        List<Vector3> surface,
        int leftIdx, int rightIdx,
        float cutY, float entryX, float exitX)
    {
        var pts = new List<Vector3>();

        // Points before the left crossing (left approach, ascending)
        for (int i = 0; i <= leftIdx; i++)
            pts.Add(surface[i]);

        // Cap: entry point at cutY, flat across, exit at cutY
        pts.Add(new Vector3(entryX, cutY, 0f));
        pts.Add(new Vector3(exitX, cutY, 0f));

        // Points after the right crossing (right descend)
        for (int i = rightIdx; i < surface.Count; i++)
            pts.Add(surface[i]);

        RemoveNearDuplicates(pts, 0.001f);
        return pts;
    }

    // -----------------------------------------------------------------------
    // Build top surface — just the peak cap above cutY, shifted up
    // -----------------------------------------------------------------------

    List<Vector3> BuildTopSurface(
    List<Vector3> surface,
    int leftIdx, int rightIdx,
    float cutY, float entryX, float exitX,
    float shift)
    {
        var pts = new List<Vector3>();

        // Start at left cut point
        pts.Add(new Vector3(entryX, cutY + shift, 0f));

        // Add original mountain top points between crossings
        for (int i = leftIdx + 1; i < rightIdx; i++)
            pts.Add(new Vector3(surface[i].x, surface[i].y + shift, 0f));

        // End at right cut point
        pts.Add(new Vector3(exitX, cutY + shift, 0f));

        RemoveNearDuplicates(pts, 0.001f);
        return pts;
    }

    // -----------------------------------------------------------------------
    // Replace mountain fill mesh and collider with bottom piece geometry
    // -----------------------------------------------------------------------

    void ReplaceMountainWithBottom(
        GameObject mountainGO,
        List<Vector3> bottomSurface,
        float baseY,
        Color fillColor, int fillSortingOrder,
        float outlineWidth, Color outlineColor,
        float tessScale, bool tessInvertWinding)
    {
        // Remove existing MeshFilter/MeshRenderer (fill) and LineRenderer (outline)
        var existingMF = mountainGO.GetComponent<MeshFilter>();
        var existingMR = mountainGO.GetComponent<MeshRenderer>();
        var existingLR = mountainGO.GetComponent<LineRenderer>();
        var existingCol = mountainGO.GetComponent<PolygonCollider2D>();

        if (existingMF)
        {
            if (Application.isPlaying) Destroy(existingMF);
            else DestroyImmediate(existingMF);
        }
        if (existingMR)
        {
            if (Application.isPlaying) Destroy(existingMR);
            else DestroyImmediate(existingMR);
        }
        if (existingLR)
        {
            if (Application.isPlaying) Destroy(existingLR);
            else DestroyImmediate(existingLR);
        }
        if (existingCol)
        {
            if (Application.isPlaying) Destroy(existingCol);
            else DestroyImmediate(existingCol);
        }

        // Build closed polygon for fill (extend down for fill depth)
        float fillCloseY = baseY - 50f;
        var fillPoly = BuildClosedPolygon(bottomSurface, fillCloseY);
        var colPoly = BuildClosedPolygon(bottomSurface, baseY);

        CleanPolygon(fillPoly);
        CleanPolygon(colPoly);

        // Fill mesh
        AddFillMesh(mountainGO, fillPoly, fillColor, fillSortingOrder, tessScale, tessInvertWinding);

        // Outline
        var lr = mountainGO.AddComponent<LineRenderer>();
        lr.loop = false;
        lr.positionCount = bottomSurface.Count;
        lr.useWorldSpace = false;
        lr.widthMultiplier = outlineWidth;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = outlineColor;
        lr.endColor = outlineColor;
        lr.SetPositions(bottomSurface.ToArray());

        // Collider
        var col = mountainGO.AddComponent<PolygonCollider2D>();
        var v2 = new Vector2[colPoly.Count];
        for (int i = 0; i < colPoly.Count; i++) v2[i] = colPoly[i];
        col.pathCount = 1;
        col.SetPath(0, v2);
    }

    // -----------------------------------------------------------------------
    // Build the floating top piece GameObject
    // -----------------------------------------------------------------------

    void BuildTopPiece(
        GameObject topGO,
        List<Vector3> topSurface,
        float closeY,
        Color fillColor, int fillSortingOrder,
        float outlineWidth, Color outlineColor,
        float tessScale, bool tessInvertWinding)
    {
        if (topSurface.Count < 3) return;

        var fillPoly = BuildClosedPolygon(topSurface, closeY);
        var colPoly = BuildClosedPolygon(topSurface, closeY);

        CleanPolygon(fillPoly);
        CleanPolygon(colPoly);

        if (fillPoly.Count < 3) return;

        // Fill
        AddFillMesh(topGO, fillPoly, fillColor, topPieceSortingOrder, tessScale, tessInvertWinding);

        // Outline
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

        // Collider — just the underside of the top piece acts as ceiling
        var col = topGO.AddComponent<PolygonCollider2D>();
        var v2 = new Vector2[colPoly.Count];
        for (int i = 0; i < colPoly.Count; i++) v2[i] = colPoly[i];
        col.pathCount = 1;
        col.SetPath(0, v2);
    }

    // -----------------------------------------------------------------------
    // Peak detection
    // -----------------------------------------------------------------------

    int FindBestPeak(List<Vector3> surface, float baseY, float minX, float maxX)
    {
        int bestIdx = -1;
        float bestH = float.MinValue;

        for (int i = 1; i < surface.Count - 1; i++)
        {
            float x = surface[i].x;
            float y = surface[i].y;

            if (x < minX + 3f || x > maxX - 3f) continue;
            if (y - baseY < minPeakHeight) continue;

            // Local max (>=) to handle flat tops
            if (y >= surface[i - 1].y && y >= surface[i + 1].y && y > bestH)
            {
                bestH = y;
                bestIdx = i;
            }
        }

        return bestIdx;
    }

    int FindCrossingLeft(List<Vector3> surface, int fromIdx, float targetY)
    {
        float peakX = surface[fromIdx].x;
        float peakY = surface[fromIdx].y;
        float prevY = peakY;

        for (int i = fromIdx - 1; i >= 0; i--)
        {
            float y = surface[i].y;

            // Stop if we have gone back UP past cutY after descending
            // (means we have crossed a valley and are on another peak)
            if (y > prevY + 0.2f && y > targetY)
                return -1;

            if (y <= targetY)
                return i;

            prevY = y;
        }
        return -1;
    }

    int FindCrossingRight(List<Vector3> surface, int fromIdx, float targetY)
    {
        float peakY = surface[fromIdx].y;
        float prevY = peakY;

        for (int i = fromIdx + 1; i < surface.Count; i++)
        {
            float y = surface[i].y;

            // Stop if we have gone back UP past cutY after descending
            if (y > prevY + 0.5f && y > targetY)
                return -1;

            if (y <= targetY)
                return i;

            prevY = y;
        }
        return -1;
    }

    float InterpolateXAtY(List<Vector3> surface, int idxA, int idxB, float targetY)
    {
        if (idxA < 0 || idxB < 0 || idxA >= surface.Count || idxB >= surface.Count)
            return -1f;
        Vector3 a = surface[idxA], b = surface[idxB];
        float dy = b.y - a.y;
        if (Mathf.Abs(dy) < 0.0001f) return a.x;
        float t = Mathf.Clamp01((targetY - a.y) / dy);
        return Mathf.Lerp(a.x, b.x, t);
    }

    // -----------------------------------------------------------------------
    // Polygon helpers
    // -----------------------------------------------------------------------

    List<Vector3> BuildClosedPolygon(List<Vector3> surface, float closeY)
    {
        var poly = new List<Vector3>(surface);
        var last = surface[surface.Count - 1];
        var first = surface[0];
        if (Mathf.Abs(last.y - closeY) > 0.0001f)
            poly.Add(new Vector3(last.x, closeY, 0f));
        var groundStart = new Vector3(first.x, closeY, 0f);
        if ((poly[poly.Count - 1] - groundStart).sqrMagnitude > 0.0001f)
            poly.Add(groundStart);
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
            if ((pts[i + 1] - pts[i]).sqrMagnitude <= eps * eps)
                pts.RemoveAt(i + 1);
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
                if (Mathf.Abs(cross) <= eps) { pts.RemoveAt(i); removed = true; break; }
            }
            if (!removed) break;
        }
    }

    void AddFillMesh(GameObject go, List<Vector3> polygon, Color color,
                     int sortingOrder, float tessScale, bool tessInvertWinding)
    {
        if (polygon.Count < 3) return;

        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        var mat = new Material(Shader.Find("Sprites/Default")) { color = color };
        mr.sharedMaterial = mat;
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
            verts[i] = new Vector3((float)(tess.Vertices[i].Position.X / s),
                                   (float)(tess.Vertices[i].Position.Y / s), 0f);

        var tris = new List<int>();
        for (int e = 0; e < tess.ElementCount; e++)
        {
            int i0 = tess.Elements[e * 3], i1 = tess.Elements[e * 3 + 1], i2 = tess.Elements[e * 3 + 2];
            if (i0 >= 0 && i1 >= 0 && i2 >= 0) { tris.Add(i0); tris.Add(i1); tris.Add(i2); }
        }

        var mesh = new UnityEngine.Mesh { name = "Fill" };
        mesh.vertices = verts;
        mesh.triangles = tris.ToArray();
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        mf.sharedMesh = mesh;
    }
}