using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LobbyManager : NetworkBehaviour
{
    public static LobbyManager Instance;

    public TMP_Text roomCodeText;
    public TMP_Text playerListText;
    public TMP_Text statusText;
    public LoadingOverlay loadingOverlay;

    public NetworkList<FixedString32Bytes> playerNames;
    public NetworkList<bool> readyStates;

    void Awake()
    {
        Instance = this;

        playerNames = new NetworkList<FixedString32Bytes>();
        readyStates = new NetworkList<bool>();
    }

    public override void OnNetworkSpawn()
    {
        playerNames.OnListChanged += OnPlayerListChanged;
        readyStates.OnListChanged += OnReadyListChanged;

        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

            playerNames.Clear();
            readyStates.Clear();

            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                readyStates.Add(false);
                playerNames.Add("Player " + client.ClientId);
            }
        }

        RefreshPlayerList();
    }

    public override void OnNetworkDespawn()
    {
        playerNames.OnListChanged -= OnPlayerListChanged;
        readyStates.OnListChanged -= OnReadyListChanged;

        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    void OnClientConnected(ulong clientId)
    {
        if (!IsServer) return;

        string name = "Player " + clientId;

        for (int i = 0; i < playerNames.Count; i++)
        {
            if (playerNames[i].ToString() == name)
                return;
        }

        readyStates.Add(false);
        playerNames.Add(name);

        RefreshPlayerList();
    }

    void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer) return;

        int index = GetPlayerIndexByClientId(clientId);

        if (index >= 0)
        {
            playerNames.RemoveAt(index);
            readyStates.RemoveAt(index);
        }

        RefreshPlayerList();
    }

    void OnPlayerListChanged(NetworkListEvent<FixedString32Bytes> change)
    {
        RefreshPlayerList();
    }

    void OnReadyListChanged(NetworkListEvent<bool> change)
    {
        RefreshPlayerList();
    }

    void RefreshPlayerList()
    {
        if (playerListText == null) return;

        string text = "";

        int count = Mathf.Min(playerNames.Count, readyStates.Count);

        for (int i = 0; i < count; i++)
        {
            string ready = readyStates[i] ? "[READY]" : "[NOT READY]";
            text += playerNames[i].ToString() + " " + ready + "\n";
        }

        playerListText.text = text;
    }

    public void ToggleReady()
    {
        if (NetworkManager.Singleton == null) return;

        ToggleReadyServerRpc(NetworkManager.Singleton.LocalClientId);
    }

    [Rpc(SendTo.Server)]
    void ToggleReadyServerRpc(ulong clientId)
    {
        int index = GetPlayerIndexByClientId(clientId);

        if (index >= 0 && index < readyStates.Count)
        {
            readyStates[index] = !readyStates[index];
        }
    }

    int GetPlayerIndexByClientId(ulong clientId)
    {
        string targetName = "Player " + clientId;

        for (int i = 0; i < playerNames.Count; i++)
        {
            if (playerNames[i].ToString() == targetName)
                return i;
        }

        return -1;
    }

    public void StartGame()
    {
        if (!IsServer) return;

        if (loadingOverlay != null)
            loadingOverlay.Show("Starting game...");

        int count = Mathf.Min(playerNames.Count, readyStates.Count);

        if (count < 2)
        {
            if (loadingOverlay != null)
                loadingOverlay.Hide();
            SetStatusClientRpc("Need at least 2 players");
            return;
        }

        for (int i = 0; i < count; i++)
        {
            if (!readyStates[i])
            {
                if (loadingOverlay != null)
                    loadingOverlay.Hide();
                SetStatusClientRpc("All players must ready");
                return;
            }
        }

        PlayerPrefs.SetInt("PlayerCount", count);

        Debug.Log("Start game with player count: " + count);
        ShowLoadingClientRpc("Starting game...");

        NetworkManager.Singleton.SceneManager.LoadScene(
            "GameScene",
            LoadSceneMode.Single
        );
    }

    [Rpc(SendTo.ClientsAndHost)]
    void SetStatusClientRpc(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }

    [Rpc(SendTo.ClientsAndHost)]
    void ShowLoadingClientRpc(string message)
    {
        if (loadingOverlay != null)
            loadingOverlay.Show(message);
    }
}
