using System.Collections;
using System.Threading.Tasks;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RelayManager : MonoBehaviour
{
    public TMP_Text joinCodeText;
    public TMP_InputField joinCodeInput;
    public TMP_Text statusText;
    public LoadingOverlay loadingOverlay;
    public string lobbySceneName = "LobbyScene";

    string lastJoinCode;
    string currentJoinCode;
    bool isBusy;
    bool disconnectCallbackRegistered;
    bool observedClientConnection;
    bool suppressDisconnectReturn;
    bool returningAfterDisconnect;

    const int DisconnectTimeoutMs = 5000;
    const int HeartbeatTimeoutMs = 500;
    const int MaxConnectAttempts = 8;

    void OnEnable()
    {
        EnsureSingleNetworkManager();
        TryRegisterDisconnectCallback();
    }

    void OnDisable()
    {
        UnregisterDisconnectCallback();
    }

    async void Start()
    {
        EnsureSingleNetworkManager();
        TryRegisterDisconnectCallback();

        SetBusy(true, "Connecting services...");

        await EnsureUnityServicesReady();

        SetBusy(false);

        SetStatus("Unity Services ready");
    }

    void Update()
    {
        TryRegisterDisconnectCallback();
        MonitorLocalDisconnect();
    }

    public void CreateRelay()
    {
        _ = CreateRelayAsync();
    }

    async Task CreateRelayAsync()
    {
        if (isBusy) return;

        try
        {
            SetBusy(true, "Creating room...");
            SetStatus("Creating relay...");
            await EnsureUnityServicesReady();

            NetworkManager networkManager = GetActiveNetworkManager();
            if (networkManager == null)
            {
                SetStatus("NetworkManager missing.");
                return;
            }

            var allocation = await RelayService.Instance.CreateAllocationAsync(3);

            string joinCode =
                await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            currentJoinCode = joinCode;

            var transport =
                networkManager.GetComponent<UnityTransport>();

            ConfigureTransportTimeouts(transport);
            transport.SetRelayServerData(
                AllocationUtils.ToRelayServerData(allocation, "dtls")
            );

            bool result = networkManager.StartHost();


            if (joinCodeText != null)
            {
                joinCodeText.text = "Code: " + joinCode;
            }

            SetStatus("Host started: " + result + " Code: " + joinCode);
        }
        catch (System.Exception e)
        {
            SetStatus("Create relay failed: " + e.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    public void JoinRelay()
    {
        _ = JoinRelayAsync();
    }

    async Task JoinRelayAsync()
    {
        if (isBusy) return;

        try
        {
            string code = joinCodeInput.text.Trim();

            if (string.IsNullOrEmpty(code))
            {
                SetStatus("Please enter join code");
                return;
            }

            SetBusy(true, "Joining room...");
            SetStatus("Joining relay...");
            await EnsureUnityServicesReady();

            NetworkManager networkManager = GetActiveNetworkManager();
            if (networkManager == null)
            {
                SetStatus("NetworkManager missing.");
                return;
            }

            var joinAllocation =
                await RelayService.Instance.JoinAllocationAsync(code);

            lastJoinCode = code;

            var transport =
                networkManager.GetComponent<UnityTransport>();

            ConfigureTransportTimeouts(transport);
            transport.SetRelayServerData(
                AllocationUtils.ToRelayServerData(joinAllocation, "dtls")
            );

            bool result = networkManager.StartClient();
            if (result)
            {
                SetStatus("Waiting for host to load lobby...");
            }

            SetStatus("Client started: " + result);
        }
        catch (System.Exception e)
        {
            SetStatus("Join relay failed: " + e.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    public void Reconnect()
    {
        _ = ReconnectAsync();
    }

    async Task ReconnectAsync()
    {
        if (isBusy) return;

        string code = lastJoinCode;

        if (string.IsNullOrWhiteSpace(code) && joinCodeInput != null)
            code = joinCodeInput.text.Trim();

        if (string.IsNullOrWhiteSpace(code))
        {
            SetStatus("No room code to reconnect.");
            return;
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.Shutdown();

        if (joinCodeInput != null)
            joinCodeInput.SetTextWithoutNotify(code);

        await Task.Delay(250);
        await JoinRelayAsync();
    }

    public void LeaveRoom()
    {
        _ = LeaveRoomAsync();
    }

    public void CopyJoinCode()
    {
        if (string.IsNullOrWhiteSpace(currentJoinCode))
        {
            SetStatus("No room code to copy.");
            return;
        }

        GUIUtility.systemCopyBuffer = currentJoinCode;
        SetStatus("Room code copied: " + currentJoinCode);
    }

    async Task LeaveRoomAsync()
    {
        if (isBusy) return;

        SetBusy(true, "Leaving room...");
        SetStatus("Leaving room...");

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            if (LobbyManager.Instance != null &&
                NetworkManager.Singleton.IsClient &&
                !NetworkManager.Singleton.IsServer)
            {
                LobbyManager.Instance.RequestLeaveLobby();
                await Task.Delay(200);
            }

            suppressDisconnectReturn = true;
            NetworkManager.Singleton.Shutdown();
        }

        await Task.Delay(250);

        if (joinCodeText != null)
            joinCodeText.text = "Code: -";

        currentJoinCode = "";

        if (LobbyManager.Instance != null)
            LobbyManager.Instance.ResetLocalLobbyViewAfterLeave();

        SetStatus("Left room. Create or join another room.");
        suppressDisconnectReturn = false;
        SetBusy(false);
    }

    public void BackToAuth()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.Shutdown();

        SceneManager.LoadScene("AuthScene");
    }

    async Task EnsureUnityServicesReady()
    {
        if (UnityServices.State == ServicesInitializationState.Uninitialized)
            await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    void ConfigureTransportTimeouts(UnityTransport transport)
    {
        if (transport == null)
            return;

        transport.DisconnectTimeoutMS = DisconnectTimeoutMs;
        transport.HeartbeatTimeoutMS = HeartbeatTimeoutMs;
        transport.MaxConnectAttempts = MaxConnectAttempts;
    }

    NetworkManager GetActiveNetworkManager()
    {
        EnsureSingleNetworkManager();
        return NetworkManager.Singleton;
    }

    void EnsureSingleNetworkManager()
    {
        NetworkManager[] managers =
            FindObjectsByType<NetworkManager>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        if (managers == null || managers.Length <= 1)
            return;

        NetworkManager keep =
            NetworkManager.Singleton != null ? NetworkManager.Singleton : managers[0];

        foreach (NetworkManager manager in managers)
        {
            if (manager == null || manager == keep)
                continue;

            Debug.LogWarning("Destroyed duplicate NetworkManager: " + manager.name);
            Destroy(manager.gameObject);
        }
    }

    void SetBusy(bool busy, string message = "")
    {
        isBusy = busy;

        if (loadingOverlay == null)
            return;

        if (busy)
            loadingOverlay.Show(message);
        else
            loadingOverlay.Hide();
    }

    void SetStatus(string message)
    {
        Debug.Log(message);

        if (statusText != null)
            statusText.text = message;
    }

    void TryRegisterDisconnectCallback()
    {
        if (disconnectCallbackRegistered || NetworkManager.Singleton == null)
            return;

        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        disconnectCallbackRegistered = true;
    }

    void UnregisterDisconnectCallback()
    {
        if (!disconnectCallbackRegistered || NetworkManager.Singleton == null)
            return;

        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        disconnectCallbackRegistered = false;
    }

    void OnClientDisconnected(ulong clientId)
    {
        if (suppressDisconnectReturn ||
            returningAfterDisconnect ||
            NetworkManager.Singleton == null ||
            NetworkManager.Singleton.IsServer ||
            clientId != NetworkManager.Singleton.LocalClientId)
            return;

        StartCoroutine(ReturnToLobbyAfterDisconnect("Disconnected from host. Returning to lobby..."));
    }

    void MonitorLocalDisconnect()
    {
        if (suppressDisconnectReturn ||
            returningAfterDisconnect ||
            NetworkManager.Singleton == null ||
            NetworkManager.Singleton.IsServer)
            return;

        if (NetworkManager.Singleton.IsClient && NetworkManager.Singleton.IsConnectedClient)
        {
            observedClientConnection = true;
            return;
        }

        if (observedClientConnection)
            StartCoroutine(ReturnToLobbyAfterDisconnect("Disconnected from host. Returning to lobby..."));
    }

    IEnumerator ReturnToLobbyAfterDisconnect(string message)
    {
        returningAfterDisconnect = true;
        SetBusy(true, message);
        SetStatus(message);

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.Shutdown();

        yield return null;

        observedClientConnection = false;

        if (SceneManager.GetActiveScene().name != lobbySceneName)
        {
            SceneManager.LoadScene(lobbySceneName, LoadSceneMode.Single);
        }
        else
        {
            if (LobbyManager.Instance != null)
                LobbyManager.Instance.ResetLocalLobbyViewAfterLeave();

            SetStatus("Host closed the room. Create or join another room.");
            SetBusy(false);
        }
    }
}
