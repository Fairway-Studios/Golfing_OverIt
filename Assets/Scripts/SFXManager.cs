using UnityEngine;

public class SFXManager : MonoBehaviour
{
    public static SFXManager Instance;

    public AudioSource audioSource;
    public AudioClip birdsChirping;

    private void Awake()
    {
        Instance = this;

        audioSource.clip = birdsChirping;
        audioSource.loop = true;
        audioSource.Play();
    }

    public void PlaySFX(AudioClip clip)
    {
        audioSource.PlayOneShot(clip);
    }
}
