using UnityEngine;

public class BreakableWall : MonoBehaviour
{
    [Header("Root Only - Damage States")]
    [SerializeField] private GameObject intactState;
    [SerializeField] private GameObject crackedState;
    [SerializeField] private GameObject heavyCrackedState;

    [Header("Break Rules")]
    [SerializeField] private float hitThreshold = 6f;
    [SerializeField] private bool requireBallTag = true;
    [SerializeField] private string ballTag = "Ball";

    [Header("Setup")]
    [SerializeField] private bool isChildCollisionForwarder = false;
    [SerializeField] private BreakableWall rootWall;

    [Header("Spawn Overlap Fix")]
    [SerializeField] private bool resolveOverlapOnStart = false;
    [SerializeField] private int maxShiftAttempts = 20;
    [SerializeField] private Vector2 overlapPadding = new Vector2(0.15f, 0.05f);

    private int currentState = 0;
    private bool isBroken = false;

    private Transform obstaclesRoot;
    private Transform breakableWallsRoot;

    private void Awake()
    {
        if (isChildCollisionForwarder)
            return;

        ShowState(0);
    }

    private void Start()
    {
        if (isChildCollisionForwarder)
            return;

        if (resolveOverlapOnStart)
            ResolveInitialOverlap(pushRight: true);
    }

    public void InitializeSceneRoots(Transform obstacleRootTransform, Transform breakableWallsRootTransform)
    {
        obstaclesRoot = obstacleRootTransform;
        breakableWallsRoot = breakableWallsRootTransform;
    }

    public void ResolvePlacementNow(bool pushRight = true)
    {
        if (isChildCollisionForwarder)
            return;

        ResolveInitialOverlap(pushRight);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (isChildCollisionForwarder)
        {
            if (rootWall != null)
                rootWall.ReceiveCollision(collision);
            return;
        }

        ReceiveCollision(collision);
    }

    public void ReceiveCollision(Collision2D collision)
    {
        if (isBroken) return;

        if (requireBallTag && !collision.gameObject.CompareTag(ballTag))
            return;

        if (collision.relativeVelocity.magnitude >= hitThreshold)
            AdvanceDamageState();
    }

    private void AdvanceDamageState()
    {
        currentState++;

        if (currentState == 1) ShowState(1);
        else if (currentState == 2) ShowState(2);
        else BreakFully();
    }

    private void ShowState(int stateIndex)
    {
        if (intactState != null) intactState.SetActive(stateIndex == 0);
        if (crackedState != null) crackedState.SetActive(stateIndex == 1);
        if (heavyCrackedState != null) heavyCrackedState.SetActive(stateIndex == 2);
    }

    private void BreakFully()
    {
        isBroken = true;
        if (intactState != null) intactState.SetActive(false);
        if (crackedState != null) crackedState.SetActive(false);
        if (heavyCrackedState != null) heavyCrackedState.SetActive(false);
        gameObject.SetActive(false);
    }

    private void ResolveInitialOverlap(bool pushRight)
    {
        Collider2D activeCol = GetActiveStateCollider();
        if (activeCol == null)
            return;

        for (int attempts = 0; attempts < maxShiftAttempts; attempts++)
        {
            // Re-fetch bounds every iteration — position changed last loop.
            Collider2D overlapping = GetFirstRelevantOverlap(activeCol);
            if (overlapping == null)
                return; // clean — no more overlaps

            Bounds myBounds = activeCol.bounds;
            Bounds otherBounds = overlapping.bounds;

            float myHalfWidth = myBounds.extents.x;
            float targetWorldX;

            if (pushRight)
                targetWorldX = otherBounds.max.x + myHalfWidth + overlapPadding.x;
            else
                targetWorldX = otherBounds.min.x - myHalfWidth - overlapPadding.x;

            Vector3 pos = transform.position;
            pos.x = targetWorldX;
            transform.position = pos;

            // Physics2D bounds update is deferred; sync manually so the next
            // OverlapBoxAll call sees the wall's new position immediately.
            Physics2D.SyncTransforms();
        }
    }

    private Collider2D GetActiveStateCollider()
    {
        if (intactState != null && intactState.activeInHierarchy)
        {
            Collider2D c = intactState.GetComponent<Collider2D>();
            if (c != null) return c;
        }
        if (crackedState != null && crackedState.activeInHierarchy)
        {
            Collider2D c = crackedState.GetComponent<Collider2D>();
            if (c != null) return c;
        }
        if (heavyCrackedState != null && heavyCrackedState.activeInHierarchy)
        {
            Collider2D c = heavyCrackedState.GetComponent<Collider2D>();
            if (c != null) return c;
        }
        return null;
    }

    private Collider2D GetFirstRelevantOverlap(Collider2D activeCol)
    {
        Bounds b = activeCol.bounds;

        Vector2 size = new Vector2(
            b.size.x + overlapPadding.x,
            b.size.y + overlapPadding.y
        );

        Collider2D[] hits = Physics2D.OverlapBoxAll(b.center, size, 0f);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null) continue;
            if (IsOwnCollider(hit)) continue;  // skip every part of this wall
            if (!IsRelevantOverlap(hit)) continue;  // skip unrelated objects

            return hit;
        }

        return null;
    }

    private bool IsRelevantOverlap(Collider2D hit)
    {
        Transform t = hit.transform;

        if (obstaclesRoot != null && t.IsChildOf(obstaclesRoot))
            return true;

        if (breakableWallsRoot != null && t.IsChildOf(breakableWallsRoot))
            return true;

        return false;
    }

    // Returns true for ANY collider that belongs to this wall —
    // the root or any descendant (intact / cracked / heavyCracked children and
    // their children). Previously only checked col.transform == transform (the
    // root), which missed colliders living on child GameObjects, causing the
    // wall to treat its own collider as an obstacle and fling itself sideways.
    private bool IsOwnCollider(Collider2D col)
    {
        // Is it the root itself?
        if (col.transform == transform)
            return true;

        // Is it anywhere inside this wall's hierarchy?
        if (col.transform.IsChildOf(transform))
            return true;

        return false;
    }
}