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

    [Header("DEBUG TEXT")]
    [SerializeField] private TMP_Text debugText;

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
    private Dictionary<string, GameObject> lobbyButtons = new Dictionary<string, GameObject>();

    private bool isRefreshing = false;
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

    // =====================================================
    // QUICK PLAY (UNCHANGED)
    // =====================================================
    private async Task QuickPlay()
    {
        if (networkManager.IsClient || networkManager.IsHost)
            return;

        quickPlayButton.gameObject.SetActive(false);
        createServerButton.gameObject.SetActive(false);

        if (serverNameInput != null)
            serverNameInput.gameObject.SetActive(false);

        searching = true;

        float elapsed = 0f;

        while (searching)
        {
            elapsed += 1f;
            float remaining = 10f - elapsed;

            ShowDebug("Searching " + Mathf.CeilToInt(remaining));

            if (remaining <= 0f)
            {
                ShowDebug("No servers online");

                await Task.Delay(3000);

                HideDebug();

                quickPlayButton.gameObject.SetActive(true);
                createServerButton.gameObject.SetActive(true);

                if (serverNameInput != null)
                    serverNameInput.gameObject.SetActive(true);

                searching = false;
                return;
            }

            QueryResponse response =
                await LobbyService.Instance.QueryLobbiesAsync(new QueryLobbiesOptions { Count = 50 });

            List<Lobby> valid = new List<Lobby>();

            foreach (var l in response.Results)
            {
                if (l.Data.ContainsKey("joinCode"))
                    valid.Add(l);
            }

            if (valid.Count > 0)
            {
                Lobby lobby = valid[Random.Range(0, valid.Count)];

                string joinCode = lobby.Data["joinCode"].Value;

                await LobbyService.Instance.JoinLobbyByIdAsync(lobby.Id);

                JoinAllocation allocation =
                    await RelayService.Instance.JoinAllocationAsync(joinCode);

                HideDebug();

                StartClient(allocation);

                currentLobbyId = lobby.Id;
                currentServerName = lobby.Data.ContainsKey("serverName")
                    ? lobby.Data["serverName"].Value
                    : "Server";

                SetStatus("Joined server: " + currentServerName);

                leaveButton.gameObject.SetActive(true);

                inMatch = true;
                SetServerListVisible(false);
                ClearButtons();

                searching = false;
                return;
            }

            await Task.Delay(1000);
        }
    }

    // =====================================================
    // CREATE SERVER (UNCHANGED)
    // =====================================================
    private async Task CreateServer()
    {
        if (networkManager.IsClient || networkManager.IsHost)
            return;

        quickPlayButton.gameObject.SetActive(false);
        createServerButton.gameObject.SetActive(false);

        string serverName =
            string.IsNullOrEmpty(serverNameInput != null ? serverNameInput.text : "")
                ? ""
                : serverNameInput.text;

        if (string.IsNullOrEmpty(serverName))
        {
            ShowDebug("Enter name");

            quickPlayButton.gameObject.SetActive(true);
            createServerButton.gameObject.SetActive(true);

            if (serverNameInput != null)
                serverNameInput.gameObject.SetActive(true);

            return;
        }

        if (serverName.Length > 10)
        {
            ShowDebug("Max 10 letters");

            quickPlayButton.gameObject.SetActive(true);
            createServerButton.gameObject.SetActive(true);

            if (serverNameInput != null)
                serverNameInput.gameObject.SetActive(true);

            return;
        }

        if (serverNameInput != null)
            serverNameInput.gameObject.SetActive(false);

        isHost = true;
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

        HideDebug();

        StartHost(hostAllocation);

        leaveButton.gameObject.SetActive(true);

        inMatch = true;
        SetServerListVisible(false);
        ClearButtons();

        SetStatus("Server: " + serverName);

        await UpdatePlayerCount();
    }

    // =====================================================
    // LEAVE (UNCHANGED)
    // =====================================================
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

        quickPlayButton.gameObject.SetActive(true);
        createServerButton.gameObject.SetActive(true);

        if (serverNameInput != null)
            serverNameInput.gameObject.SetActive(true);

        SetServerListVisible(true);

        SetStatus("Browsing servers...");
    }

    // =====================================================
    // HOST / CLIENT (UNCHANGED)
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
    // PLAYER EVENTS
    // =====================================================
    private async void OnClientConnected(ulong clientId)
    {
        if (!networkManager.IsServer) return;

        GameObject obj = Instantiate(playerPrefab);
        obj.GetComponent<NetworkObject>().SpawnWithOwnership(clientId);

        await Task.Delay(200);
        await UpdatePlayerCount();
    }

    private async void OnClientDisconnected(ulong clientId)
    {
        if (!networkManager.IsServer)
        {
            // CLIENT SIDE: HOST LEFT
            ShowDebug("Host Leaved");

            leaveButton.gameObject.SetActive(false);

            inMatch = false;
            searching = false;

            quickPlayButton.gameObject.SetActive(true);
            createServerButton.gameObject.SetActive(true);

            if (serverNameInput != null)
                serverNameInput.gameObject.SetActive(true);

            SetServerListVisible(true);

            currentLobby = null;
            hostAllocation = null;
            currentLobbyId = "";
            currentServerName = "";

            ClearButtons();

            SetStatus("Browsing servers..."); // ✅ FIX ADDED

            await Task.Delay(2000);
            HideDebug();

            return;
        }

        await Task.Delay(200);
        await UpdatePlayerCount();
    }

    // =====================================================
    // PLAYER COUNT
    // =====================================================
    private async Task UpdatePlayerCount()
    {
        if (!isHost || currentLobby == null) return;

        int count = networkManager.ConnectedClientsIds.Count;

        try
        {
            await LobbyService.Instance.UpdateLobbyAsync(
                currentLobby.Id,
                new UpdateLobbyOptions
                {
                    Data = new Dictionary<string, DataObject>
                    {
                        {
                            "playerCount",
                            new DataObject(DataObject.VisibilityOptions.Public, count.ToString())
                        }
                    }
                }
            );
        }
        catch { }
    }

    // =====================================================
    // SERVER LIST + HELPERS (UNCHANGED)
    // =====================================================
    private IEnumerator ServerListUpdater()
    {
        while (true)
        {
            _ = UpdateServerList();
            yield return new WaitForSeconds(2f);
        }
    }

    private async Task UpdateServerList()
    {
        if (inMatch || isRefreshing) return;

        isRefreshing = true;

        QueryResponse response =
            await LobbyService.Instance.QueryLobbiesAsync(new QueryLobbiesOptions { Count = 50 });

        HashSet<string> activeLobbies = new HashSet<string>();

        foreach (var lobby in response.Results)
        {
            if (!lobby.Data.ContainsKey("joinCode"))
                continue;

            string lobbyId = lobby.Id;
            activeLobbies.Add(lobbyId);

            string serverName = lobby.Data.ContainsKey("serverName")
                ? lobby.Data["serverName"].Value
                : "Server";

            string joinCode = lobby.Data["joinCode"].Value;

            int playerCount = 1;

            if (lobby.Data.ContainsKey("playerCount"))
                int.TryParse(lobby.Data["playerCount"].Value, out playerCount);

            if (!lobbyButtons.ContainsKey(lobbyId))
            {
                GameObject btn = CreateServerButton(serverName, joinCode, playerCount);
                lobbyButtons[lobbyId] = btn;
            }
            else
            {
                TMP_Text text = lobbyButtons[lobbyId].GetComponentInChildren<TMP_Text>();
                if (text != null)
                    text.text = $"Server: {serverName} | Players: {playerCount}";
            }
        }

        List<string> toRemove = new List<string>();

        foreach (var kvp in lobbyButtons)
        {
            if (!activeLobbies.Contains(kvp.Key))
                toRemove.Add(kvp.Key);
        }

        foreach (string id in toRemove)
        {
            Destroy(lobbyButtons[id]);
            lobbyButtons.Remove(id);
        }

        isRefreshing = false;
    }

    private float buttonSpacing = 80f;

    private GameObject CreateServerButton(string serverName, string joinCode, int playerCount)
    {
        GameObject obj = Instantiate(serverButtonPrefab, serverListParent);
        spawnedButtons.Add(obj);

        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchoredPosition = new Vector2(0, -spawnedButtons.Count * buttonSpacing);

        TMP_Text text = obj.GetComponentInChildren<TMP_Text>();
        if (text != null)
            text.text = $"Server: {serverName} | Players: {playerCount}";

        Button btn = obj.GetComponent<Button>();
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => _ = JoinServer(joinCode));

        return obj;
    }

    private async Task JoinServer(string joinCode)
    {
        if (networkManager.IsClient || networkManager.IsHost)
            return;

        JoinAllocation allocation =
            await RelayService.Instance.JoinAllocationAsync(joinCode);

        StartClient(allocation);

        SetStatus("Joined server");

        leaveButton.gameObject.SetActive(true);

        inMatch = true;
        SetServerListVisible(false);
        ClearButtons();

        await Task.Delay(200);
        await UpdatePlayerCount();
    }

    private void ClearButtons()
    {
        foreach (var b in spawnedButtons)
            Destroy(b);

        spawnedButtons.Clear();
        lobbyButtons.Clear();
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

    private void ShowDebug(string msg)
    {
        if (debugText == null) return;
        debugText.gameObject.SetActive(true);
        debugText.text = msg;
    }

    private void HideDebug()
    {
        if (debugText == null) return;
        debugText.text = "";
        debugText.gameObject.SetActive(false);
    }
}