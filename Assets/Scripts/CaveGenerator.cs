using System;
using System.Collections.Generic;
using UnityEngine;

public class CaveGenerator : MonoBehaviour
{
    [Header("Cave Count")]
    public int cavesPerMountain = 1;

    [Header("Cave Size")]
    public float minDepth = 3f;
    public float maxDepth = 8f;

    public float minLength = 4f;   // horizontal inset range
    public float maxLength = 10f;

    [Header("Span Along Surface (Entry -> Exit)")]
    public float minSpanX = 6f;
    public float maxSpanX = 14f;

    [Header("Shape")]
    public float mouthHeight = 1f;
    public float interiorDropScale = 0.2f;
    public float insideSurfaceMargin = 0.08f;

    [Header("Quality Filters")]
    public float maxMouthHeightDiff = 6f;
    public float minExtraVerticalClear = 0.25f;
    public float minCaveThickness = 1.0f;

    [Tooltip("Prevents huge sideways caves. Inset is clamped to span * this value.")]
    [Range(0.2f, 2.0f)] public float insetToSpanMax = 0.9f;

    [Header("Safety")]
    public float baseClearance = 0.1f;
    public int edgePadding = 18;
    public float edgeMarginWorld = 1.0f;

    [Header("Attempts")]
    [Range(1, 50)] public int maxAttemptsPerCave = 12;

    [Header("Rounded Caves")]
    public bool roundCave = true;
    [Range(0, 5)] public int roundIterations = 2;
    [Range(16, 256)] public int maxSmoothedPoints = 96;

    struct Candidate
    {
        public int entryIdx;
        public int exitIdx;
        public Candidate(int e, int x) { entryIdx = e; exitIdx = x; }
    }

    // Call from PerlinMountain2D:
    // caveGenerator.ApplyFakeCaves(surface, baseY, cursorX, mountainWidth, seed + 100*m);
    public void ApplyFakeCaves(List<Vector3> surface, float baseY, float mountainStartX, float mountainWidth, int seed)
    {
        if (surface == null || surface.Count < 20) return;
        if (cavesPerMountain <= 0) return;

        var rng = new System.Random(seed);

        float mountainEndX = mountainStartX + mountainWidth;
        float centerX = mountainStartX + mountainWidth * 0.5f;
        float minY = baseY + baseClearance;

        for (int c = 0; c < cavesPerMountain; c++)
        {
            // snapshot ORIGINAL surface for sampling/clamping
            var originalSurface = new List<Vector3>(surface);

            // build stable candidates as PAIRS (entry+exit)
            var candidates = BuildCandidates(surface, originalSurface, baseY, minY, mountainStartX, mountainEndX, centerX);

            if (candidates.Count == 0)
                continue;

            bool placed = false;

            for (int t = 0; t < maxAttemptsPerCave; t++)
            {
                var pick = candidates[rng.Next(0, candidates.Count)];
                if (TryBuildAndInsertCave(surface, originalSurface, baseY, minY,
                    mountainStartX, mountainEndX, centerX, pick.entryIdx, pick.exitIdx, rng))
                {
                    placed = true;
                    break;
                }
            }

            if (!placed)
            {
                // skip quietly
            }
        }
    }

    // ---------------- candidates as PAIRS ----------------

    List<Candidate> BuildCandidates(
        List<Vector3> surface,
        List<Vector3> originalSurface,
        float baseY,
        float minY,
        float mountainStartX,
        float mountainEndX,
        float centerX)
    {
        var candidates = new List<Candidate>();

        int start = Mathf.Clamp(edgePadding, 0, surface.Count - 2);
        int end = Mathf.Clamp(surface.Count - edgePadding - 10, 0, surface.Count - 2);
        if (end <= start) return candidates;

        float spanProbe = (minSpanX + maxSpanX) * 0.5f;

        for (int entryIdx = start; entryIdx < end; entryIdx++)
        {
            // must be forward-x segment (ignore vertical stair)
            if (surface[entryIdx + 1].x - surface[entryIdx].x <= 0.0001f)
                continue;

            Vector3 entry = surface[entryIdx];

            if (entry.x < mountainStartX + edgeMarginWorld) continue;
            if (entry.x > mountainEndX - edgeMarginWorld) continue;

            int exitIdx = FindExitIndexByX(surface, entryIdx, spanProbe);
            if (exitIdx < 0) continue;
            if (exitIdx <= entryIdx + 2) continue;
            if (exitIdx >= surface.Count - 2) continue;

            Vector3 exit = surface[exitIdx];

            if (exit.x < mountainStartX + edgeMarginWorld) continue;
            if (exit.x > mountainEndX - edgeMarginWorld) continue;

            float span = exit.x - entry.x;
            if (span < minSpanX * 0.8f) continue;

            if (Mathf.Abs(entry.y - exit.y) > maxMouthHeightDiff)
                continue;

            float verticalClear = Mathf.Min(
                entry.y - (baseY + baseClearance),
                exit.y - (baseY + baseClearance)
            );

            if (verticalClear <= mouthHeight + minExtraVerticalClear)
                continue;

            // inside-roof check at a test inset (prevents “barely inside” spots)
            float dirX = (entry.x < centerX) ? +1f : -1f;
            float insetTest = Mathf.Clamp((minLength + maxLength) * 0.5f, 1f, maxLength);

            // also limit test inset by span so we don't approve crazy sideways interiors
            insetTest = Mathf.Min(insetTest, span * insetToSpanMax);

            float insideXEntry = Mathf.Clamp(entry.x + dirX * insetTest, mountainStartX + edgeMarginWorld, mountainEndX - edgeMarginWorld);
            float insideXExit = Mathf.Clamp(exit.x + dirX * insetTest, mountainStartX + edgeMarginWorld, mountainEndX - edgeMarginWorld);

            float surfInsideEntryY = SampleSurfaceY(originalSurface, insideXEntry) - insideSurfaceMargin;
            float surfInsideExitY = SampleSurfaceY(originalSurface, insideXExit) - insideSurfaceMargin;

            float entryDownY = entry.y - mouthHeight;
            float exitDownY = exit.y - mouthHeight;

            if (entryDownY > surfInsideEntryY) continue;
            if (exitDownY > surfInsideExitY) continue;

            candidates.Add(new Candidate(entryIdx, exitIdx));
        }

        return candidates;
    }

    // ---------------- build attempt ----------------

    bool TryBuildAndInsertCave(
        List<Vector3> surface,
        List<Vector3> originalSurface,
        float baseY,
        float minY,
        float mountainStartX,
        float mountainEndX,
        float centerX,
        int entryIdx,
        int exitIdx,
        System.Random rng)
    {
        if (entryIdx < 0 || exitIdx < 0) return false;
        if (exitIdx <= entryIdx + 2) return false;
        if (exitIdx >= surface.Count) return false;

        Vector3 entry = surface[entryIdx];
        Vector3 exit = surface[exitIdx];

        float span = exit.x - entry.x;
        if (span < minSpanX * 0.8f) return false;

        if (entry.x < mountainStartX + edgeMarginWorld) return false;
        if (exit.x > mountainEndX - edgeMarginWorld) return false;

        if (Mathf.Abs(entry.y - exit.y) > maxMouthHeightDiff)
            return false;

        float verticalClear = Mathf.Min(
            entry.y - (baseY + baseClearance),
            exit.y - (baseY + baseClearance)
        );

        if (verticalClear <= mouthHeight + 0.25f)
            return false;

        float dirX = (entry.x < centerX) ? +1f : -1f;

        float inset = Mathf.Lerp(minLength, maxLength, (float)rng.NextDouble());

        // clamp inset to mountain bounds (both entry + exit)
        float maxInsetFromEntry = (dirX > 0f)
            ? (mountainEndX - edgeMarginWorld) - entry.x
            : entry.x - (mountainStartX + edgeMarginWorld);

        float maxInsetFromExit = (dirX > 0f)
            ? (mountainEndX - edgeMarginWorld) - exit.x
            : exit.x - (mountainStartX + edgeMarginWorld);

        float maxInsetAllowed = Mathf.Min(maxInsetFromEntry, maxInsetFromExit);
        if (maxInsetAllowed <= 1.0f) return false;

        inset = Mathf.Min(inset, maxInsetAllowed);

        // IMPORTANT: also clamp inset by span to stop “weird long shelves”
        inset = Mathf.Min(inset, span * insetToSpanMax);

        float depth = Mathf.Lerp(minDepth, maxDepth, (float)rng.NextDouble());
        float innerDrop = Mathf.Min(verticalClear - mouthHeight, depth * interiorDropScale);

        Vector3 entryDown = new Vector3(entry.x, Mathf.Max(minY, entry.y - mouthHeight), 0f);
        Vector3 exitDown = new Vector3(exit.x, Mathf.Max(minY, exit.y - mouthHeight), 0f);

        float insideXEntry = Mathf.Clamp(entry.x + dirX * inset, mountainStartX + edgeMarginWorld, mountainEndX - edgeMarginWorld);
        float insideXExit = Mathf.Clamp(exit.x + dirX * inset, mountainStartX + edgeMarginWorld, mountainEndX - edgeMarginWorld);

        float deepY = Mathf.Max(minY, Mathf.Min(entryDown.y, exitDown.y) - innerDrop);

        // thickness/headroom test (prevents thin weird caves)
        if (!HasEnoughHeadroom(originalSurface, insideXEntry, insideXExit, deepY))
            return false;

        var loop = new List<Vector3>();

        Vector3 roofIn = new Vector3(insideXEntry, entryDown.y, 0f);
        Vector3 deepA = new Vector3(insideXEntry, deepY, 0f);
        Vector3 deepB = new Vector3(insideXExit, deepY, 0f);
        Vector3 floorOut = new Vector3(insideXExit, exitDown.y, 0f);

        loop.Add(entry);
        loop.Add(entryDown);
        loop.Add(roofIn);
        loop.Add(deepA);
        loop.Add(deepB);
        loop.Add(floorOut);
        loop.Add(exitDown);
        loop.Add(exit);

        if (roundCave && roundIterations > 0)
            loop = SmoothOpenPolyline_Chaikin(loop, roundIterations, maxSmoothedPoints);

        ClampLoopUnderSurface(originalSurface, loop, minY);

        if (!LooksLikeValidCave(loop, entryDown, exitDown))
            return false;

        if (!IsLoopUnderSurface(originalSurface, loop))
            return false;

        int removeCount = exitIdx - entryIdx + 1;
        surface.RemoveRange(entryIdx, removeCount);
        surface.InsertRange(entryIdx, loop);

        return true;
    }

    bool HasEnoughHeadroom(List<Vector3> originalSurface, float insideXEntry, float insideXExit, float deepY)
    {
        float xMid = (insideXEntry + insideXExit) * 0.5f;

        float s1 = SampleSurfaceY(originalSurface, insideXEntry) - insideSurfaceMargin;
        float s2 = SampleSurfaceY(originalSurface, xMid) - insideSurfaceMargin;
        float s3 = SampleSurfaceY(originalSurface, insideXExit) - insideSurfaceMargin;

        float h1 = s1 - deepY;
        float h2 = s2 - deepY;
        float h3 = s3 - deepY;

        return (h1 >= minCaveThickness && h2 >= minCaveThickness && h3 >= minCaveThickness);
    }

    // ---------------- smoothing (Chaikin for open polyline) ----------------

    List<Vector3> SmoothOpenPolyline_Chaikin(List<Vector3> pts, int iterations, int maxPoints)
    {
        if (pts == null || pts.Count < 3) return pts;

        List<Vector3> cur = new List<Vector3>(pts);

        for (int it = 0; it < iterations; it++)
        {
            if (cur.Count >= maxPoints) break;

            var next = new List<Vector3>(Mathf.Min(maxPoints, cur.Count * 2));
            next.Add(cur[0]);

            for (int i = 0; i < cur.Count - 1; i++)
            {
                Vector3 p0 = cur[i];
                Vector3 p1 = cur[i + 1];

                Vector3 Q = Vector3.Lerp(p0, p1, 0.25f);
                Vector3 R = Vector3.Lerp(p0, p1, 0.75f);

                next.Add(Q);
                next.Add(R);

                if (next.Count >= maxPoints - 1) break;
            }

            next.Add(cur[cur.Count - 1]);
            RemoveNearDuplicates(next, 0.0001f);
            cur = next;
        }

        return cur;
    }

    void RemoveNearDuplicates(List<Vector3> pts, float eps)
    {
        if (pts == null || pts.Count < 2) return;

        for (int i = pts.Count - 2; i >= 0; i--)
        {
            if ((pts[i + 1] - pts[i]).sqrMagnitude <= eps * eps)
                pts.RemoveAt(i + 1);
        }
    }

    // ---------------- helpers ----------------

    int FindExitIndexByX(List<Vector3> surface, int entryIdx, float spanX)
    {
        float startX = surface[entryIdx].x;
        float targetX = startX + spanX;

        for (int i = entryIdx + 2; i < surface.Count - 1; i++)
        {
            if (surface[i].x - surface[i - 1].x <= 0.0001f) continue;
            if (surface[i].x >= targetX) return i;
        }

        int fallback = Mathf.Min(surface.Count - 2, entryIdx + 10);
        for (int i = fallback; i < surface.Count - 1; i++)
        {
            if (surface[i].x - surface[i - 1].x > 0.0001f)
                return i;
        }

        return -1;
    }

    void ClampLoopUnderSurface(List<Vector3> originalSurface, List<Vector3> loop, float minY)
    {
        for (int i = 0; i < loop.Count; i++)
        {
            float x = loop[i].x;
            float surfaceY = SampleSurfaceY(originalSurface, x);

            float maxAllowedY = surfaceY - insideSurfaceMargin;

            Vector3 p = loop[i];
            if (p.y > maxAllowedY) p.y = maxAllowedY;
            if (p.y < minY) p.y = minY;

            loop[i] = p;
        }
    }

    bool LooksLikeValidCave(List<Vector3> loop, Vector3 entryDown, Vector3 exitDown)
    {
        float mouthY = Mathf.Min(entryDown.y, exitDown.y);
        float deepest = mouthY;

        for (int i = 0; i < loop.Count; i++)
            deepest = Mathf.Min(deepest, loop[i].y);

        return (mouthY - deepest) > 0.4f;
    }

    bool IsLoopUnderSurface(List<Vector3> originalSurface, List<Vector3> loop)
    {
        for (int i = 0; i < loop.Count; i++)
        {
            float x = loop[i].x;
            float caveY = loop[i].y;
            float surfaceY = SampleSurfaceY(originalSurface, x);

            if (caveY > surfaceY - insideSurfaceMargin * 0.5f)
                return false;
        }
        return true;
    }

    float SampleSurfaceY(List<Vector3> surface, float x)
    {
        for (int i = 1; i < surface.Count; i++)
        {
            Vector3 a = surface[i - 1];
            Vector3 b = surface[i];

            float dx = b.x - a.x;
            if (dx <= 0.0001f) continue;

            if (a.x <= x && x <= b.x)
            {
                float t = Mathf.InverseLerp(a.x, b.x, x);
                return Mathf.Lerp(a.y, b.y, t);
            }
        }

        float bestY = surface[0].y;
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
}
