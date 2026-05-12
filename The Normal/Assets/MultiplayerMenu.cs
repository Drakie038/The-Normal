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
    private CameraMovement cameraMovement;

    [Header("UI - Existing")]
    [SerializeField] private Button quickPlayButton;
    [SerializeField] private Button createServerButton;
    [SerializeField] private Button leaveButton;
    [SerializeField] private Button ResumeButton;

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

    [Header("MENU SETUP")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button multiplayerButton;
    [SerializeField] private Button singleplayerButton;

    [Header("GROUPS")]
    [SerializeField] private GameObject groupSinglePlayer;
    [SerializeField] private GameObject groupMultiplayer;
    [SerializeField] private GameObject SettingsGroup;

    [Header("Singleplayer")]
    [SerializeField] private Button startGameButton;

    [Header("Multiplayer Extra Buttons")]
    [SerializeField] private Button browserRoomsButton;

    [Header("ADDITIONAL MENU BUTTONS")]
    [SerializeField] private Button quickJoinButton;
    [SerializeField] private Button menuCreateServerButton;
    [SerializeField] private Button StopSearchingButton;

    [Header("BACK BUTTON")]
    [SerializeField] private Button backButton;

    private Stack<System.Action> backStack = new Stack<System.Action>();

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

    private int sessionId = 0;
    private bool forceCancelled = false;

    private bool isSingleplayer = false;
    private GameObject singleplayerPlayer;

    private bool isResettingOrLeaving = false;
    private bool isResetting = false;

    private Coroutine serverListRoutine;

    private void OnEnable()
    {
        if (networkManager != null)
        {
            networkManager.OnClientDisconnectCallback += HandleClientDisconnect;
        }
    }

    private void OnDisable()
    {
        if (networkManager != null)
        {
            networkManager.OnClientDisconnectCallback -= HandleClientDisconnect;
        }
    }

    private void Start()
    {
        RegisterCallbacks();

        UnityServices.InitializeAsync();
        cameraMovement = FindObjectOfType<CameraMovement>();

        AuthenticationService.Instance.SignInAnonymouslyAsync();

        quickPlayButton.onClick.AddListener(() => _ = QuickPlay());
        createServerButton.onClick.AddListener(() => _ = CreateServer());

        leaveButton.onClick.AddListener(LeaveGame);
        ResumeButton.onClick.AddListener(CloseSettingsMenu);

        StopSearchingButton.onClick.AddListener(StopSearching);

        StartCoroutine(ServerListUpdater());

        SetupMenu();
    }

    private void SetupMenu()
    {
        startButton.gameObject.SetActive(true);
        backButton.gameObject.SetActive(false);

        multiplayerButton.gameObject.SetActive(false);
        singleplayerButton.gameObject.SetActive(false);

        groupSinglePlayer.SetActive(false);
        groupMultiplayer.SetActive(false);

        quickJoinButton.gameObject.SetActive(false);
        browserRoomsButton.gameObject.SetActive(false);
        menuCreateServerButton.gameObject.SetActive(false);
        createServerButton.gameObject.SetActive(false);

        StopSearchingButton.gameObject.SetActive(false);

        if (serverNameInput != null)
            serverNameInput.gameObject.SetActive(false);

        if (debugText != null)
            debugText.gameObject.SetActive(false);

        serverListParent.gameObject.SetActive(false);

        // START
        startButton.onClick.AddListener(() =>
        {
            startButton.gameObject.SetActive(false);

            multiplayerButton.gameObject.SetActive(true);
            singleplayerButton.gameObject.SetActive(true);

            backButton.gameObject.SetActive(true);

            backStack.Push(() =>
            {
                startButton.gameObject.SetActive(true);
                multiplayerButton.gameObject.SetActive(false);
                singleplayerButton.gameObject.SetActive(false);
                backButton.gameObject.SetActive(false);
            });
        });

        // MULTIPLAYER
        multiplayerButton.onClick.AddListener(() =>
        {
            groupMultiplayer.gameObject.SetActive(true);
            multiplayerButton.gameObject.SetActive(false);
            singleplayerButton.gameObject.SetActive(false);
            statusText.gameObject.SetActive(false);
            

            quickJoinButton.gameObject.SetActive(true);
            browserRoomsButton.gameObject.SetActive(true);
            menuCreateServerButton.gameObject.SetActive(true);

            backStack.Push(() =>
            {
                multiplayerButton.gameObject.SetActive(true);
                singleplayerButton.gameObject.SetActive(true);

                quickJoinButton.gameObject.SetActive(false);
                browserRoomsButton.gameObject.SetActive(false);
                menuCreateServerButton.gameObject.SetActive(false);
            });
        });

        // QUICK JOIN
        if (quickJoinButton != null)
        {
            quickJoinButton.onClick.AddListener(() =>
            {
                _ = QuickPlay();
            });
        }

        // CREATE MENU
        if (menuCreateServerButton != null)
        {
            menuCreateServerButton.onClick.AddListener(() =>
            {
                quickJoinButton.gameObject.SetActive(false);
                browserRoomsButton.gameObject.SetActive(false);
                menuCreateServerButton.gameObject.SetActive(false);

                createServerButton.gameObject.SetActive(true);

                if (serverNameInput != null)
                    serverNameInput.gameObject.SetActive(true);

                if (debugText != null)
                    debugText.gameObject.SetActive(true);

                serverListParent.gameObject.SetActive(false);

                backStack.Push(() =>
                {
                    createServerButton.gameObject.SetActive(false);

                    if (serverNameInput != null)
                        serverNameInput.gameObject.SetActive(false);

                    if (debugText != null)
                        debugText.gameObject.SetActive(false);

                    quickJoinButton.gameObject.SetActive(true);
                    browserRoomsButton.gameObject.SetActive(true);
                    menuCreateServerButton.gameObject.SetActive(true);
                });
            });
        }

        // BROWSER ROOMS
        if (browserRoomsButton != null)
        {
            browserRoomsButton.onClick.AddListener(() =>
            {
                serverListParent.gameObject.SetActive(true);

                // 🔥 FORCE CLEAN REFRESH
                ClearButtons();
                _ = UpdateServerList();

                quickJoinButton.gameObject.SetActive(false);
                browserRoomsButton.gameObject.SetActive(false);
                menuCreateServerButton.gameObject.SetActive(false);

                // 🔥 FIX
                createServerButton.gameObject.SetActive(false);

                if (serverNameInput != null)
                    serverNameInput.gameObject.SetActive(false);

                if (debugText != null)
                    debugText.gameObject.SetActive(false);

                backStack.Push(() =>
                {
                    serverListParent.gameObject.SetActive(false);

                    quickJoinButton.gameObject.SetActive(true);
                    browserRoomsButton.gameObject.SetActive(true);
                    menuCreateServerButton.gameObject.SetActive(true);
                });
            });
        }

        // SINGLEPLAYER
        if (singleplayerButton != null)
        {
            singleplayerButton.onClick.AddListener(() =>
            {
                groupSinglePlayer.SetActive(true);
                groupMultiplayer.SetActive(false);

                multiplayerButton.gameObject.SetActive(false);
                singleplayerButton.gameObject.SetActive(false);

                backStack.Push(() =>
                {
                    groupSinglePlayer.SetActive(false);

                    multiplayerButton.gameObject.SetActive(true);
                    singleplayerButton.gameObject.SetActive(true);
                });
            });


        }

        // BACK BUTTON
        if (backButton != null)
        {
            backButton.onClick.AddListener(() =>
            {
                if (backStack.Count > 0)
                {
                    var action = backStack.Pop();
                    action.Invoke();
                }
            });
        }


        if (startGameButton != null)
        {
            startGameButton.onClick.AddListener(() =>
            {
                StartSingleplayer();
            });
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1))
        {
            Debug.Log("IsClient: " + networkManager.IsClient);
            Debug.Log("IsHost: " + networkManager.IsHost);
            Debug.Log("IsListening: " + networkManager.IsListening);
        }

        if (networkManager != null &&
            (networkManager.IsClient || networkManager.IsHost) &&
            Input.GetKeyDown(KeyCode.Escape))
        {
            if (SettingsGroup == null) return;

            if (SettingsGroup.activeSelf)
                CloseSettingsMenu();
            else
                OpenSettingsMenu();
        }

        // HOST HEARTBEAT
        if (isHost && currentLobby != null)
        {
            heartbeatTimer += Time.deltaTime;

            if (heartbeatTimer >= 15f)
            {
                heartbeatTimer = 0f;
                _ = LobbyService.Instance.SendHeartbeatPingAsync(currentLobby.Id);
            }
        }
    }

    private async Task QuickPlay()
    {
        if (isResetting) return;
        
        if (networkManager.IsListening)
            return;

        int mySession = ++sessionId; // unieke sessie voor deze run
        forceCancelled = false;
        searching = true;

        quickJoinButton.gameObject.SetActive(false);
        browserRoomsButton.gameObject.SetActive(false);
        menuCreateServerButton.gameObject.SetActive(false);
        backButton.gameObject.SetActive(false);

        StopSearchingButton.gameObject.SetActive(true);

        float startTime = Time.realtimeSinceStartup;

        while (searching)
        {
            // ❌ HARD CANCEL CHECK
            if (forceCancelled || mySession != sessionId)
            {
                StopSearchingButton.gameObject.SetActive(false);
                return;
            }

            try
            {
                float elapsed = Time.realtimeSinceStartup - startTime;
                float remaining = 10f - elapsed;

                ShowDebug("Searching " + Mathf.CeilToInt(remaining));

                if (remaining <= 0f)
                {
                    if (forceCancelled || mySession != sessionId)
                        return;

                    ShowDebug("No servers online");
                    await Task.Delay(3000);

                    if (forceCancelled || mySession != sessionId)
                        return;

                    HideDebug();

                    quickJoinButton.gameObject.SetActive(true);
                    browserRoomsButton.gameObject.SetActive(true);
                    menuCreateServerButton.gameObject.SetActive(true);
                    backButton.gameObject.SetActive(true);

                    StopSearchingButton.gameObject.SetActive(false);

                    searching = false;
                    return;
                }

                QueryResponse response =
                    await LobbyService.Instance.QueryLobbiesAsync(
                        new QueryLobbiesOptions { Count = 50 }
                    );

                // ❌ CHECK NA AWAIT
                if (forceCancelled || mySession != sessionId)
                    return;

                List<Lobby> valid = new List<Lobby>();

                foreach (var l in response.Results)
                {
                    if (l.Data != null && l.Data.ContainsKey("joinCode"))
                        valid.Add(l);
                }

                if (valid.Count > 0)
                {
                    // ❌ FINAL CHECK VOOR JOIN
                    if (forceCancelled || mySession != sessionId)
                        return;

                    Lobby lobby = valid[Random.Range(0, valid.Count)];
                    string joinCode = lobby.Data["joinCode"].Value;

                    await LobbyService.Instance.JoinLobbyByIdAsync(lobby.Id);

                    if (forceCancelled || mySession != sessionId)
                    {
                        try
                        {
                            await LobbyService.Instance.RemovePlayerAsync(
                                lobby.Id,
                                AuthenticationService.Instance.PlayerId
                            );
                        }
                        catch { }

                        return;
                    }

                    JoinAllocation allocation =
                        await RelayService.Instance.JoinAllocationAsync(joinCode);

                    if (forceCancelled || mySession != sessionId)
                        return;

                    HideDebug();
                    if (!TryStartClient(allocation))
                    {
                        throw new System.Exception("Client failed to start");
                    }

                    currentLobbyId = lobby.Id;

                    currentServerName = lobby.Data.ContainsKey("serverName")
                        ? lobby.Data["serverName"].Value
                        : "Server";

                    SetStatus("Joined server: " + currentServerName);

                    statusText.gameObject.SetActive(true);

                    SetInMatchUI(true);

                    StopSearchingButton.gameObject.SetActive(false);

                    searching = false;
                    return;
                }

                await Task.Delay(1000);
            }
            catch (System.Exception e)
            {
                if (forceCancelled || mySession != sessionId)
                    return;

                Debug.LogWarning("QuickPlay error (ignored): " + e.Message);

                await Task.Delay(500);
            }
        }

        StopSearchingButton.gameObject.SetActive(false);
    }

    private async Task CreateServer()
    {
        if (networkManager.IsListening)
            return;

        quickPlayButton.gameObject.SetActive(false);
        createServerButton.gameObject.SetActive(false);
        menuCreateServerButton.gameObject.SetActive(false);

        string serverName =
            string.IsNullOrEmpty(serverNameInput != null ? serverNameInput.text : "")
                ? ""
                : serverNameInput.text;

        if (string.IsNullOrEmpty(serverName))
        {
            ShowDebug("Enter name");

            createServerButton.gameObject.SetActive(true);

            if (serverNameInput != null)
                serverNameInput.gameObject.SetActive(true);

            return;
        }

        if (serverName.Length > 10)
        {
            ShowDebug("Max 10 letters");

            createServerButton.gameObject.SetActive(true);

            if (serverNameInput != null)
                serverNameInput.gameObject.SetActive(true);

            return;
        }

        if (serverNameInput != null)
            serverNameInput.gameObject.SetActive(false);
            backButton.gameObject.SetActive(false);

        if (debugText != null)
            debugText.gameObject.SetActive(false);

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

        statusText.gameObject.SetActive(true);
        menuCreateServerButton.gameObject.SetActive(false);

        SetInMatchUI(true);

        SetStatus("Server: " + serverName);

        await UpdatePlayerCount();
    }

    private async void LeaveGame()
    {
        if (isResettingOrLeaving)
            return;

        isResettingOrLeaving = true;

        forceCancelled = true;
        searching = false;

        sessionId++;

        SetInMatchUI(false);

        CloseSettingsMenu();

        // lobby cleanup
        try
        {
            if (!string.IsNullOrEmpty(currentLobbyId))
            {
                if (isHost)
                {
                    await LobbyService.Instance.DeleteLobbyAsync(currentLobbyId);
                }
                else
                {
                    await LobbyService.Instance.RemovePlayerAsync(
                        currentLobbyId,
                        AuthenticationService.Instance.PlayerId
                    );
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Lobby cleanup failed: " + e.Message);
        }

        // volledige NGO reset
        await FullNetworkResetAsync();

        float t = 0;
        while (networkManager != null && networkManager.IsListening && t < 5f)
        {
            await Task.Delay(100);
            t += 0.1f;
        }

        Debug.Log("Netcode fully reset");

        // alles clean resetten
        ForceFullReset();
    }
    private void StartHost(Allocation allocation)
    {
        RegisterCallbacks();

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
        RegisterCallbacks();

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

    private async void OnClientConnected(ulong clientId)
    {
        if (!NetworkManager.Singleton.IsServer)
            return;

        GameObject obj = Instantiate(playerPrefab);
        obj.GetComponent<NetworkObject>().SpawnWithOwnership(clientId);

        await Task.Delay(100);

        _ = UpdatePlayerCount();
    }

    private void HandleClientDisconnect(ulong clientId)
    {
        if (isResettingOrLeaving || isResetting)
            return;

        Debug.LogWarning("DISCONNECT DETECTED → GLOBAL RESET");

        _ = FullResetFromAnyError();
    }

    private async Task UpdatePlayerCount()
    {
        if (currentLobby == null)
            return;

        if (NetworkManager.Singleton == null)
            return;

        try
        {
            int count = NetworkManager.Singleton.ConnectedClientsIds.Count;

            await LobbyService.Instance.UpdateLobbyAsync(
                currentLobby.Id,
                new UpdateLobbyOptions
                {
                    Data = new Dictionary<string, DataObject>
                    {
                    {
                        "playerCount",
                        new DataObject(
                            DataObject.VisibilityOptions.Public,
                            count.ToString()
                        )
                    }
                    }
                }
            );
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("PlayerCount update failed: " + e.Message);
        }
    }

    private IEnumerator ServerListUpdater()
    {
        while (true)
        {
            if (!inMatch)
            {
                _ = UpdateServerList();
            }

            yield return new WaitForSeconds(2f);
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
            QueryResponse response =
                await LobbyService.Instance.QueryLobbiesAsync(
                    new QueryLobbiesOptions { Count = 50 }
                );

            // 🔥 HARD RESET UI (fixes ghost buttons)
            foreach (var obj in spawnedButtons)
            {
                if (obj != null)
                    Destroy(obj);
            }

            spawnedButtons.Clear();
            lobbyButtons.Clear();

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

                GameObject btn = CreateServerButton(serverName, joinCode, playerCount);
                lobbyButtons[lobbyId] = btn;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Server list error: " + e.Message);
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
        btn.onClick.AddListener(async () =>
        {
            Debug.Log("CLICK JOIN: " + joinCode);

            if (networkManager.IsListening)
                await FullNetworkResetAsync();

            await JoinServer(joinCode);
        });
        return obj;
    }

    private TaskCompletionSource<bool> joinTcs;

    private async Task JoinServer(string joinCode)
    {
        if (networkManager == null)
            return;

        if (networkManager.IsListening)
        {
            await FullNetworkResetAsync();
        }

        if (isResetting || forceCancelled)
            return;

        try
        {
            JoinAllocation allocation =
                await RelayService.Instance.JoinAllocationAsync(joinCode);

            if (isResetting || forceCancelled)
                return;

            TaskCompletionSource<bool> joinTcs = new TaskCompletionSource<bool>();

            void OnConnected(ulong id)
            {
                joinTcs.TrySetResult(true);
            }

            networkManager.OnClientConnectedCallback += OnConnected;

            if (!TryStartClient(allocation))
            {
                networkManager.OnClientConnectedCallback -= OnConnected;
                throw new System.Exception("Client failed to start");
            }

            var completed = await Task.WhenAny(
                joinTcs.Task,
                Task.Delay(8000)
            );

            networkManager.OnClientConnectedCallback -= OnConnected;

            if (completed != joinTcs.Task)
            {
                throw new System.Exception("Connection timeout");
            }

            // ✅ SUCCESS
            SetStatus("Joined server: " + joinCode);
            SetInMatchUI(true);

            inMatch = true;
            SetServerListVisible(false);
            ClearButtons();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("JOIN FAILED → RESET: " + e.Message);
            await FullResetFromAnyError();
        }
    }
    private void ClearButtons()
    {
        for (int i = 0; i < spawnedButtons.Count; i++)
        {
            if (spawnedButtons[i] != null)
                Destroy(spawnedButtons[i]);
        }

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

    private void ResetAllState()
    {

        isSingleplayer = false;

        if (singleplayerPlayer != null)
        {
            Destroy(singleplayerPlayer);
            singleplayerPlayer = null;
            startGameButton.gameObject.SetActive(true);
            groupSinglePlayer.SetActive(true);
        }

        if (cameraMovement != null)
        {
            cameraMovement.ResetCameraToMenu();
        }

        searching = false;
        forceCancelled = true;

        sessionId++; // ❗ invalideert ALLE lopende async processen

        inMatch = false;
        isHost = false;

        currentLobby = null;
        hostAllocation = null;
        currentLobbyId = "";
        currentServerName = "";
        searching = false;
        inMatch = false;
        isHost = false;

        currentLobby = null;
        hostAllocation = null;
        currentLobbyId = "";
        currentServerName = "";

        ClearButtons();

        SetServerListVisible(true);

        // SETTINGS MENU UIT
        if (SettingsGroup != null)
            SettingsGroup.SetActive(false);

        leaveButton.gameObject.SetActive(false);
        ResumeButton.gameObject.SetActive(false);

        StopSearchingButton.gameObject.SetActive(false);

        statusText.gameObject.SetActive(false);

        quickPlayButton.gameObject.SetActive(true);
        createServerButton.gameObject.SetActive(false);
        menuCreateServerButton.gameObject.SetActive(true);

        browserRoomsButton.gameObject.SetActive(true);

        backButton.gameObject.SetActive(true);

        serverListParent.gameObject.SetActive(false);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        HideDebug();

        StopAllCoroutines();
        StartCoroutine(ServerListUpdater());
    }

    private void OpenSettingsMenu()
    {
        if (!inMatch)
            return;

        if (SettingsGroup != null)
            SettingsGroup.SetActive(true);

        leaveButton.gameObject.SetActive(true);
        ResumeButton.gameObject.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void CloseSettingsMenu()
    {
        if (SettingsGroup != null)
            SettingsGroup.SetActive(false);

        leaveButton.gameObject.SetActive(false);
        ResumeButton.gameObject.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void StopSearching()
    {
        searching = false;
        forceCancelled = true;

        sessionId++; // breekt QuickPlay volledig af

        HideDebug();

        StopSearchingButton.gameObject.SetActive(false);

        quickJoinButton.gameObject.SetActive(true);
        browserRoomsButton.gameObject.SetActive(true);
        menuCreateServerButton.gameObject.SetActive(true);

        backButton.gameObject.SetActive(true);
    }

    private void StartSingleplayer()
    {
        isSingleplayer = true;

        // UI weg
        groupSinglePlayer.SetActive(false);
        startButton.gameObject.SetActive(false);
        multiplayerButton.gameObject.SetActive(false);
        singleplayerButton.gameObject.SetActive(false);
        backButton.gameObject.SetActive(false);

        // spawn player lokaal
        singleplayerPlayer = Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);

        inMatch = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        SetStatus("Singleplayer mode");
    }

    private void ForceFullReset()
    {
        Debug.Log("FORCE FULL RESET");

        isResettingOrLeaving = true;

        forceCancelled = true;
        searching = false;
        inMatch = false;
        isHost = false;

        currentLobby = null;
        currentLobbyId = "";
        hostAllocation = null;

        sessionId++;

        CleanupAllPlayers();

        if (cameraMovement != null)
            cameraMovement.ResetCameraToMenu();

        ClearButtons();

        ShowMainMenu();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        StopAllCoroutines();
        StartCoroutine(ServerListUpdater());

        StartCoroutine(ResetUnlock());
    }
    private void CleanupPlayer(ulong clientId)
    {
        var players = FindObjectsOfType<PlayerCubeController>();

        foreach (var p in players)
        {
            if (p == null)
                continue;

            var netObj = p.GetComponent<NetworkObject>();

            if (netObj != null && netObj.OwnerClientId == clientId)
            {
                if (netObj.IsSpawned)
                {
                    netObj.Despawn(true);
                }
                else
                {
                    Destroy(p.gameObject);
                }

                return;
            }
        }
    }

    private void BeginReset()
    {
        ForceFullReset();
    }

    private IEnumerator ResetUnlock()
    {
        yield return new WaitForSeconds(1f);
        isResettingOrLeaving = false;
    }

    private void CleanupAllPlayers()
    {
        var players = FindObjectsOfType<PlayerCubeController>();

        foreach (var p in players)
        {
            if (p == null)
                continue;

            NetworkObject netObj = p.GetComponent<NetworkObject>();

            if (netObj != null && netObj.IsSpawned)
            {
                netObj.Despawn(true);
            }
            else
            {
                Destroy(p.gameObject);
            }
        }
    }

    private void SetInMatchUI(bool state)
    {
        inMatch = state;
        searching = false;
        forceCancelled = true;

        // MENU UI
        startButton?.gameObject.SetActive(!state);
        multiplayerButton?.gameObject.SetActive(!state);
        singleplayerButton?.gameObject.SetActive(!state);
        backButton?.gameObject.SetActive(!state);

        // MULTIPLAYER MENU
        quickPlayButton?.gameObject.SetActive(!state);
        browserRoomsButton?.gameObject.SetActive(!state);
        menuCreateServerButton?.gameObject.SetActive(!state);

        createServerButton?.gameObject.SetActive(false);
        StopSearchingButton?.gameObject.SetActive(false);

        // SERVER LIST
        serverListParent?.gameObject.SetActive(!state);
        ClearButtons();

        // SETTINGS
        SettingsGroup?.SetActive(false);
        leaveButton?.gameObject.SetActive(false);
        ResumeButton?.gameObject.SetActive(false);

        // TEXT
        if (statusText != null)
            statusText.gameObject.SetActive(state);

        if (debugText != null)
            debugText.gameObject.SetActive(false);

        // CURSOR
        Cursor.lockState = state ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !state;
    }

    private void ShowMainMenu()
    {
        startButton.gameObject.SetActive(true);

        multiplayerButton.gameObject.SetActive(false);
        singleplayerButton.gameObject.SetActive(false);
        backButton.gameObject.SetActive(false);

        quickPlayButton.gameObject.SetActive(false);
        browserRoomsButton.gameObject.SetActive(false);
        menuCreateServerButton.gameObject.SetActive(false);
        createServerButton.gameObject.SetActive(false);

        StopSearchingButton.gameObject.SetActive(false);

        serverListParent.gameObject.SetActive(false);

        SettingsGroup?.SetActive(false);
        leaveButton?.gameObject.SetActive(false);
        ResumeButton?.gameObject.SetActive(false);

        if (statusText != null)
            statusText.gameObject.SetActive(false);

        if (debugText != null)
            debugText.gameObject.SetActive(false);
    }

    private async Task FullNetworkResetAsync()
    {
        if (networkManager == null)
            return;

        // ❌ altijd eerst callbacks UNREGISTEREN
        networkManager.OnClientConnectedCallback -= OnClientConnected;
        networkManager.OnClientDisconnectCallback -= HandleClientDisconnect;

        // shutdown netcode
        if (networkManager.IsListening)
        {
            networkManager.Shutdown();
        }

        // ⛔ wachten tot echt gestopt
        while (networkManager != null && networkManager.IsListening)
        {
            await Task.Delay(50);
        }

        await Task.Delay(100);

        // transport reset
        var transport = networkManager.GetComponent<UnityTransport>();
        if (transport != null)
        {
            transport.Shutdown();
        }

        hostAllocation = null;

        // 🔁 BELANGRIJK: callbacks opnieuw registreren voor volgende session
        RegisterCallbacks();
    }

    private void RegisterCallbacks()
    {
        if (networkManager == null)
            return;

        // eerst verwijderen tegen duplicates
        networkManager.OnClientConnectedCallback -= OnClientConnected;
        networkManager.OnClientDisconnectCallback -= HandleClientDisconnect;

        // opnieuw registreren
        networkManager.OnClientConnectedCallback += OnClientConnected;
        networkManager.OnClientDisconnectCallback += HandleClientDisconnect;
    }

    private async Task FullResetFromAnyError()
    {
        if (isResettingOrLeaving)
            return;

        isResettingOrLeaving = true;

        forceCancelled = true;
        searching = false;
        isResetting = true;

        sessionId++;

        try
        {
            // 1. NETWORK RESET
            await FullNetworkResetAsync();

            float t = 0;
            while (networkManager != null && networkManager.IsListening && t < 5f)
            {
                await Task.Delay(100);
                t += 0.1f;
            }

            Debug.Log("Netcode fully reset");
        }
        catch { }

        try
        {
            // 2. LOBBY CLEANUP SAFE
            if (!string.IsNullOrEmpty(currentLobbyId))
            {
                if (isHost)
                    await LobbyService.Instance.DeleteLobbyAsync(currentLobbyId);
                else
                    await LobbyService.Instance.RemovePlayerAsync(
                        currentLobbyId,
                        AuthenticationService.Instance.PlayerId
                    );
            }
        }
        catch { }

        // 3. CLEAR EVERYTHING
        currentLobbyId = "";
        currentLobby = null;
        hostAllocation = null;
        isHost = false;
        inMatch = false;

        // 4. UI RESET HARD
        ForceFullReset();

        await Task.Delay(500);

        isResettingOrLeaving = false;
        isResetting = false;
    }

    private async Task SafeJoin(string joinCode)
    {
        try
        {
            await JoinServer(joinCode);
        }
        catch (System.Exception e)
        {
            Debug.LogError("SafeJoin failed: " + e);
        }
    }

    private bool TryStartClient(JoinAllocation allocation)
    {
        try
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

            // ❌ BELANGRIJK: check alleen IsListening
            if (networkManager.IsListening)
                networkManager.Shutdown();

            bool started = networkManager.StartClient();

            Debug.Log("StartClient result: " + started);

            return started;
        }
        catch (System.Exception e)
        {
            Debug.LogError("StartClient FAILED: " + e);
            return false;
        }
    }
}