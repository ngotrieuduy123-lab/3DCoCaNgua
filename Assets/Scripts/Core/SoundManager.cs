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
        audioSource.PlayOneShot(diceSound);
    }

    public void PlayMove()
    {
        if (Time.time - lastMoveSoundTime < 0.08f)
            return;

        lastMoveSoundTime = Time.time;

        audioSource.PlayOneShot(moveSound);
    }

    public void PlayKick()
    {
        audioSource.PlayOneShot(kickSound);
    }

    public void PlayFinish()
    {
        audioSource.PlayOneShot(finishSound);
    }
}