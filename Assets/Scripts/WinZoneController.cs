using UnityEngine;

public class WinZone : MonoBehaviour
{
    [Header("UI")]
    public GameObject winUI;

    private void Awake()
    {
        if (winUI != null)
            winUI.SetActive(false);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("GolfBall"))
        {
            if (winUI != null)
                winUI.SetActive(true);

            Debug.Log("Win condition reached!");
        }
    }
}
