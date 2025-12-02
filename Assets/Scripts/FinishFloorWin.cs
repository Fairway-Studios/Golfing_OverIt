using UnityEngine;

public class FinishFloorWin : MonoBehaviour
{
    [SerializeField] private string golfBallTag = "GolfBall";
    [SerializeField] private float requiredStayTime = 1.5f; // how long the ball must rest
    [SerializeField] private GameObject winUI;              // assign in Inspector

    private float _stayTimer = 0f;
    private bool _ballOnFloor = false;

    private void Update()
    {
        // If ball is currently on the floor, count up
        if (_ballOnFloor)
        {
            _stayTimer += Time.deltaTime;

            if (_stayTimer >= requiredStayTime)
            {
                // Show the Win UI
                if (winUI != null)
                    winUI.SetActive(true);

                // Optional: stop checking after win
                enabled = false;
            }
        }
        else
        {
            // If ball is not touching, slowly reset timer
            if (_stayTimer > 0f)
                _stayTimer = 0f;
        }

        // Reset flag every frame, will be set again in OnCollisionStay
        _ballOnFloor = false;
    }

    private void OnCollisionStay(Collision collision)
    {
        if (collision.collider.CompareTag(golfBallTag))
        {
            _ballOnFloor = true;
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision.collider.CompareTag(golfBallTag))
        {
            _ballOnFloor = false;
            _stayTimer = 0f;
        }
    }
}
