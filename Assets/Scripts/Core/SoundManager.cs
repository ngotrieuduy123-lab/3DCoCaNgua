using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance;

    public AudioSource audioSource;

    public AudioClip diceSound;
    public AudioClip moveSound;
    public AudioClip kickSound;
    public AudioClip finishSound;
    private float lastMoveSoundTime;

    void Awake()
    {
        Instance = this;
    }

    public void PlayDice()
    {
        if (audioSource == null || diceSound == null)
            return;

        audioSource.PlayOneShot(diceSound);
    }

    public void PlayMove()
    {
        if (Time.time - lastMoveSoundTime < 0.08f)
            return;

        lastMoveSoundTime = Time.time;

        if (audioSource == null || moveSound == null)
            return;

        audioSource.PlayOneShot(moveSound);
    }

    public void PlayKick()
    {
        if (audioSource == null || kickSound == null)
            return;

        audioSource.PlayOneShot(kickSound);
    }

    public void PlayFinish()
    {
        if (audioSource == null || finishSound == null)
            return;

        audioSource.PlayOneShot(finishSound);
    }
}
