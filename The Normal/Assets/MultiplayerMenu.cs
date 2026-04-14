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

    private string lastJoinCode;
    private bool searching = false;

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

    // =====================================================
    // 🔵 QUICK PLAY (FIXED RELIABLE SEARCH)
    // =====================================================
    private async Task QuickPlay()
    {
        if (networkManager.IsClient || networkManager.IsHost)
            return;

        searching = true;

        while (searching)
        {
            try
            {
                QueryResponse lobbies = await LobbyService.Instance.QueryLobbiesAsync();

                foreach (var lobby in lobbies.Results)
                {
                    if (!lobby.Data.ContainsKey("joinCode"))
                        continue;

                    string code = lobby.Data["joinCode"].Value;

                    try
                    {
                        JoinAllocation allocation =
                            await RelayService.Instance.JoinAllocationAsync(code);

                        lastJoinCode = code;

                        StartClient(allocation);

                        leaveButton.gameObject.SetActive(true);

                        searching = false;
                        return;
                    }
                    catch
                    {
                        // ❌ Relay dead → skip this lobby
                        continue;
                    }
                }

                Debug.Log("No valid servers found... retrying");
                await Task.Delay(2000);
            }
            catch
            {
                await Task.Delay(2000);
            }
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

        lastJoinCode = joinCode;

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
    }

    // =====================================================
    // 🔴 LEAVE
    // =====================================================
    private void LeaveGame()
    {
        searching = false;

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
    }

    // =====================================================
    // 🧍 SPAWN SYSTEM
    // =====================================================
    private void OnClientConnected(ulong clientId)
    {
        if (!networkManager.IsServer) return;

        GameObject obj = Instantiate(playerPrefab);
        obj.GetComponent<NetworkObject>().SpawnWithOwnership(clientId);
    }
}