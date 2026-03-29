using UnityEngine;

public class FinishFloorWin : MonoBehaviour
{
    [SerializeField] private string golfBallTag = "GolfBall";
    [SerializeField] private GameObject flag;
    [SerializeField] private GameObject falseFinishUI;

    private bool _triggered = false;

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

            _triggered = true;
        }
    }
}