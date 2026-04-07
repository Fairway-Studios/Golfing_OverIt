using UnityEngine;

public class BallFallRescue2D : MonoBehaviour
{
    [Header("Fallback Spawn")]
    [SerializeField] private Transform fallbackSpawnPoint;

    [Header("Ball Filter")]
    [SerializeField] private string golfBallTag = "GolfBall";

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(golfBallTag))
            return;

        if (fallbackSpawnPoint == null)
        {
            Debug.LogWarning("[BallFallRescue2D] Fallback spawn point is not assigned.");
            return;
        }

        Rigidbody2D ballRb = other.attachedRigidbody;
        if (ballRb == null)
            return;

        Vector3 rescuePosition = fallbackSpawnPoint.position;

        GolfBallController ballController = ballRb.GetComponent<GolfBallController>();
        GameManager gameManager = Object.FindFirstObjectByType<GameManager>();

        // Move ball
        ballRb.linearVelocity = Vector2.zero;
        ballRb.angularVelocity = 0f;
        ballRb.position = rescuePosition;
        ballRb.transform.position = rescuePosition;
        ballRb.Sleep();

        if (ballController != null)
            ballController.ResetForNextShot();

        InputController[] controllers = Object.FindObjectsByType<InputController>(FindObjectsSortMode.None);
        System.Array.Sort(controllers, (a, b) => a.transform.root.name.CompareTo(b.transform.root.name));

        foreach (var controller in controllers)
        {
            controller.OnPlayerTeleported();
        }

        bool isMultiplayer = false;
        if (gameManager != null)
            isMultiplayer = gameManager.IsMultiplayer();

        // In multiplayer: only teleport the ball
        if (!isMultiplayer)
        {
            Vector3 playerOffset = Vector3.zero;
            if (gameManager != null)
                playerOffset = gameManager.GetPlayerOffsetFromBall();

            int ownerIndex = -1;
            if (ballController != null)
                ownerIndex = ballController.GetOwnerIndex();

            if (ownerIndex >= 0 && ownerIndex < controllers.Length && controllers[ownerIndex] != null)
            {
                Transform playerRoot = controllers[ownerIndex].transform.root;
                playerRoot.position = rescuePosition + playerOffset;

                Rigidbody2D playerRb = playerRoot.GetComponent<Rigidbody2D>();
                if (playerRb != null)
                {
                    playerRb.position = rescuePosition + playerOffset;
                    playerRb.linearVelocity = Vector2.zero;
                    playerRb.angularVelocity = 0f;
                    playerRb.Sleep();
                }
            }
            else if (controllers.Length > 0)
            {
                Transform playerRoot = controllers[0].transform.root;
                playerRoot.position = rescuePosition + playerOffset;

                Rigidbody2D playerRb = playerRoot.GetComponent<Rigidbody2D>();
                if (playerRb != null)
                {
                    playerRb.position = rescuePosition + playerOffset;
                    playerRb.linearVelocity = Vector2.zero;
                    playerRb.angularVelocity = 0f;
                    playerRb.Sleep();
                }
            }

            if (Camera.main != null)
            {
                Transform cam = Camera.main.transform;
                cam.position = new Vector3(rescuePosition.x, rescuePosition.y, cam.position.z);
            }

            CameraController cameraController = Object.FindFirstObjectByType<CameraController>();
            if (cameraController != null)
            {
                cameraController.PrepareForSwings();
            }
        }
    }
}