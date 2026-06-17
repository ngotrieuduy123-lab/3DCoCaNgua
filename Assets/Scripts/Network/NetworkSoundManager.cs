using Unity.Netcode;
using UnityEngine;

public class NetworkSoundManager : NetworkBehaviour
{
    public static NetworkSoundManager Instance;

    void Awake()
    {
        Instance = this;
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void PlayMoveSoundRpc()
    {
        if (SoundManager.Instance != null)
            SoundManager.Instance.PlayMove();
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void PlayKickSoundRpc()
    {
        if (SoundManager.Instance != null)
            SoundManager.Instance.PlayKick();
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void PlayDiceSoundRpc()
    {
        if (SoundManager.Instance != null)
            SoundManager.Instance.PlayDice();
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void PlayFinishSoundRpc()
    {
        if (SoundManager.Instance != null)
            SoundManager.Instance.PlayFinish();
    }
}
