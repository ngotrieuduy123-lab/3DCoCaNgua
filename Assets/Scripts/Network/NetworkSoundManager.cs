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
        SoundManager.Instance.PlayMove();
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void PlayKickSoundRpc()
    {
        SoundManager.Instance.PlayKick();
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void PlayDiceSoundRpc()
    {
        SoundManager.Instance.PlayDice();
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void PlayFinishSoundRpc()
    {
        SoundManager.Instance.PlayFinish();
    }
}