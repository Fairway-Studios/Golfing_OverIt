using System.Collections.Generic;
using UnityEngine;
using LibTessDotNet;

public class ShortcutGenerator : MonoBehaviour
{
    [Header("Enable")]
    public bool spawnTunnels = true;

    [Header("Count")]
    [Min(1)] public int tunnelsPerMountain = 1;

    [Header("Tunnel Shape")]
    [Range(0.1f, 0.8f)] public float cutHeightFraction = 0.3f;
    [Range(0f, 0.2f)] public float cutHeightRandomness = 0.08f;
    public float gapSize = 2f;

    [Header("Shape Variations")]
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

    [Header("Breakable Wall")]
    public bool spawnBreakableWall = true;
    public GameObject breakableWallPrefab;
    public float wallEntranceOffsetX = 0.4f;
    public float wallVerticalOffset = 0f;
    public string breakableWallsRootName = "BreakableWalls_Root";
    public string obstaclesRootName = "Obstacle_Root";

    // How many units to nudge right per step when resolving overlaps
    public float overlapNudge = 0.3f;
    public int maxNudgeSteps = 30;

    public Vector3 authoredBreakableWallScale = new Vector3(2.153862f, 4.7544f, 1f);

    private int _breakableWallCounter = 0;
    private int _tunnelTopCounter = 0;

    enum TunnelShape { Arch, Jagged, Angled }
    readonly List<GameObject> _spawnedPieces = new();

    // ------------------------------------------------------------------
    public void ApplyShortcuts(List<Vector3> surface, float baseY,
                               float mountainStartX, float mountainWidth, int seed)
    { }

    // ------------------------------------------------------------------
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
        _breakableWallCounter = 0;
        _tunnelTopCounter = 0;

        Transform wallsRoot = transform.Find(breakableWallsRootName);
        if (wallsRoot != null)
        {
            if (Application.isPlaying) Destroy(wallsRoot.gameObject);
            else DestroyImmediate(wallsRoot.gameObject);
        }

        var extras = new List<GameObject>();
        foreach (Transform child in transform)
        {
            if (!child) continue;
            if (child.name.StartsWith("TunnelTop_") || child.name.StartsWith("BreakableWall_"))
                extras.Add(child.gameObject);
        }
        foreach (var go in extras)
        {
            if (!go) continue;
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }
    }

    // ------------------------------------------------------------------
    public void SpawnTunnelForMountain(
        List<Vector3> surfaceLocal, float baseY,
        float mountainStartX, float mountainWidth, int seed,
        GameObject mountainGO,
        Color fillColor, int fillSortingOrder,
        float outlineWidth, Color outlineColor,
        float tessScale, bool tessInvertWinding)
    {
        if (!spawnTunnels) return;
        if (surfaceLocal == null || surfaceLocal.Count < 6) return;
        if (mountainGO == null) return;

        var rng = new System.Random(seed);
        float mountainEndX = mountainStartX + mountainWidth;

        var peaks = FindAllPeaks(surfaceLocal, baseY, mountainStartX, mountainEndX);
        if (peaks.Count == 0) return;

        int placed = 0;
        foreach (int peakIdx in peaks)
        {
            if (placed >= tunnelsPerMountain) break;
            if (TryBuildTunnel(surfaceLocal, baseY, mountainStartX, mountainEndX,
                               mountainGO, fillColor, fillSortingOrder,
                               outlineWidth, outlineColor, tessScale, tessInvertWinding,
                               rng, peakIdx))
                placed++;
        }
    }

    // ------------------------------------------------------------------
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
            fillColor, fillSortingOrder, outlineWidth, outlineColor, tessScale, tessInvertWinding);

        var topGO = new GameObject($"TunnelTop_{_tunnelTopCounter++}");
        topGO.transform.SetParent(mountainGO.transform.parent, false);
        topGO.transform.localPosition = Vector3.zero;
        topGO.transform.localRotation = Quaternion.identity;
        topGO.transform.localScale = Vector3.one;
        _spawnedPieces.Add(topGO);

        BuildTopPiece(topGO, topSurf, fillColor, topPieceSortingOrder,
                      outlineWidth, outlineColor, tessScale, tessInvertWinding);

        if (spawnBreakableWall && breakableWallPrefab != null)
            SpawnWallForTunnel(topGO);

        return true;
    }

    // ------------------------------------------------------------------
    // Places one breakable wall at the bottom-left of the TunnelTop bounds,
    // then nudges it right until it no longer overlaps any obstacle or
    // other breakable wall.
    //
    // bounds.min in world space = bottom-left corner of the tunnel opening.
    // That is the tunnel entrance — exactly where the wall should start.
    void SpawnWallForTunnel(GameObject topGO)
    {
        var topCol = topGO.GetComponent<PolygonCollider2D>();
        if (topCol == null) return;

        Transform wallsRoot = GetOrCreateBreakableWallsRoot();
        Transform obstaclesRoot = FindObstaclesRoot();

        // bounds is always world-space — no TransformPoint needed.
        // bounds.min.x is the far-left of the entire TunnelTop piece, which
        // extends left beyond the actual tunnel opening (it includes the rock
        // above the gap). We want the X of the tunnel entrance specifically.
        //
        // The tunnel entrance is the bottom-left corner of the top piece —
        // i.e. the leftmost point that is also near the bottom of the bounds.
        // We find this by scanning path points in world space and picking the
        // one with the smallest X among those within gapSize of bounds.min.y.
        Bounds b = topCol.bounds;

        Vector2[] path = topCol.GetPath(0);
        float entranceWorldX = b.max.x; // start with max, find true left entrance
        float yThreshold = b.min.y + gapSize * 1.5f; // bottom portion of bounds

        foreach (var pt in path)
        {
            Vector3 wp = topGO.transform.TransformPoint(new Vector3(pt.x, pt.y, 0f));
            if (wp.y <= yThreshold && wp.x < entranceWorldX)
                entranceWorldX = wp.x;
        }

        // Among the same bottom-region points, also find the Y at the entrance.
        // The entrance bottom Y = lowest world Y among those points.
        // Gap centre = entranceBottomY + gapSize * 0.5f (gapSize is in surface units,
        // but topGO has localScale=1 so world units match surface units exactly).
        float entranceBottomY = float.MaxValue;
        foreach (var pt in path)
        {
            Vector3 wp = topGO.transform.TransformPoint(new Vector3(pt.x, pt.y, 0f));
            if (wp.y <= yThreshold && wp.x < (entranceWorldX + 2f))
                if (wp.y < entranceBottomY) entranceBottomY = wp.y;
        }
        if (entranceBottomY == float.MaxValue) entranceBottomY = b.min.y;

        float wallX = entranceWorldX + wallEntranceOffsetX;
        float wallY = entranceBottomY + gapSize * 0.5f + wallVerticalOffset;

        GameObject wall = Instantiate(breakableWallPrefab);
        wall.name = $"BreakableWall_{_breakableWallCounter++}";

        wall.transform.position = new Vector3(wallX, wallY, 0f);
        wall.transform.rotation = Quaternion.identity;

        // Scale so wall world size matches authored size regardless of parent scale
        Vector3 lossy = wallsRoot.lossyScale;
        wall.transform.localScale = new Vector3(
            lossy.x != 0f ? authoredBreakableWallScale.x / lossy.x : authoredBreakableWallScale.x,
            lossy.y != 0f ? authoredBreakableWallScale.y / lossy.y : authoredBreakableWallScale.y,
            lossy.z != 0f ? authoredBreakableWallScale.z / lossy.z : authoredBreakableWallScale.z
        );

        wall.transform.SetParent(wallsRoot, worldPositionStays: true);

        // Give scripts their scene references before resolving
        foreach (var ws in wall.GetComponentsInChildren<BreakableWall>(true))
        {
            if (ws == null) continue;
            ws.InitializeSceneRoots(obstaclesRoot, wallsRoot);
        }

        // Sync physics so OverlapBoxAll sees the wall immediately
        Physics2D.SyncTransforms();

        // Nudge right until not overlapping any obstacle or sibling wall
        NudgeWallClear(wall, obstaclesRoot, wallsRoot);

        _spawnedPieces.Add(wall);
    }

    // ------------------------------------------------------------------
    // Finds the wall's active collider, then steps it right by overlapNudge
    // until no relevant overlap remains (or we hit the step limit).
    void NudgeWallClear(GameObject wall, Transform obstaclesRoot, Transform wallsRoot)
    {
        // Find the active state collider on the wall
        Collider2D wallCol = null;
        foreach (var col in wall.GetComponentsInChildren<Collider2D>(false))
        {
            if (col.gameObject.activeInHierarchy) { wallCol = col; break; }
        }
        if (wallCol == null) return;

        for (int step = 0; step < maxNudgeSteps; step++)
        {
            Physics2D.SyncTransforms();

            Bounds wb = wallCol.bounds;
            Vector2 sz = new Vector2(wb.size.x + 0.05f, wb.size.y + 0.05f);
            var hits = Physics2D.OverlapBoxAll(wb.center, sz, 0f);

            bool overlapping = false;
            foreach (var hit in hits)
            {
                if (hit == null) continue;
                // Skip own colliders
                if (hit.transform == wall.transform) continue;
                if (hit.transform.IsChildOf(wall.transform)) continue;

                // Only care about obstacles and other breakable walls
                bool isObstacle = obstaclesRoot != null && hit.transform.IsChildOf(obstaclesRoot);
                bool isWall = wallsRoot != null && hit.transform.IsChildOf(wallsRoot);
                if (!isObstacle && !isWall) continue;

                overlapping = true;

                // Push our centre to the right of this hit's right edge
                float newX = hit.bounds.max.x + wb.extents.x + 0.05f;
                Vector3 pos = wall.transform.position;
                pos.x = newX;
                wall.transform.position = pos;
                break;
            }

            if (!overlapping) return;
        }
    }

    // ------------------------------------------------------------------
    Transform GetOrCreateBreakableWallsRoot()
    {
        Transform existing = transform.Find(breakableWallsRootName);
        if (existing != null) return existing;

        GameObject root = new GameObject(breakableWallsRootName);
        root.transform.SetParent(transform, false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;
        return root.transform;
    }

    Transform FindObstaclesRoot()
    {
        Transform found = transform.Find(obstaclesRootName);
        if (found != null) return found;
        return transform.Find("Obstacles_Root");
    }

    // ------------------------------------------------------------------
    List<int> FindAllPeaks(List<Vector3> surface, float baseY, float minX, float maxX)
    {
        var result = new List<(int idx, float h)>();
        const int window = 3;

        for (int i = window; i < surface.Count - window; i++)
        {
            float x = surface[i].x, y = surface[i].y;
            if (x < minX + 3f || x > maxX - 3f) continue;
            if (y - baseY < minPeakHeight) continue;

            bool isLocalMax = true;
            for (int w = 1; w <= window && isLocalMax; w++)
                if (surface[i - w].y > y || surface[i + w].y > y)
                    isLocalMax = false;

            if (isLocalMax) result.Add((i, y - baseY));
        }

        result.Sort((a, b) => b.h.CompareTo(a.h));
        var indices = new List<int>(result.Count);
        foreach (var p in result) indices.Add(p.idx);
        return indices;
    }

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
                        float y = Mathf.Clamp(baseCutY + noise,
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

    List<Vector3> BuildBottomSurface(List<Vector3> surface, int leftIdx, int rightIdx,
                                     List<Vector3> cutLine)
    {
        var pts = new List<Vector3>();
        for (int i = 0; i <= leftIdx; i++) pts.Add(surface[i]);
        pts.AddRange(cutLine);
        for (int i = rightIdx; i < surface.Count; i++) pts.Add(surface[i]);
        RemoveNearDuplicates(pts, 0.001f);
        return pts;
    }

    List<Vector3> BuildTopSurface(List<Vector3> surface, int leftIdx, int rightIdx,
                                  List<Vector3> cutLine, float shift)
    {
        var pts = new List<Vector3>();
        for (int i = 0; i < cutLine.Count; i++)
            pts.Add(new Vector3(cutLine[i].x, cutLine[i].y + shift, 0f));
        for (int i = rightIdx - 1; i > leftIdx; i--)
            pts.Add(new Vector3(surface[i].x, surface[i].y + shift, 0f));
        RemoveNearDuplicates(pts, 0.001f);
        return pts;
    }

    void ReplaceMountainWithBottom(GameObject mountainGO, List<Vector3> bottomSurface,
        float baseY, Color fillColor, int fillSortingOrder,
        float outlineWidth, Color outlineColor, float tessScale, bool tessInvertWinding)
    {
        var fillPoly = BuildClosedPolygon(bottomSurface, baseY - 50f);
        var colPoly = BuildClosedPolygon(bottomSurface, baseY);
        CleanPolygon(fillPoly); CleanPolygon(colPoly);

        AddFillMesh(mountainGO, fillPoly, fillColor, fillSortingOrder, tessScale, tessInvertWinding);

        var lr = mountainGO.GetComponent<LineRenderer>();
        if (lr == null) lr = mountainGO.AddComponent<LineRenderer>();
        lr.loop = false; lr.positionCount = bottomSurface.Count;
        lr.useWorldSpace = false; lr.widthMultiplier = outlineWidth;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = outlineColor; lr.endColor = outlineColor;
        lr.SetPositions(bottomSurface.ToArray());

        var col = mountainGO.GetComponent<PolygonCollider2D>();
        if (col == null) col = mountainGO.AddComponent<PolygonCollider2D>();
        var v2 = new Vector2[colPoly.Count];
        for (int i = 0; i < colPoly.Count; i++) v2[i] = colPoly[i];
        col.pathCount = 1; col.SetPath(0, v2);
    }

    void BuildTopPiece(GameObject topGO, List<Vector3> topSurface,
        Color fillColor, int fillSortingOrder,
        float outlineWidth, Color outlineColor, float tessScale, bool tessInvertWinding)
    {
        if (topSurface == null || topSurface.Count < 3) return;

        var fillPoly = new List<Vector3>(topSurface);
        var colPoly = new List<Vector3>(topSurface);
        CleanPolygon(fillPoly); CleanPolygon(colPoly);
        if (fillPoly.Count < 3) return;

        AddFillMesh(topGO, fillPoly, fillColor, fillSortingOrder, tessScale, tessInvertWinding);

        var lr = topGO.AddComponent<LineRenderer>();
        lr.loop = true; lr.positionCount = fillPoly.Count;
        lr.useWorldSpace = false; lr.widthMultiplier = outlineWidth;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = outlineColor; lr.endColor = outlineColor;
        lr.sortingOrder = topPieceOutlineSortingOrder;
        lr.SetPositions(fillPoly.ToArray());

        var col = topGO.AddComponent<PolygonCollider2D>();
        var v2 = new Vector2[colPoly.Count];
        for (int i = 0; i < colPoly.Count; i++) v2[i] = colPoly[i];
        col.pathCount = 1; col.SetPath(0, v2);
    }

    int FindCrossingLeft(List<Vector3> surface, int fromIdx, float targetY)
    {
        float peakX = surface[fromIdx].x, prevY = surface[fromIdx].y;
        for (int i = fromIdx - 1; i >= 0; i--)
        {
            float y = surface[i].y, x = surface[i].x;
            if (peakX - x > maxTunnelWidth * 0.5f) return -1;
            if (y > prevY + 0.2f && y > targetY) return -1;
            if (y <= targetY) return i;
            prevY = y;
        }
        return -1;
    }

    int FindCrossingRight(List<Vector3> surface, int fromIdx, float targetY)
    {
        float peakX = surface[fromIdx].x, prevY = surface[fromIdx].y;
        for (int i = fromIdx + 1; i < surface.Count; i++)
        {
            float y = surface[i].y, x = surface[i].x;
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
        Vector3 a = surface[idxA], b = surface[idxB];
        float dy = b.y - a.y;
        if (Mathf.Abs(dy) < 0.0001f) return a.x;
        return Mathf.Lerp(a.x, b.x, Mathf.Clamp01((targetY - a.y) / dy));
    }

    List<Vector3> BuildClosedPolygon(List<Vector3> surface, float closeY)
    {
        var poly = new List<Vector3>(surface);
        if (Mathf.Abs(surface[surface.Count - 1].y - closeY) > 0.0001f)
            poly.Add(new Vector3(surface[surface.Count - 1].x, closeY, 0f));
        var gs = new Vector3(surface[0].x, closeY, 0f);
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
        if (polygon == null || polygon.Count < 3 || go == null) return;

        var mf = go.GetComponent<MeshFilter>();
        if (mf == null) mf = go.AddComponent<MeshFilter>();
        var mr = go.GetComponent<MeshRenderer>();
        if (mr == null) mr = go.AddComponent<MeshRenderer>();

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

        if (tess.ElementCount <= 0 || tess.Vertices == null || tess.Vertices.Length < 3) return;

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
        if (tris.Count < 3) return;

        if (mf.sharedMesh != null)
        {
            if (Application.isPlaying) Destroy(mf.sharedMesh);
            else DestroyImmediate(mf.sharedMesh);
        }

        var mesh = new UnityEngine.Mesh { name = "Fill" };
        mesh.vertices = verts; mesh.triangles = tris.ToArray();
        mesh.RecalculateBounds(); mesh.RecalculateNormals();
        mf.sharedMesh = mesh;
    }
}