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

    async void Start()
    {
        SetBusy(true, "Connecting services...");

        await EnsureUnityServicesReady();

        SetBusy(false);

        SetStatus("Unity Services ready");
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

            var allocation = await RelayService.Instance.CreateAllocationAsync(3);

            string joinCode =
                await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            currentJoinCode = joinCode;

            var transport =
                NetworkManager.Singleton.GetComponent<UnityTransport>();

            transport.SetRelayServerData(
                AllocationUtils.ToRelayServerData(allocation, "dtls")
            );

            bool result = NetworkManager.Singleton.StartHost();


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

            var joinAllocation =
                await RelayService.Instance.JoinAllocationAsync(code);

            lastJoinCode = code;

            var transport =
                NetworkManager.Singleton.GetComponent<UnityTransport>();

            transport.SetRelayServerData(
                AllocationUtils.ToRelayServerData(joinAllocation, "dtls")
            );

            bool result = NetworkManager.Singleton.StartClient();
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
            NetworkManager.Singleton.Shutdown();

        await Task.Delay(250);

        if (joinCodeText != null)
            joinCodeText.text = "Code: -";

        currentJoinCode = "";

        SetStatus("Left room. Create or join another room.");
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

    
}
