using UnityEngine;

// --- NEW: A dedicated container to hold every single procedural setting ---
[System.Serializable]
public class PCGSaveData
{
    public int seed;

    // Mountain Settings
    public int mountainCount;
    public float mountainWidth, mountainSpacing, baseY, amplitude, frequency, stepX;
    public bool clampToBase; public float baseMargin;
    public bool quantizeHeights; public float heightStep; public bool useStairSteps;
    public float groundYOffset; public bool scaleGroundHeight;
    public float[] finishFlagOffset; public float finishEdgeSearchWidth; public float finishFlagFrontInset;

    // Cave Settings
    public int cavesPerMountain;
    public float minDepth, maxDepth, minLength, maxLength, minSpanX, maxSpanX;
    public float mouthHeight, interiorDropScale, insideSurfaceMargin, maxMouthHeightDiff;
    public float minExtraVerticalClear, minCaveThickness, insetToSpanMax, baseClearance, edgeMarginWorld;
    public int edgePadding, maxAttemptsPerCave, roundIterations, maxSmoothedPoints;
    public bool roundCave;

    // Shortcut Settings
    public bool spawnTunnels, spawnBreakableWall;
    public int tunnelsPerMountain, jaggedPoints, maxNudgeSteps;
    public float cutHeightFraction, cutHeightRandomness, gapSize, archDepth, jaggedAmplitude, angledMaxTilt;
    public float minPeakHeight, minTunnelWidth, maxTunnelWidth, wallEntranceOffsetX, wallVerticalOffset, overlapNudge;

    // Obstacle Settings
    public bool spawnObstacles, alignToSlope, useOverlapCheck;
    public int obstaclesPerMountain, maxAttemptsPerObstacle;
    public float obsEdgePaddingX, obstacleYOffset, minSpacing, avoidSpawnPointsRadius, overlapCheckRadius;
}

[System.Serializable]
public class PlayerData
{
    public float[] playerPosition;
    public float[] ballPosition;
    public int sceneIndex;
    public int strokes;
    public float time;

    // The new container we just built
    public PCGSaveData pcgData;

    // Constructor saves positions AND extracts the current map state
    public PlayerData(Transform playerTransform, Transform ballTransform, int currentSceneIndex, int currentStrokes, float currentTime, PerlinMountain2D pcg)
    {
        sceneIndex = currentSceneIndex;
        strokes = currentStrokes;
        time = currentTime;

        playerPosition = new float[3] { playerTransform.position.x, playerTransform.position.y, playerTransform.position.z };
        ballPosition = new float[3] { ballTransform.position.x, ballTransform.position.y, ballTransform.position.z };

        // Safely extract ALL PCG data if the generator exists in the scene
        if (pcg != null)
        {
            pcgData = new PCGSaveData();

            // Base Mountain Settings
            pcgData.seed = pcg.seed;
            pcgData.mountainCount = pcg.mountainCount;
            pcgData.mountainWidth = pcg.mountainWidth;
            pcgData.mountainSpacing = pcg.mountainSpacing;
            pcgData.baseY = pcg.baseY;
            pcgData.amplitude = pcg.amplitude;
            pcgData.frequency = pcg.frequency;
            pcgData.stepX = pcg.stepX;
            pcgData.clampToBase = pcg.clampToBase;
            pcgData.baseMargin = pcg.baseMargin;
            pcgData.quantizeHeights = pcg.quantizeHeights;
            pcgData.heightStep = pcg.heightStep;
            pcgData.useStairSteps = pcg.useStairSteps;
            pcgData.groundYOffset = pcg.groundYOffset;
            pcgData.scaleGroundHeight = pcg.scaleGroundHeight;
            pcgData.finishFlagOffset = new float[3] { pcg.finishFlagOffset.x, pcg.finishFlagOffset.y, pcg.finishFlagOffset.z };
            pcgData.finishEdgeSearchWidth = pcg.finishEdgeSearchWidth;
            pcgData.finishFlagFrontInset = pcg.finishFlagFrontInset;

            // Caves
            if (pcg.caveGenerator != null)
            {
                pcgData.cavesPerMountain = pcg.caveGenerator.cavesPerMountain;
                pcgData.minDepth = pcg.caveGenerator.minDepth;
                pcgData.maxDepth = pcg.caveGenerator.maxDepth;
                pcgData.minLength = pcg.caveGenerator.minLength;
                pcgData.maxLength = pcg.caveGenerator.maxLength;
                pcgData.minSpanX = pcg.caveGenerator.minSpanX;
                pcgData.maxSpanX = pcg.caveGenerator.maxSpanX;
                pcgData.mouthHeight = pcg.caveGenerator.mouthHeight;
                pcgData.interiorDropScale = pcg.caveGenerator.interiorDropScale;
                pcgData.insideSurfaceMargin = pcg.caveGenerator.insideSurfaceMargin;
                pcgData.maxMouthHeightDiff = pcg.caveGenerator.maxMouthHeightDiff;
                pcgData.minExtraVerticalClear = pcg.caveGenerator.minExtraVerticalClear;
                pcgData.minCaveThickness = pcg.caveGenerator.minCaveThickness;
                pcgData.insetToSpanMax = pcg.caveGenerator.insetToSpanMax;
                pcgData.baseClearance = pcg.caveGenerator.baseClearance;
                pcgData.edgePadding = pcg.caveGenerator.edgePadding;
                pcgData.edgeMarginWorld = pcg.caveGenerator.edgeMarginWorld;
                pcgData.maxAttemptsPerCave = pcg.caveGenerator.maxAttemptsPerCave;
                pcgData.roundCave = pcg.caveGenerator.roundCave;
                pcgData.roundIterations = pcg.caveGenerator.roundIterations;
                pcgData.maxSmoothedPoints = pcg.caveGenerator.maxSmoothedPoints;
            }

            // Shortcuts
            if (pcg.shortcutGenerator != null)
            {
                pcgData.spawnTunnels = pcg.shortcutGenerator.spawnTunnels;
                pcgData.tunnelsPerMountain = pcg.shortcutGenerator.tunnelsPerMountain;
                pcgData.cutHeightFraction = pcg.shortcutGenerator.cutHeightFraction;
                pcgData.cutHeightRandomness = pcg.shortcutGenerator.cutHeightRandomness;
                pcgData.gapSize = pcg.shortcutGenerator.gapSize;
                pcgData.archDepth = pcg.shortcutGenerator.archDepth;
                pcgData.jaggedAmplitude = pcg.shortcutGenerator.jaggedAmplitude;
                pcgData.jaggedPoints = pcg.shortcutGenerator.jaggedPoints;
                pcgData.angledMaxTilt = pcg.shortcutGenerator.angledMaxTilt;
                pcgData.minPeakHeight = pcg.shortcutGenerator.minPeakHeight;
                pcgData.minTunnelWidth = pcg.shortcutGenerator.minTunnelWidth;
                pcgData.maxTunnelWidth = pcg.shortcutGenerator.maxTunnelWidth;
                pcgData.spawnBreakableWall = pcg.shortcutGenerator.spawnBreakableWall;
                pcgData.wallEntranceOffsetX = pcg.shortcutGenerator.wallEntranceOffsetX;
                pcgData.wallVerticalOffset = pcg.shortcutGenerator.wallVerticalOffset;
                pcgData.overlapNudge = pcg.shortcutGenerator.overlapNudge;
                pcgData.maxNudgeSteps = pcg.shortcutGenerator.maxNudgeSteps;
            }

            // Obstacles
            if (pcg.obstaclePlacer != null)
            {
                pcgData.spawnObstacles = pcg.obstaclePlacer.spawnObstacles;
                pcgData.obstaclesPerMountain = pcg.obstaclePlacer.obstaclesPerMountain;
                pcgData.obsEdgePaddingX = pcg.obstaclePlacer.edgePaddingX;
                pcgData.obstacleYOffset = pcg.obstaclePlacer.yOffset;
                pcgData.minSpacing = pcg.obstaclePlacer.minSpacing;
                pcgData.maxAttemptsPerObstacle = pcg.obstaclePlacer.maxAttemptsPerObstacle;
                pcgData.alignToSlope = pcg.obstaclePlacer.alignToSlope;
                pcgData.avoidSpawnPointsRadius = pcg.obstaclePlacer.avoidSpawnPointsRadius;
                pcgData.useOverlapCheck = pcg.obstaclePlacer.useOverlapCheck;
                pcgData.overlapCheckRadius = pcg.obstaclePlacer.overlapCheckRadius;
            }
        }
    }
}