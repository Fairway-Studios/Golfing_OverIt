using UnityEngine;
using TMPro;

public class BallIndicator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform indicatorRect;
    [SerializeField] private TextMeshProUGUI arrowImage;
    [SerializeField] private TextMeshProUGUI labelText;

    [Header("Settings")]
    [SerializeField] private float edgePadding = 50f;
    [SerializeField] private float onScreenScale = 0.8f;
    [SerializeField] private float offScreenScale = 1f;

    private Camera mainCamera;
    private Transform targetBall;
    private bool isActive = false;

    void Awake()
    {
        mainCamera = Camera.main;

        if (indicatorRect == null)
            indicatorRect = GetComponentInChildren<RectTransform>();

        gameObject.SetActive(false);
    }

    void Update()
    {
        if (!isActive || targetBall == null || mainCamera == null)
        {
            if (gameObject.activeSelf)
                gameObject.SetActive(false);

            return;
        }

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        UpdateIndicatorPosition();
    }

    void UpdateIndicatorPosition()
    {
        Vector3 ballWorldPos = targetBall.position;
        Vector3 screenPos = mainCamera.WorldToScreenPoint(ballWorldPos);

        bool isOnScreen =
            screenPos.z > 0 &&
            screenPos.x > 0 && screenPos.x < Screen.width &&
            screenPos.y > 0 && screenPos.y < Screen.height;

        if (isOnScreen)
            ShowOnScreenIndicator(screenPos);
        else
            ShowOffScreenIndicator(screenPos);
    }

    void ShowOnScreenIndicator(Vector3 screenPos)
    {
        indicatorRect.position = screenPos;

        if (arrowImage != null)
            arrowImage.enabled = false;

        indicatorRect.localScale = Vector3.one * onScreenScale;
    }

    void ShowOffScreenIndicator(Vector3 screenPos)
    {
        Vector2 clamped = new Vector2(
            Mathf.Clamp(screenPos.x, edgePadding, Screen.width - edgePadding),
            Mathf.Clamp(screenPos.y, edgePadding, Screen.height - edgePadding)
        );

        indicatorRect.position = clamped;

        if (arrowImage != null)
        {
            arrowImage.enabled = true;
            Vector2 direction = (Vector2)screenPos - clamped;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            arrowImage.transform.localRotation = Quaternion.Euler(0, 0, angle - 90);
        }

        indicatorRect.localScale = Vector3.one * offScreenScale;
    }

    public void Setup(Transform ball, string label)
    {
        targetBall = ball;

        if (labelText != null)
            labelText.text = label;
    }

    public void Show()
    {
        isActive = true;
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        isActive = false;
        gameObject.SetActive(false);
    }
}
