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

public class MultiplayerMenu : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Button quickPlayButton;
    [SerializeField] private Button createServerButton;
    [SerializeField] private Button leaveButton;

    [Header("Server UI")]
    [SerializeField] private Transform serverListParent;
    [SerializeField] private GameObject serverButtonPrefab;

    [Header("Optional Input")]
    [SerializeField] private TMP_InputField serverNameInput;

    [Header("Status Text")]
    [SerializeField] private TMP_Text statusText;

    [Header("Player")]
    [SerializeField] private GameObject playerPrefab;

    [Header("Netcode")]
    [SerializeField] private NetworkManager networkManager;

    private Lobby currentLobby;
    private Allocation hostAllocation;

    private bool isHost = false;
    private bool inMatch = false;
    private bool searching = false;

    private string currentLobbyId = "";
    private string currentServerName = "";

    private List<GameObject> spawnedButtons = new List<GameObject>();

    private bool isRefreshing = false;

    private float heartbeatTimer;

    // 🔥 NEW: UI spacing system
    private float buttonSpacingY = -60f;

    private async void Start()
    {
        await UnityServices.InitializeAsync();
        await AuthenticationService.Instance.SignInAnonymouslyAsync();

        quickPlayButton.onClick.AddListener(() => _ = QuickPlay());
        createServerButton.onClick.AddListener(() => _ = CreateServer());
        leaveButton.onClick.AddListener(LeaveGame);

        leaveButton.gameObject.SetActive(false);

        networkManager.OnClientConnectedCallback += OnClientConnected;
        networkManager.OnClientDisconnectCallback += OnClientDisconnected;

        StartCoroutine(ServerListUpdater());
    }

    private void Update()
    {
        if (!isHost || currentLobby == null) return;

        heartbeatTimer += Time.deltaTime;

        if (heartbeatTimer >= 15f)
        {
            heartbeatTimer = 0f;
            _ = LobbyService.Instance.SendHeartbeatPingAsync(currentLobby.Id);
        }
    }

    private async Task QuickPlay()
    {
        if (networkManager.IsClient || networkManager.IsHost)
            return;

        searching = true;

        while (searching)
        {
            QueryResponse response =
                await LobbyService.Instance.QueryLobbiesAsync(new QueryLobbiesOptions { Count = 50 });

            if (response.Results.Count == 0)
            {
                await Task.Delay(2000);
                continue;
            }

            Lobby lobby = response.Results[Random.Range(0, response.Results.Count)];

            if (!lobby.Data.ContainsKey("joinCode"))
                continue;

            string joinCode = lobby.Data["joinCode"].Value;

            await LobbyService.Instance.JoinLobbyByIdAsync(lobby.Id);

            JoinAllocation allocation =
                await RelayService.Instance.JoinAllocationAsync(joinCode);

            StartClient(allocation);

            currentLobbyId = lobby.Id;
            currentServerName = lobby.Data.ContainsKey("serverName")
                ? lobby.Data["serverName"].Value
                : "Server";

            SetStatus("Joined: " + currentServerName);

            leaveButton.gameObject.SetActive(true);

            inMatch = true;
            SetServerListVisible(false);
            ClearButtons();

            searching = false;
            return;
        }
    }

    private async Task CreateServer()
    {
        if (networkManager.IsClient || networkManager.IsHost)
            return;

        isHost = true;

        string serverName =
            string.IsNullOrEmpty(serverNameInput != null ? serverNameInput.text : "")
                ? "Server " + Random.Range(100, 999)
                : serverNameInput.text;

        currentServerName = serverName;

        hostAllocation = await RelayService.Instance.CreateAllocationAsync(4);
        string joinCode = await RelayService.Instance.GetJoinCodeAsync(hostAllocation.AllocationId);

        CreateLobbyOptions options = new CreateLobbyOptions
        {
            IsPrivate = false,
            Data = new Dictionary<string, DataObject>
            {
                { "joinCode", new DataObject(DataObject.VisibilityOptions.Public, joinCode) },
                { "serverName", new DataObject(DataObject.VisibilityOptions.Public, serverName) },
                { "playerCount", new DataObject(DataObject.VisibilityOptions.Public, "1") }
            }
        };

        currentLobby = await LobbyService.Instance.CreateLobbyAsync(serverName, 4, options);

        currentLobbyId = currentLobby.Id;

        StartHost(hostAllocation);

        leaveButton.gameObject.SetActive(true);

        inMatch = true;
        SetServerListVisible(false);
        ClearButtons();

        SetStatus("Server: " + serverName);
    }

    private async void LeaveGame()
    {
        searching = false;

        if (!string.IsNullOrEmpty(currentLobbyId))
        {
            try
            {
                await LobbyService.Instance.RemovePlayerAsync(
                    currentLobbyId,
                    AuthenticationService.Instance.PlayerId
                );
            }
            catch { }
        }

        if (isHost && currentLobby != null)
        {
            try
            {
                await LobbyService.Instance.DeleteLobbyAsync(currentLobby.Id);
            }
            catch { }

            isHost = false;
        }

        networkManager.Shutdown();

        currentLobby = null;
        hostAllocation = null;
        currentLobbyId = "";

        leaveButton.gameObject.SetActive(false);

        inMatch = false;
        currentServerName = "";

        SetStatus("Browsing servers...");
        SetServerListVisible(true);

        StartCoroutine(ServerListUpdater());
    }

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

    private void OnClientConnected(ulong clientId)
    {
        if (!networkManager.IsServer) return;

        GameObject obj = Instantiate(playerPrefab);
        obj.GetComponent<NetworkObject>().SpawnWithOwnership(clientId);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (!networkManager.IsServer) return;
    }

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
        if (inMatch || isRefreshing) return;

        isRefreshing = true;

        try
        {
            QueryResponse response =
                await LobbyService.Instance.QueryLobbiesAsync(new QueryLobbiesOptions { Count = 50 });

            ClearButtons();

            int index = 0;

            foreach (var lobby in response.Results)
            {
                if (!lobby.Data.ContainsKey("joinCode"))
                    continue;

                string serverName = lobby.Data.ContainsKey("serverName")
                    ? lobby.Data["serverName"].Value
                    : "Server";

                string joinCode = lobby.Data["joinCode"].Value;

                int playerCount = 1;

                if (lobby.Data.ContainsKey("playerCount"))
                    int.TryParse(lobby.Data["playerCount"].Value, out playerCount);

                CreateServerButton(serverName, joinCode, playerCount, index);
                index++;
            }
        }
        catch
        {
            Debug.Log("Server list error");
        }

        isRefreshing = false;
    }

    // 🔥 FIX: stacked UI positioning
    private void CreateServerButton(string serverName, string joinCode, int playerCount, int index)
    {
        GameObject obj = Instantiate(serverButtonPrefab, serverListParent);
        spawnedButtons.Add(obj);

        // 🔥 POSITION FIX (UNDER EACH OTHER)
        RectTransform rt = obj.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchoredPosition = new Vector2(0, index * buttonSpacingY);
        }

        TMP_Text text = obj.GetComponentInChildren<TMP_Text>();
        if (text != null)
            text.text = $"Server: {serverName} | Players: {playerCount}";

        Button btn = obj.GetComponent<Button>();
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => _ = JoinServer(joinCode));
    }

    private async Task JoinServer(string joinCode)
    {
        if (networkManager.IsClient || networkManager.IsHost)
            return;

        JoinAllocation allocation =
            await RelayService.Instance.JoinAllocationAsync(joinCode);

        StartClient(allocation);

        SetStatus("Joined: " + currentServerName);

        leaveButton.gameObject.SetActive(true);

        inMatch = true;
        SetServerListVisible(false);
        ClearButtons();
    }

    private void ClearButtons()
    {
        foreach (var b in spawnedButtons)
            Destroy(b);

        spawnedButtons.Clear();
    }

    private void SetServerListVisible(bool visible)
    {
        if (serverListParent != null)
            serverListParent.gameObject.SetActive(visible);
    }

    private void SetStatus(string text)
    {
        if (statusText != null)
            statusText.text = text;
    }
}