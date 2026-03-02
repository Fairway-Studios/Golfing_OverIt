using UnityEngine;
using System.Collections;

public class ToolTipController : MonoBehaviour
{
    [Header("References")]
    public GameObject tooltipObject;
    public string triggerTag = "GolfBall";

    [Header("Fade Settings")]
    public float fadeDuration = 1f;

    private CanvasGroup canvasGroup;
    private Coroutine fadeCoroutine;

    private void Start()
    {
        if (tooltipObject != null)
        {
            canvasGroup = tooltipObject.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = tooltipObject.AddComponent<CanvasGroup>();

            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            tooltipObject.SetActive(true);
        }
        else
        {
            Debug.LogWarning("Tooltip object not assigned on " + gameObject.name);
        }

        Collider2D col = GetComponent<Collider2D>();
        if (col != null && !col.isTrigger)
        {
            Debug.LogWarning("Collider on " + gameObject.name + " is not set as a trigger!");
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag(triggerTag))
        {
            FadeInTooltip();
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag(triggerTag))
        {
            FadeOutTooltip();
        }
    }

    private void FadeInTooltip()
    {
        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        fadeCoroutine = StartCoroutine(FadeTo(1f));
    }

    private void FadeOutTooltip()
    {
        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        fadeCoroutine = StartCoroutine(FadeTo(0f));
    }

    private IEnumerator FadeTo(float targetAlpha)
    {
        float startAlpha = canvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / fadeDuration);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;

        if (targetAlpha == 0f)
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
        else
        {
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }
    }
}