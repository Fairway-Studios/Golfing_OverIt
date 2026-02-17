using UnityEngine;

public class ToolTipTController : MonoBehaviour
{
    public GameObject tooltipObject;
    public string triggerTag = "GolfBall";

    private void Start()
    {
        if (tooltipObject != null)
        {
            tooltipObject.SetActive(false);
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
            ShowTooltip();
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag(triggerTag))
        {
            HideTooltip();
        }
    }

    private void ShowTooltip()
    {
        if (tooltipObject != null)
        {
            tooltipObject.SetActive(true);
        }
    }

    private void HideTooltip()
    {
        if (tooltipObject != null)
        {
            tooltipObject.SetActive(false);
        }
    }
}