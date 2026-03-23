using UnityEngine;

public class CameraDirectionTrigger : MonoBehaviour
{
    [SerializeField] private bool faceRight = true;
    [SerializeField] private CameraController cameraController;

    [SerializeField] private bool triggerOnce = true;
    private bool hasTriggered = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (triggerOnce && hasTriggered) return;
        if (!other.CompareTag("GolfBall")) return;

        hasTriggered = true;

        if (cameraController != null)
            cameraController.SetFacingDirection(faceRight);
    }
}