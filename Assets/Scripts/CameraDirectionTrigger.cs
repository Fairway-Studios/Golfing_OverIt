using UnityEngine;

public class CameraDirectionTrigger : MonoBehaviour
{
    [SerializeField] private bool faceRight = true;
    [SerializeField] private CameraController cameraController;

    private void Awake()
    {
        if (cameraController == null)
            cameraController = FindFirstObjectByType<CameraController>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("GolfBall")) return;
        if (cameraController == null) return;

        cameraController.SetFacingDirection(faceRight);
    }
}