using TMPro;
using System.Collections;
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

        StartCoroutine(SubmitDisplayNameRoutine());
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

        RemovePlayerFromLobby(clientId);
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
            string displayName = playerNames[i].ToString();

            if (string.IsNullOrWhiteSpace(displayName))
                displayName = "Player " + i;

            text += displayName + " " + ready + "\n";
        }

        playerListText.text = text;

        CacheLocalPlayerIndex();
    }

    public void ToggleReady()
    {
        if (NetworkManager.Singleton == null) return;

        ToggleReadyServerRpc(NetworkManager.Singleton.LocalClientId);
    }

    public void RequestLeaveLobby()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            return;

        if (IsServer)
        {
            RemovePlayerFromLobby(NetworkManager.Singleton.LocalClientId);
            return;
        }

        RequestLeaveLobbyServerRpc();
    }

    public void ResetLocalLobbyViewAfterLeave()
    {
        if (playerListText != null)
            playerListText.text = "";

        if (roomCodeText != null)
            roomCodeText.text = "Code: -";
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

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    void RequestLeaveLobbyServerRpc(RpcParams rpcParams = default)
    {
        RemovePlayerFromLobby(rpcParams.Receive.SenderClientId);
    }

    void SubmitDisplayName()
    {
        if (NetworkManager.Singleton == null)
            return;

        string displayName = GetLocalDisplayName();

        if (IsServer)
        {
            SetPlayerDisplayName(NetworkManager.Singleton.LocalClientId, displayName);
            return;
        }

        SubmitDisplayNameServerRpc(displayName);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    void SubmitDisplayNameServerRpc(string displayName, RpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;

        if (GetPlayerIndexByClientId(senderClientId) < 0)
            AddPlayer(senderClientId);

        SetPlayerDisplayName(senderClientId, displayName);
    }

    IEnumerator SubmitDisplayNameRoutine()
    {
        yield return null;

        for (int i = 0; i < 5; i++)
        {
            SubmitDisplayName();
            yield return new WaitForSeconds(0.25f);
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
        CacheTurnDisplayNamesClientRpc(
            count,
            GetPlayerDisplayNameForCache(0),
            GetPlayerDisplayNameForCache(1),
            GetPlayerDisplayNameForCache(2),
            GetPlayerDisplayNameForCache(3)
        );

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

    [Rpc(SendTo.ClientsAndHost)]
    void CacheTurnDisplayNamesClientRpc(
        int count,
        string player0Name,
        string player1Name,
        string player2Name,
        string player3Name)
    {
        string[] names =
        {
            player0Name,
            player1Name,
            player2Name,
            player3Name
        };

        PlayerPrefs.SetInt("PlayerCount", count);

        for (int i = 0; i < names.Length; i++)
        {
            PlayerPrefs.SetString(
                "TurnDisplayName_" + i,
                SanitizeDisplayName(names[i], i)
            );
        }

        PlayerPrefs.Save();
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

    void RemovePlayerFromLobby(ulong clientId)
    {
        if (!IsServer)
            return;

        int index = GetPlayerIndexByClientId(clientId);

        if (index < 0)
            return;

        playerClientIds.RemoveAt(index);
        playerNames.RemoveAt(index);
        readyStates.RemoveAt(index);

        CachePlayerMappingsForGame();
        RefreshPlayerList();
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

    string GetPlayerDisplayNameForCache(int index)
    {
        if (index >= 0 && index < playerNames.Count)
            return playerNames[index].ToString();

        return "Player " + index;
    }

    void SetPlayerDisplayName(ulong clientId, string displayName)
    {
        if (!IsServer)
            return;

        int index = GetPlayerIndexByClientId(clientId);

        if (index < 0)
        {
            AddPlayer(clientId);
            index = GetPlayerIndexByClientId(clientId);
        }

        if (index < 0 || index >= playerNames.Count)
            return;

        playerNames[index] = SanitizeDisplayName(displayName, index);

        RefreshPlayerList();
    }

    string GetLocalDisplayName()
    {
        if (DatabaseManager.Instance != null &&
            DatabaseManager.Instance.CurrentPlayer != null &&
            !string.IsNullOrWhiteSpace(DatabaseManager.Instance.CurrentPlayer.DisplayName))
            return DatabaseManager.Instance.CurrentPlayer.DisplayName;

        string displayName = PlayerPrefs.GetString("DisplayName", "");

        if (!string.IsNullOrWhiteSpace(displayName))
            return displayName;

        return PlayerPrefs.GetString("Username", "");
    }

    string SanitizeDisplayName(string displayName, int index)
    {
        displayName = string.IsNullOrWhiteSpace(displayName)
            ? "Player " + index
            : displayName.Trim();

        if (displayName.Length > 24)
            displayName = displayName.Substring(0, 24);

        return displayName;
    }
}
