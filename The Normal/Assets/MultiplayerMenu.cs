using UnityEngine;
using UnityEngine.UI;
using TMPro;

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
using System;

public class MultiplayerMenu : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Button quickPlayButton;
    [SerializeField] private Button createServerButton;
    [SerializeField] private Button leaveButton;

    [Header("TMP UI")]
    [SerializeField] private TMP_InputField serverNameInput;
    [SerializeField] private TMP_Text serverListText;

    [Header("Player")]
    [SerializeField] private GameObject playerPrefab;

    [Header("Netcode")]
    [SerializeField] private NetworkManager networkManager;

    private Lobby currentLobby;
    private Allocation hostAllocation;

    private bool searching = false;
    private bool isHost = false;

    private string lastJoinCode;

    private async void Start()
    {
        await UnityServices.InitializeAsync();
        await AuthenticationService.Instance.SignInAnonymouslyAsync();

        quickPlayButton.onClick.AddListener(() => _ = QuickPlay());
        createServerButton.onClick.AddListener(() => _ = CreateServer());
        leaveButton.onClick.AddListener(LeaveGame);

        leaveButton.gameObject.SetActive(false);

        networkManager.OnClientConnectedCallback += OnClientConnected;

        StartCoroutine(ServerListUpdater());
    }

    // =====================================================
    // 🔵 QUICK PLAY (RANDOM MATCHMAKING)
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
                QueryResponse response = await LobbyService.Instance.QueryLobbiesAsync();

                if (response.Results.Count == 0)
                {
                    await Task.Delay(2000);
                    continue;
                }

                // random server
                int index = UnityEngine.Random.Range(0, response.Results.Count);
                Lobby lobby = response.Results[index];

                if (!lobby.Data.ContainsKey("joinCode"))
                {
                    await Task.Delay(500);
                    continue;
                }

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
                    await Task.Delay(500);
                }
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

        isHost = true;

        string serverName = string.IsNullOrEmpty(serverNameInput.text)
            ? "Unnamed Server"
            : serverNameInput.text;

        hostAllocation = await RelayService.Instance.CreateAllocationAsync(4);
        string joinCode = await RelayService.Instance.GetJoinCodeAsync(hostAllocation.AllocationId);

        lastJoinCode = joinCode;

        var options = new CreateLobbyOptions
        {
            IsPrivate = false,
            Data = new Dictionary<string, DataObject>
            {
                {
                    "joinCode",
                    new DataObject(DataObject.VisibilityOptions.Public, joinCode)
                },
                {
                    "serverName",
                    new DataObject(DataObject.VisibilityOptions.Public, serverName)
                }
            }
        };

        currentLobby = await LobbyService.Instance.CreateLobbyAsync(serverName, 4, options);

        await Task.Delay(300);

        StartHost(hostAllocation);

        leaveButton.gameObject.SetActive(true);
    }

    // =====================================================
    // 🔴 LEAVE + CLEANUP
    // =====================================================
    private async void LeaveGame()
    {
        searching = false;

        if (isHost)
        {
            try
            {
                if (currentLobby != null)
                    await LobbyService.Instance.DeleteLobbyAsync(currentLobby.Id);
            }
            catch { }

            isHost = false;
        }

        networkManager.Shutdown();

        currentLobby = null;
        hostAllocation = null;

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
    // 🧍 PLAYER SPAWN
    // =====================================================
    private void OnClientConnected(ulong clientId)
    {
        if (!networkManager.IsServer) return;

        GameObject obj = Instantiate(playerPrefab);
        obj.GetComponent<NetworkObject>().SpawnWithOwnership(clientId);
    }

    // =====================================================
    // 📡 SERVER LIST (MULTIPLE SERVERS UI)
    // =====================================================
    private IEnumerator ServerListUpdater()
    {
        while (true)
        {
            _ = UpdateServerList();
            yield return new WaitForSeconds(5f);
        }
    }

    private async Task UpdateServerList()
    {
        if (networkManager.IsClient || networkManager.IsHost)
        {
            serverListText.text = "";
            return;
        }

        try
        {
            QueryResponse response = await LobbyService.Instance.QueryLobbiesAsync();

            if (response.Results.Count == 0)
            {
                serverListText.text = "No servers online";
                return;
            }

            string output = "";

            foreach (var lobby in response.Results)
            {
                string name = lobby.Data.ContainsKey("serverName")
                    ? lobby.Data["serverName"].Value
                    : "Unnamed Server";

                output += $"Server: {name} - Joinable\n";
            }

            serverListText.text = output;
        }
        catch
        {
            serverListText.text = "Server list unavailable";
        }
    }

    // =====================================================
    // 🔴 CLEAN EXIT (GAME CLOSED)
    // =====================================================
    private async void OnApplicationQuit()
    {
        if (isHost)
        {
            try
            {
                if (currentLobby != null)
                    await LobbyService.Instance.DeleteLobbyAsync(currentLobby.Id);
            }
            catch { }

            try
            {
                networkManager.Shutdown();
            }
            catch { }
        }
    }
}