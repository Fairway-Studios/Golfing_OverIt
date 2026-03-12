using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates foddian-style shortcuts on procedural mountains.
///
/// Two shortcut types:
///
/// SADDLE CUT — carves a gap through the highest peak on the mountain.
///   Looks like a tempting shortcut over the top, but the narrow passage
///   makes it easy to slip back down. Width is randomised — sometimes
///   wide and obvious, sometimes narrow and deceptive.
///
///   ?????   ?????
///   ?????   ?????
///       ?????
///
/// NARROW LEDGE — carves a thin flat (or subtly downward-sloping) shelf
///   into a steep section of the mountain. Tempting resting spot mid-climb,
///   but sloped ledges punish the player by sliding them back.
///
///        ????
///   ?????????
///        ????
///
/// Hook-up (already in PerlinMountain2D.Regenerate):
///   shortcutGenerator.ApplyShortcuts(surface, baseY, cursorX, mountainWidth, seed + 5000 + 100*m);
/// </summary>
public class ShortcutGenerator : MonoBehaviour
{
    [Header("Generation")]
    [Tooltip("Minimum number of shortcuts to place per mountain.")]
    [Min(0)] public int minShortcutsPerMountain = 1;
    [Tooltip("Maximum number of shortcuts to place per mountain.")]
    [Min(1)] public int maxShortcutsPerMountain = 2;

    [Header("Saddle Cut")]
    [Tooltip("Minimum width of the saddle gap (narrow = deceptive/hard).")]
    public float saddleMinWidth = 2f;
    [Tooltip("Maximum width of the saddle gap (wide = obvious/easier).")]
    public float saddleMaxWidth = 6f;
    [Tooltip("How deep the saddle cuts down from the peak. " +
             "Larger = more dramatic gap.")]
    public float saddleDepth = 3f;
    [Tooltip("Minimum peak height above baseY to be eligible for a saddle cut.")]
    public float saddleMinPeakHeight = 4f;

    [Header("Narrow Ledge")]
    [Tooltip("Width of the ledge platform.")]
    public float ledgeMinWidth = 3f;
    public float ledgeMaxWidth = 7f;
    [Tooltip("Depth the ledge is cut into the mountain face (how far it sticks out).")]
    public float ledgeDepth = 1.5f;
    [Tooltip("Chance (0-1) that a ledge slopes downward — punishing the player.")]
    [Range(0f, 1f)] public float trapLedgeChance = 0.5f;
    [Tooltip("How much a trap ledge slopes downward (world units drop across ledge width).")]
    public float trapLedgeSlope = 0.4f;
    [Tooltip("Minimum height above baseY for a point to be eligible for a ledge.")]
    public float ledgeMinHeight = 3f;
    [Tooltip("Skip this many points from each mountain edge when placing ledges.")]
    public int ledgeEdgePadding = 10;

    [Header("Attempts")]
    [Range(1, 50)] public int maxAttempts = 20;

    [Header("Debug Visual")]
    public bool drawDebugLine = true;
    public Color saddleDebugColor = Color.red;
    public Color ledgeDebugColor = new Color(1f, 0.5f, 0f); // orange
    [Min(0.05f)] public float debugWidth = 0.3f;
    public int debugSortingOrder = 50;

    [Header("Diagnostics")]
    public bool logDiagnostics = false;

    Transform _debugRoot;
    readonly List<GameObject> _debugLines = new();

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    public void ApplyShortcuts(
        List<Vector3> surface,
        float baseY,
        float mountainStartX,
        float mountainWidth,
        int seed)
    {
        if (surface == null || surface.Count < 20) return;

        var rng = new System.Random(seed);
        float mountainEndX = mountainStartX + mountainWidth;

        int count = rng.Next(minShortcutsPerMountain, maxShortcutsPerMountain + 1);

        // Build a pool of shortcut types, pick randomly
        // We attempt each type and stop once we've placed enough
        var types = new List<int> { 0, 1 }; // 0 = saddle, 1 = ledge
        Shuffle(types, rng);

        int placed = 0;
        foreach (int type in types)
        {
            if (placed >= count) break;

            bool success = type == 0
                ? TryPlaceSaddle(surface, baseY, mountainStartX, mountainEndX, rng)
                : TryPlaceLedge(surface, baseY, mountainStartX, mountainEndX, rng);

            if (success) placed++;
        }

        // If we haven't hit count yet, try both types again with more attempts
        for (int i = 0; i < maxAttempts && placed < count; i++)
        {
            bool success = rng.NextDouble() < 0.5f
                ? TryPlaceSaddle(surface, baseY, mountainStartX, mountainEndX, rng)
                : TryPlaceLedge(surface, baseY, mountainStartX, mountainEndX, rng);
            if (success) placed++;
        }

        if (logDiagnostics)
            Debug.Log($"[ShortcutGen] Placed {placed}/{count} shortcuts on mountain x=[{mountainStartX:F0},{mountainEndX:F0}]");
    }

    public void ClearGeneratedVisuals()
    {
        for (int i = _debugLines.Count - 1; i >= 0; i--)
        {
            var go = _debugLines[i];
            if (!go) continue;
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }
        _debugLines.Clear();

        if (_debugRoot != null)
        {
            var rootGO = _debugRoot.gameObject;
            _debugRoot = null;
            if (rootGO != null)
            {
                if (Application.isPlaying) Destroy(rootGO);
                else DestroyImmediate(rootGO);
            }
        }
    }

    // -----------------------------------------------------------------------
    // SADDLE CUT
    // Finds the highest point on the surface, carves a gap of random width
    // centred on it, cutting down by saddleDepth.
    // -----------------------------------------------------------------------

    bool TryPlaceSaddle(
        List<Vector3> surface,
        float baseY,
        float mountainStartX,
        float mountainEndX,
        System.Random rng)
    {
        // Find the highest surface point (excluding edge padding)
        int peakIdx = -1;
        float peakY = float.MinValue;

        int start = 5;
        int end = surface.Count - 6;
        if (end <= start) return false;

        for (int i = start; i <= end; i++)
        {
            float x = surface[i].x;
            if (x < mountainStartX + 5f || x > mountainEndX - 5f) continue;
            if (surface[i].y > peakY)
            {
                peakY = surface[i].y;
                peakIdx = i;
            }
        }

        if (peakIdx < 0) return false;
        if (peakY - baseY < saddleMinPeakHeight) return false;

        float peakX = surface[peakIdx].x;
        float saddleW = Mathf.Lerp(saddleMinWidth, saddleMaxWidth, (float)rng.NextDouble());
        float halfW = saddleW * 0.5f;
        float saddleY = peakY - saddleDepth;

        // Clamp saddleY so it doesn't go below baseY + margin
        saddleY = Mathf.Max(saddleY, baseY + 0.5f);

        float entryX = peakX - halfW;
        float exitX = peakX + halfW;

        // Clamp to mountain bounds
        entryX = Mathf.Max(entryX, mountainStartX + 2f);
        exitX = Mathf.Min(exitX, mountainEndX - 2f);
        if (exitX - entryX < 1f) return false;

        // Find surface indices bracketing entryX and exitX
        int entryIdx = FindIndexJustBefore(surface, entryX);
        int exitIdx = FindIndexJustAfter(surface, exitX);

        if (entryIdx < 0 || exitIdx < 0 || entryIdx >= exitIdx) return false;

        // Sample surface Y at entry and exit for smooth joins
        float entryY = SampleSurfaceY(surface, entryX);
        float exitY = SampleSurfaceY(surface, exitX);

        // Build the saddle shape:
        //   entryX/entryY ? slope down to saddle floor ? slope up to exitX/exitY
        var saddle = new List<Vector3>
        {
            new Vector3(entryX, entryY,  0f),   // join left side of surface
            new Vector3(entryX, saddleY, 0f),   // drop to saddle floor
            new Vector3(exitX,  saddleY, 0f),   // flat saddle bottom
            new Vector3(exitX,  exitY,   0f),   // rise back to surface
        };

        RemoveNearDuplicates(saddle, 0.001f);

        // Splice into surface
        int removeStart = entryIdx + 1;
        int removeCount = exitIdx - entryIdx - 1;
        if (removeCount < 0) removeCount = 0;
        if (removeStart + removeCount > surface.Count) return false;

        surface.RemoveRange(removeStart, removeCount);
        surface.InsertRange(removeStart, saddle);

        if (drawDebugLine) DrawDebugLine(saddle, saddleDebugColor);

        if (logDiagnostics)
            Debug.Log($"[ShortcutGen] Saddle placed at x=[{entryX:F1},{exitX:F1}] " +
                      $"width={saddleW:F1} peakY={peakY:F2} saddleY={saddleY:F2}");

        return true;
    }

    // -----------------------------------------------------------------------
    // NARROW LEDGE
    // Finds a point on the surface that is elevated and has a steep neighbour,
    // carves a horizontal shelf into the slope.
    // Randomly either flat (fair) or slightly downward-sloping (trap).
    // -----------------------------------------------------------------------

    bool TryPlaceLedge(
        List<Vector3> surface,
        float baseY,
        float mountainStartX,
        float mountainEndX,
        System.Random rng)
    {
        // Collect eligible surface points: elevated, inside bounds, not at edges
        var candidates = new List<int>();

        int start = Mathf.Clamp(ledgeEdgePadding, 0, surface.Count - 2);
        int end = Mathf.Clamp(surface.Count - ledgeEdgePadding - 1, 0, surface.Count - 2);

        for (int i = start; i <= end; i++)
        {
            float x = surface[i].x;
            float y = surface[i].y;

            if (x < mountainStartX + 4f || x > mountainEndX - 4f) continue;
            if (y - baseY < ledgeMinHeight) continue;

            candidates.Add(i);
        }

        if (candidates.Count == 0) return false;

        // Shuffle and try each candidate
        Shuffle(candidates, rng);

        foreach (int anchorIdx in candidates)
        {
            float anchorX = surface[anchorIdx].x;
            float anchorY = surface[anchorIdx].y;

            float ledgeW = Mathf.Lerp(ledgeMinWidth, ledgeMaxWidth, (float)rng.NextDouble());
            bool isTrap = rng.NextDouble() < trapLedgeChance;

            // Place the ledge to the LEFT of anchor (sticks out from a rightward-rising face)
            // or RIGHT — randomly pick direction
            bool goLeft = rng.NextDouble() < 0.5f;

            float ledgeStartX, ledgeEndX;
            if (goLeft)
            {
                ledgeEndX = anchorX;
                ledgeStartX = anchorX - ledgeW;
            }
            else
            {
                ledgeStartX = anchorX;
                ledgeEndX = anchorX + ledgeW;
            }

            // Clamp to mountain
            ledgeStartX = Mathf.Max(ledgeStartX, mountainStartX + 1f);
            ledgeEndX = Mathf.Min(ledgeEndX, mountainEndX - 1f);
            if (ledgeEndX - ledgeStartX < 1.5f) continue;

            // Ledge Y: sits at anchorY - ledgeDepth (cut into the slope)
            float ledgeBaseY = anchorY - ledgeDepth;
            ledgeBaseY = Mathf.Max(ledgeBaseY, baseY + 0.3f);

            // Trap ledge slopes downward in the direction of travel (left to right)
            float ledgeStartY = ledgeBaseY;
            float ledgeEndY = isTrap ? ledgeBaseY - trapLedgeSlope : ledgeBaseY;

            // Find bracketing indices
            int startIdx = FindIndexJustBefore(surface, ledgeStartX);
            int endIdx = FindIndexJustAfter(surface, ledgeEndX);

            if (startIdx < 0 || endIdx < 0 || startIdx >= endIdx) continue;

            // Sample surface at ledge edges for smooth joins
            float surfStartY = SampleSurfaceY(surface, ledgeStartX);
            float surfEndY = SampleSurfaceY(surface, ledgeEndX);

            // The ledge must actually be BELOW the current surface (cutting INTO it)
            if (ledgeStartY >= surfStartY - 0.2f) continue;
            if (ledgeEndY >= surfEndY - 0.2f) continue;

            // Build ledge shape:
            // Drop from surface at ledgeStartX down to ledge level,
            // run along the ledge (flat or sloped),
            // rise back up to surface at ledgeEndX
            var ledge = new List<Vector3>
            {
                new Vector3(ledgeStartX, surfStartY,  0f),  // surface join left
                new Vector3(ledgeStartX, ledgeStartY, 0f),  // drop to ledge
                new Vector3(ledgeEndX,   ledgeEndY,   0f),  // ledge surface (flat or sloped)
                new Vector3(ledgeEndX,   surfEndY,    0f),  // rise back to surface
            };

            RemoveNearDuplicates(ledge, 0.001f);

            int removeStart = startIdx + 1;
            int removeCount = endIdx - startIdx - 1;
            if (removeCount < 0) removeCount = 0;
            if (removeStart + removeCount > surface.Count) continue;

            surface.RemoveRange(removeStart, removeCount);
            surface.InsertRange(removeStart, ledge);

            if (drawDebugLine) DrawDebugLine(ledge, ledgeDebugColor);

            if (logDiagnostics)
                Debug.Log($"[ShortcutGen] Ledge placed at x=[{ledgeStartX:F1},{ledgeEndX:F1}] " +
                          $"isTrap={isTrap} ledgeY=[{ledgeStartY:F2},{ledgeEndY:F2}] anchorY={anchorY:F2}");

            return true;
        }

        return false;
    }

    // -----------------------------------------------------------------------
    // Index helpers
    // -----------------------------------------------------------------------

    /// <summary>Last index whose X is <= targetX (splice point on the left).</summary>
    int FindIndexJustBefore(List<Vector3> surface, float targetX)
    {
        int best = -1;
        for (int i = 0; i < surface.Count; i++)
        {
            if (surface[i].x <= targetX + 0.001f) best = i;
            else break;
        }
        return best;
    }

    /// <summary>First index whose X is >= targetX (splice point on the right).</summary>
    int FindIndexJustAfter(List<Vector3> surface, float targetX)
    {
        for (int i = 0; i < surface.Count; i++)
        {
            if (surface[i].x >= targetX - 0.001f) return i;
        }
        return -1;
    }

    // -----------------------------------------------------------------------
    // Debug visual
    // -----------------------------------------------------------------------

    void DrawDebugLine(List<Vector3> pts, Color color)
    {
        if (pts == null || pts.Count < 2) return;
        EnsureDebugRoot();
        var go = new GameObject($"ShortcutDebug_{_debugLines.Count}");
        go.transform.SetParent(_debugRoot, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false; lr.loop = false;
        lr.positionCount = pts.Count;
        lr.widthMultiplier = debugWidth;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = color; lr.endColor = color;
        lr.sortingOrder = debugSortingOrder;
        lr.numCapVertices = 4; lr.numCornerVertices = 4;
        lr.SetPositions(pts.ToArray());
        _debugLines.Add(go);
    }

    void EnsureDebugRoot()
    {
        if (_debugRoot != null) return;
        var root = new GameObject("ShortcutDebug_Root");
        root.transform.SetParent(transform, false);
        _debugRoot = root.transform;
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    float SampleSurfaceY(List<Vector3> surface, float x)
    {
        for (int i = 1; i < surface.Count; i++)
        {
            Vector3 a = surface[i - 1], b = surface[i];
            float dx = b.x - a.x;
            if (dx <= 0.0001f) continue;
            if (a.x <= x && x <= b.x)
                return Mathf.Lerp(a.y, b.y, Mathf.InverseLerp(a.x, b.x, x));
        }
        float bestY = surface[0].y, bestD = Mathf.Abs(surface[0].x - x);
        for (int i = 1; i < surface.Count; i++)
        {
            float d = Mathf.Abs(surface[i].x - x);
            if (d < bestD) { bestD = d; bestY = surface[i].y; }
        }
        return bestY;
    }

    void RemoveNearDuplicates(List<Vector3> pts, float eps)
    {
        if (pts == null || pts.Count < 2) return;
        for (int i = pts.Count - 2; i >= 0; i--)
            if ((pts[i + 1] - pts[i]).sqrMagnitude <= eps * eps)
                pts.RemoveAt(i + 1);
    }

    static void Shuffle<T>(List<T> list, System.Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}