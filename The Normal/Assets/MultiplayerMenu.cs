using UnityEngine;
using UnityEngine.UI;

using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

using Unity.Services.Core;
using Unity.Services.Authentication;

using Unity.Services.Relay;
using Unity.Services.Relay.Models;

using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;

using System.Threading.Tasks;
using System.Collections.Generic;

public class MultiplayerMenu : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Button quickPlayButton;
    [SerializeField] private Button createServerButton;
    [SerializeField] private Button leaveButton;

    [Header("Player")]
    [SerializeField] private GameObject playerPrefab;

    [Header("Netcode")]
    [SerializeField] private NetworkManager networkManager;

    private Lobby currentLobby;
    private Allocation hostAllocation;

    private async void Start()
    {
        await UnityServices.InitializeAsync();
        await AuthenticationService.Instance.SignInAnonymouslyAsync();

        quickPlayButton.onClick.AddListener(() => _ = QuickPlay());
        createServerButton.onClick.AddListener(() => _ = CreateServer());
        leaveButton.onClick.AddListener(LeaveGame);

        leaveButton.gameObject.SetActive(false);

        // 🔥 BELANGRIJK: spawn players automatisch bij connect
        networkManager.OnClientConnectedCallback += OnClientConnected;
    }

    // =====================================================
    // 🔵 QUICK PLAY
    // =====================================================
    private async Task QuickPlay()
    {
        if (networkManager.IsClient || networkManager.IsHost)
            return;

        try
        {
            Lobby lobby = await LobbyService.Instance.QuickJoinLobbyAsync();

            JoinAllocation allocation =
                await RelayService.Instance.JoinAllocationAsync(lobby.Data["joinCode"].Value);

            StartClient(allocation);

            leaveButton.gameObject.SetActive(true);
        }
        catch
        {
            await CreateServer();
        }
    }

    // =====================================================
    // 🟢 CREATE SERVER
    // =====================================================
    private async Task CreateServer()
    {
        if (networkManager.IsClient || networkManager.IsHost)
            return;

        hostAllocation = await RelayService.Instance.CreateAllocationAsync(4);
        string joinCode = await RelayService.Instance.GetJoinCodeAsync(hostAllocation.AllocationId);

        Debug.Log("JOIN CODE: " + joinCode);

        var options = new CreateLobbyOptions
        {
            IsPrivate = false,
            Data = new Dictionary<string, DataObject>
            {
                {
                    "joinCode",
                    new DataObject(DataObject.VisibilityOptions.Public, joinCode)
                }
            }
        };

        currentLobby = await LobbyService.Instance.CreateLobbyAsync("Game", 4, options);

        await Task.Delay(300);

        StartHost(hostAllocation);

        leaveButton.gameObject.SetActive(true);

        // 🔥 host krijgt ook player
        SpawnPlayer(NetworkManager.Singleton.LocalClientId);
    }

    // =====================================================
    // 🔴 LEAVE
    // =====================================================
    private void LeaveGame()
    {
        networkManager.Shutdown();
        leaveButton.gameObject.SetActive(false);
    }

    // =====================================================
    // 🟢 HOST START
    // =====================================================
    private void StartHost(Allocation allocation)
    {
        var transport = networkManager.GetComponent<UnityTransport>();

        transport.SetRelayServerData(
            allocation.RelayServer.IpV4,
            (ushort)allocation.RelayServer.Port,
            allocation.AllocationIdBytes,
            allocation.Key,
            allocation.ConnectionData
        );

        networkManager.StartHost();

        Debug.Log("HOST STARTED");
    }

    // =====================================================
    // 🔵 CLIENT START
    // =====================================================
    private void StartClient(JoinAllocation allocation)
    {
        var transport = networkManager.GetComponent<UnityTransport>();

        transport.SetRelayServerData(
            allocation.RelayServer.IpV4,
            (ushort)allocation.RelayServer.Port,
            allocation.AllocationIdBytes,
            allocation.Key,
            allocation.ConnectionData,
            allocation.HostConnectionData
        );

        networkManager.StartClient();

        Debug.Log("CLIENT STARTED");
    }

    // =====================================================
    // 🧍 SPAWN PLAYER (SERVER ONLY)
    // =====================================================
    private void OnClientConnected(ulong clientId)
    {
        if (!networkManager.IsServer) return;

        SpawnPlayer(clientId);
    }

    private void SpawnPlayer(ulong clientId)
    {
        GameObject obj = Instantiate(playerPrefab);

        NetworkObject netObj = obj.GetComponent<NetworkObject>();

        netObj.SpawnWithOwnership(clientId);

        Debug.Log($"Player spawned for client {clientId}");
    }
}