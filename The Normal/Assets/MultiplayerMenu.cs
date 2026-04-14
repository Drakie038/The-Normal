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
using System.Collections;
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

    private bool searching = false;
    private Coroutine lobbyHeartbeat;

    private async void Start()
    {
        await UnityServices.InitializeAsync();
        await AuthenticationService.Instance.SignInAnonymouslyAsync();

        quickPlayButton.onClick.AddListener(() => _ = QuickPlay());
        createServerButton.onClick.AddListener(() => _ = CreateServer());
        leaveButton.onClick.AddListener(LeaveGame);

        leaveButton.gameObject.SetActive(false);

        networkManager.OnClientConnectedCallback += OnClientConnected;
    }

    private void Update()
    {
        // 🔥 FIX: UI blijft altijd correct syncen met Netcode state
        bool inGame = networkManager.IsClient || networkManager.IsHost;

        leaveButton.gameObject.SetActive(inGame);
    }

    // =====================================================
    // QUICK PLAY
    // =====================================================
    private async Task QuickPlay()
    {
        if (networkManager.IsClient || networkManager.IsHost)
            return;

        searching = true;

        networkManager.Shutdown();
        await Task.Delay(300);

        while (searching)
        {
            try
            {
                Lobby lobby = await LobbyService.Instance.QuickJoinLobbyAsync();

                string code = lobby.Data["joinCode"].Value;

                JoinAllocation allocation =
                    await RelayService.Instance.JoinAllocationAsync(code);

                StartClient(allocation);

                searching = false;
                return;
            }
            catch
            {
                await Task.Delay(1500);
            }
        }
    }

    // =====================================================
    // CREATE SERVER
    // =====================================================
    private async Task CreateServer()
    {
        if (networkManager.IsClient || networkManager.IsHost)
            return;

        hostAllocation = await RelayService.Instance.CreateAllocationAsync(4);
        string joinCode = await RelayService.Instance.GetJoinCodeAsync(hostAllocation.AllocationId);

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

        StartHost(hostAllocation);

        StartHeartbeat();
    }

    // =====================================================
    // HEARTBEAT (KEEP LOBBY ALIVE)
    // =====================================================
    private void StartHeartbeat()
    {
        if (lobbyHeartbeat != null)
            StopCoroutine(lobbyHeartbeat);

        lobbyHeartbeat = StartCoroutine(Heartbeat());
    }

    private IEnumerator Heartbeat()
    {
        while (currentLobby != null)
        {
            yield return new WaitForSeconds(5f);

            if (networkManager.IsHost && currentLobby != null)
            {
                LobbyService.Instance.UpdateLobbyAsync(
                    currentLobby.Id,
                    new UpdateLobbyOptions()
                );
            }
        }
    }

    // =====================================================
    // LEAVE GAME
    // =====================================================
    private void LeaveGame()
    {
        searching = false;

        if (lobbyHeartbeat != null)
        {
            StopCoroutine(lobbyHeartbeat);
            lobbyHeartbeat = null;
        }

        networkManager.Shutdown();

        leaveButton.gameObject.SetActive(false);
    }

    // =====================================================
    // HOST START
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
    }

    // =====================================================
    // CLIENT START
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
    }

    // =====================================================
    // SPAWN PLAYER
    // =====================================================
    private void OnClientConnected(ulong clientId)
    {
        if (!networkManager.IsServer) return;

        GameObject obj = Instantiate(playerPrefab);
        obj.GetComponent<NetworkObject>().SpawnWithOwnership(clientId);
    }
}