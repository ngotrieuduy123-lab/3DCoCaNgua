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

    public NetworkList<ulong> playerClientIds;
    public NetworkList<FixedString32Bytes> playerNames;
    public NetworkList<bool> readyStates;

    bool isStartingGame;

    void Awake()
    {
        Instance = this;

        playerClientIds = new NetworkList<ulong>();
        playerNames = new NetworkList<FixedString32Bytes>();
        readyStates = new NetworkList<bool>();
    }

    public override void OnNetworkSpawn()
    {
        playerClientIds.OnListChanged += OnPlayerClientListChanged;
        playerNames.OnListChanged += OnPlayerListChanged;
        readyStates.OnListChanged += OnReadyListChanged;

        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

            playerClientIds.Clear();
            playerNames.Clear();
            readyStates.Clear();

            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                AddPlayer(client.ClientId);
            }
        }

        RefreshPlayerList();
    }

    public override void OnNetworkDespawn()
    {
        playerClientIds.OnListChanged -= OnPlayerClientListChanged;
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

        AddPlayer(clientId);

        RefreshPlayerList();
    }

    void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer) return;

        int index = GetPlayerIndexByClientId(clientId);

        if (index >= 0)
        {
            playerClientIds.RemoveAt(index);
            playerNames.RemoveAt(index);
            readyStates.RemoveAt(index);
        }

        RefreshPlayerList();
    }

    void OnPlayerClientListChanged(NetworkListEvent<ulong> change)
    {
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

        int count = GetPlayerCount();

        for (int i = 0; i < count; i++)
        {
            string ready = readyStates[i] ? "[READY]" : "[NOT READY]";
            text += "Player " + i + " " + ready + "\n";
        }

        playerListText.text = text;

        CacheLocalPlayerIndex();
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
        for (int i = 0; i < playerClientIds.Count; i++)
        {
            if (playerClientIds[i] == clientId)
                return i;
        }

        return -1;
    }

    public async void StartGame()
    {
        if (!IsServer) return;
        if (isStartingGame) return;

        isStartingGame = true;

        if (loadingOverlay != null)
            loadingOverlay.Show("Starting game...");

        int count = GetPlayerCount();

        if (count < 2)
        {
            if (loadingOverlay != null)
                loadingOverlay.Hide();
            SetStatusClientRpc("Need at least 2 players");
            isStartingGame = false;
            return;
        }

        for (int i = 0; i < count; i++)
        {
            if (!readyStates[i])
            {
                if (loadingOverlay != null)
                    loadingOverlay.Hide();
                SetStatusClientRpc("All players must ready");
                isStartingGame = false;
                return;
            }
        }

        CachePlayerMappingsForGame();
        PlayerPrefs.SetInt("PlayerCount", count);

        if (DatabaseManager.Instance != null)
        {
            PlayerPrefs.DeleteKey("CurrentMatchHistoryId");
            string matchHistoryId = await DatabaseManager.Instance.BeginMatchHistory(count);

            if (!string.IsNullOrWhiteSpace(matchHistoryId))
                PlayerPrefs.SetString("CurrentMatchHistoryId", matchHistoryId);
        }

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

    void AddPlayer(ulong clientId)
    {
        if (GetPlayerIndexByClientId(clientId) >= 0)
            return;

        int nextIndex = GetPlayerCount();

        if (nextIndex >= 4)
        {
            Debug.Log("Lobby full. Reject extra player: " + clientId);
            return;
        }

        playerClientIds.Add(clientId);
        playerNames.Add("Player " + nextIndex);
        readyStates.Add(false);

        CachePlayerMappingsForGame();
    }

    int GetPlayerCount()
    {
        return Mathf.Min(playerClientIds.Count, readyStates.Count);
    }

    void CacheLocalPlayerIndex()
    {
        if (NetworkManager.Singleton == null)
            return;

        int localIndex = GetPlayerIndexByClientId(NetworkManager.Singleton.LocalClientId);

        if (localIndex < 0)
            return;

        PlayerPrefs.SetInt("LocalPlayerIndex", localIndex);
        PlayerPrefs.SetInt(
            "PlayerIndexForClient_" + NetworkManager.Singleton.LocalClientId,
            localIndex
        );
        PlayerPrefs.Save();
    }

    void CachePlayerMappingsForGame()
    {
        int count = GetPlayerCount();

        for (int i = 0; i < count; i++)
        {
            PlayerPrefs.SetInt("PlayerIndexForClient_" + playerClientIds[i], i);
        }

        if (NetworkManager.Singleton != null)
            CacheLocalPlayerIndex();

        PlayerPrefs.Save();
    }
}
