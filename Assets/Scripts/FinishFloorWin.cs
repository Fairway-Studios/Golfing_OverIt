using UnityEngine;

public class FinishFloorWin : MonoBehaviour
{
    [SerializeField] private string golfBallTag = "GolfBall";
    [SerializeField] private GameObject flag;
    [SerializeField] private GameObject falseFinishUI;
    [SerializeField] private GameObject fakeFinishCameraTriggers;
    [SerializeField] private bool faceRight = true;
    [SerializeField] private CameraController cameraController;

    private bool _triggered = false;

    private void Awake()
    {
        if (cameraController == null)
            cameraController = FindFirstObjectByType<CameraController>();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (_triggered)
            return;

        if (collision.collider.CompareTag(golfBallTag))
        {
            if (flag != null)
                flag.SetActive(true);

            if (falseFinishUI != null)
                falseFinishUI.SetActive(true);

            if (fakeFinishCameraTriggers != null)
                fakeFinishCameraTriggers.SetActive(false);

            if (cameraController == null) return;

            cameraController.SetFacingDirection(faceRight);

            _triggered = true;
        }
    }
}