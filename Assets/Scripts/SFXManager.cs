using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class SFXManager : MonoBehaviour
{
    public static SFXManager Instance;

    [Header("Audio Sources")]
    public AudioSource sfxSource;
    public AudioSource ambienceSource;

    [Header("UI Sounds")]
    public AudioClip hoverSound;
    public AudioClip clickSound;

    [Header("Ambience")]
    public AudioClip birdsChirping;

    EventSystem eventSystem;
    GameObject lastSelected;

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Start ambience
        if (birdsChirping != null && ambienceSource != null)
        {
            ambienceSource.clip = birdsChirping;
            ambienceSource.loop = true;
            ambienceSource.Play();
        }
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        eventSystem = EventSystem.current;
        HookButtons();
    }

    void HookButtons()
    {
        Button[] buttons = FindObjectsByType<Button>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        foreach (Button button in buttons)
        {
            button.onClick.RemoveListener(PlayClick);
            button.onClick.AddListener(PlayClick);

            UIHoverForwarder hover = button.GetComponent<UIHoverForwarder>();

            if (hover == null)
                hover = button.gameObject.AddComponent<UIHoverForwarder>();

            hover.manager = this;
        }
    }

    void Update()
    {
        if (eventSystem == null)
            return;

        if (eventSystem.IsPointerOverGameObject())
            return;

        GameObject current = eventSystem.currentSelectedGameObject;

        if (current != lastSelected)
        {
            if (current != null && current.GetComponent<Button>() != null)
            {
                PlayHover();
            }

            lastSelected = current;
        }
    }

    public void PlayHover()
    {
        if (hoverSound == null || sfxSource == null)
            return;

        sfxSource.PlayOneShot(hoverSound, 0.3f);
    }

    public void PlayClick()
    {
        if (clickSound == null || sfxSource == null)
            return;

        sfxSource.PlayOneShot(clickSound, 1f);
    }

    public void PlaySFX(AudioClip clip)
    {
        if (clip == null || sfxSource == null)
            return;

        sfxSource.PlayOneShot(clip);
    }

    class UIHoverForwarder : MonoBehaviour, IPointerEnterHandler
    {
        public SFXManager manager;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (manager != null)
                manager.PlayHover();
        }
    }
}