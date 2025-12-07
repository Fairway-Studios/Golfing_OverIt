using UnityEngine;

public class FinishFloorWin : MonoBehaviour
{
    [SerializeField] private string golfBallTag = "GolfBall";
    [SerializeField] private float requiredStayTime = 1.5f; // how long the ball must rest
    [SerializeField] private GameObject winUI;              // assign in Inspector
    [SerializeField] private GameObject anaglyphUI;
    [SerializeField] private GameObject singleplayerUI;

    private float _stayTimer = 0f;
    private bool _ballOnFloor = false;

    private void Update()
    {
        // Only count while the ball is on the floor
        if (!_ballOnFloor)
            return;

        _stayTimer += Time.deltaTime;
        Debug.Log($"[FinishFloor] Ball on floor. Timer: {_stayTimer:0.00}/{requiredStayTime}");

        if (_stayTimer >= requiredStayTime)
        {
            Debug.Log("[FinishFloor] WIN CONDITION MET — Attempting to enable WinUI.");

            if (winUI != null)
            {
                winUI.SetActive(true);
                if (anaglyphUI != null)
                    anaglyphUI.SetActive(false);
                singleplayerUI.SetActive(false);
                Debug.Log("[FinishFloor] WinUI enabled successfully.");
            }
            else
            {
                Debug.LogError("[FinishFloor] ERROR: winUI reference is NULL! Assign a scene UI object.");
            }

            enabled = false; // stop checking
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.collider.CompareTag(golfBallTag))
        {
            Debug.Log("[FinishFloor] Ball ENTERED the floor (Enter2D).");
            _ballOnFloor = true;
            _stayTimer = 0f; // start fresh when it first lands
        }
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (collision.collider.CompareTag(golfBallTag))
        {
            // We don't need to log this every frame anymore, just be sure flag stays true
            _ballOnFloor = true;
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.collider.CompareTag(golfBallTag))
        {
            Debug.Log("[FinishFloor] Ball EXITED the floor (Exit2D). Timer reset.");
            _ballOnFloor = false;
            _stayTimer = 0f;
        }
    }
}
