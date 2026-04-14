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

    [Header("UI Layout")]
    [SerializeField] private float buttonSpacing = 80f;

    private Lobby currentLobby;
    private Allocation hostAllocation;

    private bool searching = false;
    private bool isHost = false;
    private bool inMatch = false;

    private string currentServerName = "";

    private List<GameObject> spawnedButtons = new List<GameObject>();
    private bool isRefreshing = false;

    // 🔥 FIX: heartbeat timer toegevoegd
    private float heartbeatTimer;

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

        SetStatus("Browsing servers...");
    }

    // =====================================================
    // 🔥 HEARTBEAT FIX (BELANGRIJK)
    // =====================================================
    private void Update()
    {
        if (!isHost || currentLobby == null)
            return;

        heartbeatTimer += Time.deltaTime;

        if (heartbeatTimer >= 15f)
        {
            heartbeatTimer = 0f;
            _ = LobbyService.Instance.SendHeartbeatPingAsync(currentLobby.Id);
        }
    }

    // =====================================================
    // QUICK PLAY
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
                QueryLobbiesOptions options = new QueryLobbiesOptions { Count = 50 };
                QueryResponse response = await LobbyService.Instance.QueryLobbiesAsync(options);

                if (response.Results.Count == 0)
                {
                    await Task.Delay(2000);
                    continue;
                }

                int index = Random.Range(0, response.Results.Count);
                Lobby lobby = response.Results[index];

                if (!lobby.Data.ContainsKey("joinCode"))
                {
                    await Task.Delay(1000);
                    continue;
                }

                string code = lobby.Data["joinCode"].Value;
                string name = lobby.Data.ContainsKey("serverName")
                    ? lobby.Data["serverName"].Value
                    : "Server";

                JoinAllocation allocation =
                    await RelayService.Instance.JoinAllocationAsync(code);

                StartClient(allocation);

                currentServerName = name;
                SetStatus("Joined server: " + currentServerName);

                leaveButton.gameObject.SetActive(true);

                inMatch = true;
                SetServerListVisible(false);
                ClearButtons();

                searching = false;
                return;
            }
            catch
            {
                await Task.Delay(2000);
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

        isHost = true;

        string serverName =
            string.IsNullOrEmpty(serverNameInput != null ? serverNameInput.text : "")
                ? "Server " + Random.Range(100, 999)
                : serverNameInput.text;

        currentServerName = serverName;

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

        SetStatus("Server: " + currentServerName);

        inMatch = true;
        SetServerListVisible(false);
        ClearButtons();
    }

    // =====================================================
    // LEAVE
    // =====================================================
    private async void LeaveGame()
    {
        searching = false;

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

        leaveButton.gameObject.SetActive(false);

        inMatch = false;
        currentServerName = "";

        SetStatus("Browsing servers...");

        SetServerListVisible(true);

        StartCoroutine(ServerListUpdater());
    }

    // =====================================================
    // HOST
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
    // CLIENT
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
    // PLAYER SPAWN
    // =====================================================
    private void OnClientConnected(ulong clientId)
    {
        if (!networkManager.IsServer) return;

        GameObject obj = Instantiate(playerPrefab);
        obj.GetComponent<NetworkObject>().SpawnWithOwnership(clientId);
    }

    // =====================================================
    // SERVER LIST
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
        if (inMatch)
            return;

        if (isRefreshing)
            return;

        isRefreshing = true;

        try
        {
            QueryLobbiesOptions options = new QueryLobbiesOptions { Count = 50 };
            QueryResponse response = await LobbyService.Instance.QueryLobbiesAsync(options);

            ClearButtons();

            foreach (var lobby in response.Results)
            {
                if (!lobby.Data.ContainsKey("joinCode"))
                    continue;

                string serverName = lobby.Data.ContainsKey("serverName")
                    ? lobby.Data["serverName"].Value
                    : "Server";

                string joinCode = lobby.Data["joinCode"].Value;

                CreateServerButton(serverName, joinCode);
            }
        }
        catch
        {
            Debug.Log("Server list error (ignored)");
        }

        isRefreshing = false;
    }

    private void CreateServerButton(string serverName, string joinCode)
    {
        GameObject obj = Instantiate(serverButtonPrefab, serverListParent);
        spawnedButtons.Add(obj);

        TMP_Text text = obj.GetComponentInChildren<TMP_Text>();
        if (text != null)
            text.text = "Server: " + serverName;

        Button btn = obj.GetComponent<Button>();
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => _ = JoinServer(joinCode));

        RectTransform rt = obj.GetComponent<RectTransform>();
        int index = spawnedButtons.Count - 1;

        rt.anchoredPosition = new Vector2(0f, -index * buttonSpacing);
    }

    private async Task JoinServer(string joinCode)
    {
        if (networkManager.IsClient || networkManager.IsHost)
            return;

        try
        {
            QueryLobbiesOptions options = new QueryLobbiesOptions { Count = 50 };
            QueryResponse response = await LobbyService.Instance.QueryLobbiesAsync(options);

            Lobby found = null;

            foreach (var l in response.Results)
            {
                if (l.Data.ContainsKey("joinCode") && l.Data["joinCode"].Value == joinCode)
                {
                    found = l;
                    break;
                }
            }

            string serverName =
                found != null && found.Data.ContainsKey("serverName")
                    ? found.Data["serverName"].Value
                    : "Server";

            JoinAllocation allocation =
                await RelayService.Instance.JoinAllocationAsync(joinCode);

            StartClient(allocation);

            currentServerName = serverName;
            SetStatus("Joined server: " + currentServerName);

            leaveButton.gameObject.SetActive(true);

            inMatch = true;
            SetServerListVisible(false);
            ClearButtons();
        }
        catch
        {
            Debug.Log("Join failed");
        }
    }

    // =====================================================
    // HELPERS
    // =====================================================
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