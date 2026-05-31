using System.Threading.Tasks;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

public class RelayManager : MonoBehaviour
{
    public TMP_Text joinCodeText;
    public TMP_InputField joinCodeInput;
    public TMP_Text statusText;
    public string lobbySceneName = "LobbyScene";

    async void Start()
    {
        await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();

        SetStatus("Unity Services ready");
    }

    public void CreateRelay()
    {
        _ = CreateRelayAsync();
    }

    async Task CreateRelayAsync()
    {
        try
        {
            SetStatus("Creating relay...");

            var allocation = await RelayService.Instance.CreateAllocationAsync(3);

            string joinCode =
                await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

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
    }

    public void JoinRelay()
    {
        _ = JoinRelayAsync();
    }

    async Task JoinRelayAsync()
    {
        try
        {
            string code = joinCodeInput.text.Trim();

            if (string.IsNullOrEmpty(code))
            {
                SetStatus("Please enter join code");
                return;
            }

            SetStatus("Joining relay...");

            var joinAllocation =
                await RelayService.Instance.JoinAllocationAsync(code);

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
    }

    void SetStatus(string message)
    {
        Debug.Log(message);

        if (statusText != null)
            statusText.text = message;
    }

    
}