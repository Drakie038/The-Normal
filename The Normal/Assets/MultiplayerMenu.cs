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

using UnityEngine.SceneManagement;

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

    private int joinSessionId = 0;
    private bool isJoining = false;

    private Coroutine serverLoop;

    [Header("Server List Layout (manual)")]
    [SerializeField] private float buttonSpacing = 80f;
    [SerializeField] private float startY = 0f;
    [SerializeField] private float buttonHeight = 60f;

    private float currentY;

    private float playerCountTimer;

    private bool isLeavingOrResetting => isResettingOrLeaving || isResetting;

    private bool isQuickJoining = false;

    [Header("PLAYER NAME")]
    [SerializeField] private TMP_InputField playerNameInput;
    [SerializeField] private TMP_Text playerInfoText;
    private string playerName = "";

    [Header("Settings Player List")]
    [SerializeField] private TMP_Text playersListText;

    private const int MAX_PLAYERS = 15;

    private async void Start()
    {
        RegisterCallbacks();

        if (UnityServices.State != Unity.Services.Core.ServicesInitializationState.Initialized)
        {
            await UnityServices.InitializeAsync();
        }

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        cameraMovement = FindObjectOfType<CameraMovement>();

        quickPlayButton.onClick.AddListener(() => _ = QuickPlay());
        createServerButton.onClick.AddListener(() => _ = CreateServer());

        leaveButton.onClick.AddListener(LeaveGame);
        ResumeButton.onClick.AddListener(CloseSettingsMenu);

        StopSearchingButton.onClick.AddListener(StopSearching);

        StartServerLoop();
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

        if (playerNameInput != null)
            playerNameInput.gameObject.SetActive(true);

        playerInfoText.gameObject.SetActive(true);
        playerInfoText.text = "Enter a name (max 10 chars)";

        if (playerInfoText != null)
            playerInfoText.gameObject.SetActive(false);

        serverListParent.gameObject.SetActive(false);

        startButton.onClick.AddListener(() =>
        {
            if (!ValidatePlayerName())
                return;

            startButton.gameObject.SetActive(false);

            multiplayerButton.gameObject.SetActive(true);
            singleplayerButton.gameObject.SetActive(true);

            // ❗ PAS NU verbergen (na geldige naam)
            playerNameInput.gameObject.SetActive(false);
            playerInfoText.gameObject.SetActive(false);

            backButton.gameObject.SetActive(true);

            backStack.Push(() =>
            {
                startButton.gameObject.SetActive(true);

                multiplayerButton.gameObject.SetActive(false);
                singleplayerButton.gameObject.SetActive(false);

                // ❗ terug tonen bij back
                playerNameInput.gameObject.SetActive(true);

                playerInfoText.gameObject.SetActive(true);
                playerInfoText.text = "Enter a name (max 10 chars)";

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

            if (playerNameInput != null)
                playerNameInput.gameObject.SetActive(false);

            if (playerInfoText != null)
                playerInfoText.gameObject.SetActive(false);

            backStack.Push(() =>
            {
                multiplayerButton.gameObject.SetActive(true);
                singleplayerButton.gameObject.SetActive(true);

                if (playerNameInput != null)
                    playerNameInput.gameObject.SetActive(true);

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

                ClearButtons();
                _ = RefreshServerListSafe();

                quickJoinButton.gameObject.SetActive(false);
                browserRoomsButton.gameObject.SetActive(false);
                menuCreateServerButton.gameObject.SetActive(false);

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

                if (playerNameInput != null)
                    playerNameInput.gameObject.SetActive(false);

                if (playerInfoText != null)
                    playerInfoText.gameObject.SetActive(false);

                backStack.Push(() =>
                {
                    groupSinglePlayer.SetActive(false);

                    multiplayerButton.gameObject.SetActive(true);
                    singleplayerButton.gameObject.SetActive(true);

                    if (playerNameInput != null)
                        playerNameInput.gameObject.SetActive(true);
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
        if (SettingsGroup != null && SettingsGroup.activeSelf)
        {
            RefreshPlayerList();
        }

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
            if (SettingsGroup == null)
                return;

            var player = FindFirstObjectByType<PlayerCubeController>();
            bool inElevator = player != null && player.inElevator.Value;

            bool isOpen = SettingsGroup.activeSelf;

            if (isOpen)
            {
                CloseSettingsMenu();

                // camera unlock
                if (cameraMovement != null)
                    cameraMovement.settingsLocked = false;

                // cursor restore
                if (!inElevator)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
                else
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
            }
            else
            {
                if (cameraMovement != null)
                {
                    cameraMovement.settingsLocked = true;
                    cameraMovement.SmoothLookForwardForSettings();
                }

                OpenSettingsMenu();
            }
        }

        // HOST HEARTBEAT
        if (isHost && currentLobby != null)
        {
            playerCountTimer += Time.deltaTime;

            if (playerCountTimer >= 2f)
            {
                playerCountTimer = 0f;
                _ = UpdatePlayerCount();
            }

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
        if (isQuickJoining || isJoining || inMatch)
            return;

        isQuickJoining = true;

        forceCancelled = false;
        searching = true;

        // UI
        quickJoinButton.gameObject.SetActive(false);
        browserRoomsButton.gameObject.SetActive(false);
        menuCreateServerButton.gameObject.SetActive(false);
        backButton.gameObject.SetActive(false);

        StopSearchingButton.gameObject.SetActive(true);

        ShowDebug("Searching...");

        float startTime = Time.time;

        while (searching)
        {
            // CANCEL
            if (forceCancelled)
            {
                ResetQuickJoinUI();
                return;
            }

            // TIMEOUT
            float elapsed = Time.time - startTime;

            if (elapsed >= 10f)
            {
                ShowDebug("No servers found");

                await Task.Delay(1500);

                ResetQuickJoinUI();
                return;
            }

            try
            {
                QueryResponse response =
                    await LobbyService.Instance.QueryLobbiesAsync(
                        new QueryLobbiesOptions { Count = 50 }
                    );

                if (forceCancelled)
                {
                    ResetQuickJoinUI();
                    return;
                }

                List<Lobby> validLobbies = new List<Lobby>();

                foreach (var lobby in response.Results)
                {
                    if (lobby.Data != null &&
                        lobby.Data.ContainsKey("joinCode"))
                    {
                        validLobbies.Add(lobby);
                    }
                }

                // SERVER FOUND
                if (validLobbies.Count > 0)
                {
                    Lobby chosenLobby =
                        validLobbies[Random.Range(0, validLobbies.Count)];

                    string joinCode =
                        chosenLobby.Data["joinCode"].Value;

                    searching = false;

                    StopSearchingButton.gameObject.SetActive(false);

                    await JoinServerInternal(joinCode, ++joinSessionId);

                    isQuickJoining = false;
                    return;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("QuickPlay error: " + e.Message);
            }

            await Task.Delay(1000);
        }
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

        hostAllocation = await RelayService.Instance.CreateAllocationAsync(15);
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

        currentLobby = await LobbyService.Instance.CreateLobbyAsync(serverName, 15, options);

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
        await LeaveEverything();
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

        if (networkManager.IsListening)
            networkManager.Shutdown();

        bool started = networkManager.StartHost();

        if (!started)
        {
            Debug.LogError("StartHost failed!");
            return;
        }

        Debug.Log("HOST STARTED");
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

    private void OnClientDisconnect(ulong clientId)
    {
        if (networkManager == null)
            return;

        // alleen server logic
        if (networkManager.IsServer)
        {
            _ = OnServerClientDisconnect(clientId);
        }
    }

    private async Task OnServerClientDisconnect(ulong clientId)
    {
        await Task.Delay(100);
        RequestPlayerCountUpdate();
    }

    private async void HandleClientDisconnect(ulong clientId)
    {
        if (clientId != networkManager.LocalClientId)
            return;

        if (isResettingOrLeaving || isResetting)
            return;

        Debug.LogWarning("CLIENT DISCONNECTED → CLEAN EXIT");

        isResettingOrLeaving = true;

        await LeaveEverything();
        isLeavingEverything = false;

        ShowStartMenuOnly();

        isResettingOrLeaving = false;
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

    private async Task RefreshServerListSafe()
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

            ClearButtons();

            foreach (var lobby in response.Results)
            {
                if (lobby.Data == null || !lobby.Data.ContainsKey("joinCode"))
                    continue;

                string serverName = lobby.Data.ContainsKey("serverName")
                    ? lobby.Data["serverName"].Value
                    : "Server";

                string joinCode = lobby.Data["joinCode"].Value;

                int playerCount = 1;
                if (lobby.Data.ContainsKey("playerCount"))
                    int.TryParse(lobby.Data["playerCount"].Value, out playerCount);

                CreateServerButton(serverName, joinCode, playerCount);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Server list error: " + e.Message);
        }
        finally
        {
            // 🔥 CRITICAL FIX
            isRefreshing = false;
        }
    }

    private GameObject CreateServerButton(string serverName, string joinCode, int playerCount)
    {
        GameObject obj = Instantiate(serverButtonPrefab, serverListParent);
        spawnedButtons.Add(obj);

        RectTransform rt = obj.GetComponent<RectTransform>();

        // FORCE LOCAL UI POSITIONING
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);

        rt.anchoredPosition = new Vector2(0f, -currentY);

        currentY += buttonHeight + buttonSpacing;

        TMP_Text text = obj.GetComponentInChildren<TMP_Text>();
        if (text != null)
        {
            string status = playerCount >= MAX_PLAYERS ? "FULL" : "OPEN";
            text.text = $"{serverName} | {playerCount}/{MAX_PLAYERS} | {status}";
        }

        Button btn = obj.GetComponent<Button>();
        btn.onClick.RemoveAllListeners();

        string codeCopy = string.Copy(joinCode);

        btn.onClick.AddListener(() =>
        {
            Debug.Log("CLICK SERVER: " + codeCopy);
            _ = JoinFromButton(codeCopy);
        });

        return obj;
    }

    private TaskCompletionSource<bool> joinTcs;

    private async Task JoinServerSafe(string joinCode, int session)
    {
        if (session != joinSessionId) return;

        try
        {
            Debug.Log("JOIN START: " + joinCode);

            JoinAllocation allocation =
                await RelayService.Instance.JoinAllocationAsync(joinCode);

            if (session != joinSessionId) return;

            bool started = StartClientSafe(allocation);

            if (!started)
                throw new System.Exception("StartClient failed");

            currentLobbyId = "";
            inMatch = true;

            SetStatus("Joined server");
            SetInMatchUI(true);
            SetServerListVisible(false);
            ClearButtons();

            Debug.Log("JOIN SUCCESS");
        }
        catch (System.Exception e)
        {
            Debug.LogError("JOIN FAILED: " + e.Message);

            await LeaveEverything();
            isLeavingEverything = false;
        }
    }
    private void ClearButtons()
    {
        for (int i = 0; i < spawnedButtons.Count; i++)
        {
            if (spawnedButtons[i] != null)
            {
                Button b = spawnedButtons[i].GetComponent<Button>();
                if (b != null)
                    b.onClick.RemoveAllListeners();

                Destroy(spawnedButtons[i]);
            }
        }

        spawnedButtons.Clear();
        lobbyButtons.Clear();

        // RESET POSITIONING
        currentY = startY;

        Canvas.ForceUpdateCanvases();
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

    public void OpenSettingsMenu()
    {
        if (!inMatch)
            return;

        if (SettingsGroup != null)
            SettingsGroup.SetActive(true);

        leaveButton.gameObject.SetActive(true);
        ResumeButton.gameObject.SetActive(true);

        RefreshPlayerList();

        if (cameraMovement != null)
            cameraMovement.settingsLocked = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void CloseSettingsMenu()
    {
        if (SettingsGroup != null)
            SettingsGroup.SetActive(false);

        leaveButton.gameObject.SetActive(false);
        ResumeButton.gameObject.SetActive(false);

        if (cameraMovement != null)
            cameraMovement.settingsLocked = false;

        var player = FindFirstObjectByType<PlayerCubeController>();
        bool inElevator = player != null && player.inElevator.Value;

        // FIX: NIET FORCEREN ALS CAMERA HET AL DOET
        if (!inElevator)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void StopSearching()
    {
        forceCancelled = true;
        searching = false;

        ResetQuickJoinUI();
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

        if (!inMatch)
        {
            ShowStartMenuOnly();
        }

        if (isQuickJoining && !state)
            return;
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

    private void RegisterCallbacks()
    {
        if (networkManager == null)
            return;

        // eerst alles clean verwijderen
        networkManager.OnClientDisconnectCallback -= HandleClientDisconnect;
        networkManager.OnClientDisconnectCallback -= OnClientDisconnect;

        // opnieuw registreren
        networkManager.OnClientDisconnectCallback += HandleClientDisconnect;
        networkManager.OnClientDisconnectCallback += OnClientDisconnect;
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

    private bool StartClientSafe(JoinAllocation allocation)
    {
        try
        {
            if (networkManager == null)
                return false;

            var transport = networkManager.GetComponent<UnityTransport>();

            transport.SetRelayServerData(
                allocation.RelayServer.IpV4,
                (ushort)allocation.RelayServer.Port,
                allocation.AllocationIdBytes,
                allocation.Key,
                allocation.ConnectionData,
                allocation.HostConnectionData
            );

            if (networkManager.IsListening)
                networkManager.Shutdown();

            bool started = networkManager.StartClient();

            Debug.Log("StartClient: " + started);

            return started;
        }
        catch (System.Exception e)
        {
            Debug.LogError("StartClientSafe FAILED: " + e);
            return false;
        }
    }

    private void StartServerLoop()
    {
        if (serverLoop != null)
            StopCoroutine(serverLoop);

        serverLoop = StartCoroutine(ServerLoop());
    }

    private IEnumerator ServerLoop()
    {
        while (true)
        {
            if (!inMatch && !isRefreshing && !isJoining)
            {
                _ = RefreshServerListSafe();
            }

            yield return new WaitForSeconds(2f);
        }
    }

    private async Task JoinFromButton(string joinCode)
    {
        if (isJoining)
            return;

        isJoining = true;

        // 🔥 nieuwe session invalidates ALL old joins
        joinSessionId++;
        int mySession = joinSessionId;

        try
        {
            if (mySession != joinSessionId)
                return;

            await JoinServerInternal(joinCode, mySession);
        }
        finally
        {
            isJoining = false;
        }
    }
    private IEnumerator JoinFromButtonSafe(string joinCode)
    {
        Task joinTask = JoinFromButton(joinCode);

        while (!joinTask.IsCompleted)
            yield return null;

        if (joinTask.Exception != null)
        {
            Debug.LogError(joinTask.Exception);
        }
    }

    private async Task JoinServerInternal(string joinCode, int session)
    {
        if (session != joinSessionId)
            return;

        Debug.Log("JOIN START: " + joinCode);

        JoinAllocation allocation =
            await RelayService.Instance.JoinAllocationAsync(joinCode);

        if (session != joinSessionId)
            return;

        if (!StartClientSafe(allocation))
            throw new System.Exception("StartClient failed");

        // 🔥 wait for NGO ready
        float t = 0;
        while (!networkManager.IsClient && t < 3f)
        {
            await Task.Delay(100);
            t += 0.1f;
        }

        if (session != joinSessionId)
            return;

        inMatch = true;

        SetStatus("Joined server");
        SetInMatchUI(true);
        SetServerListVisible(false);

        ClearButtons();

        Debug.Log("JOIN SUCCESS");
    }


    private async Task JoinServer(string joinCode)
    {
        if (isJoining)
            return;

        isJoining = true;
        joinSessionId++;
        int mySession = joinSessionId;

        try
        {
            // HARD CLEAN RESET ALS NODIG
            if (networkManager != null &&
                (networkManager.IsClient || networkManager.IsHost || networkManager.IsListening))
            {
                await LeaveEverything();
                isLeavingEverything = false;
            }

            if (mySession != joinSessionId)
                return;

            Debug.Log("Joining Relay...");

            JoinAllocation allocation =
                await RelayService.Instance.JoinAllocationAsync(joinCode);

            if (mySession != joinSessionId)
                return;

            bool started = StartClientSafe(allocation);

            if (!started)
                throw new System.Exception("StartClient failed");

            currentLobbyId = "";
            inMatch = true;

            SetStatus("Joined server");
            SetInMatchUI(true);
            SetServerListVisible(false);
            ClearButtons();

            Debug.Log("JOIN SUCCESS");
        }
        catch (System.Exception e)
        {
            await LeaveEverything();
            isLeavingEverything = false;
        }
        finally
        {
            isJoining = false;
        }
    }

    private async void RequestPlayerCountUpdate()
    {
        await UpdatePlayerCount();
    }


    private bool ValidatePlayerName()
    {
        if (playerNameInput == null || playerInfoText == null)
            return false;

        string enteredName = playerNameInput.text.Trim();

        if (string.IsNullOrEmpty(enteredName))
        {
            playerInfoText.gameObject.SetActive(true);
            playerInfoText.text = "Enter a name";
            return false;
        }

        if (enteredName.Length > 10)
        {
            playerInfoText.gameObject.SetActive(true);
            playerInfoText.text = "Max 10 letters";
            return false;
        }

        playerName = enteredName;

        playerInfoText.gameObject.SetActive(false);

        return true;
    }

    private void UpdatePlayerListUI()
    {
        if (playersListText == null)
            return;

        if (NetworkManager.Singleton == null)
        {
            playersListText.text = "No network";
            return;
        }

        string list = "Players:\n";

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            ulong id = client.ClientId;

            string name = $"Player {id}";

            // als je later player name sync toevoegt kun je hier echte naam gebruiken

            list += $"- {name}\n";
        }

        playersListText.text = list;
    }

    public string GetPlayerName()
    {
        return playerName;
    }

    private void RefreshPlayerList()
    {
        if (playersListText == null)
            return;

        PlayerCubeController[] players =
            FindObjectsOfType<PlayerCubeController>();

        string result = "";

        foreach (var p in players)
        {
            if (p == null)
                continue;

            string name = p.PlayerName.Value.ToString();

            if (string.IsNullOrEmpty(name))
                name = "Player";

            result += name + "\n";
        }

        playersListText.text = result;
    }

    private void ShowStartMenuOnly()
    {
        if (isQuickJoining)
            return;

        // MAIN UI
        startButton.gameObject.SetActive(true);
        playerNameInput.gameObject.SetActive(true);

        if (playerInfoText != null)
            playerInfoText.gameObject.SetActive(true);

        // alles OFF (belangrijk)
        multiplayerButton.gameObject.SetActive(false);
        singleplayerButton.gameObject.SetActive(false);
        backButton.gameObject.SetActive(false);

        quickPlayButton.gameObject.SetActive(false);
        createServerButton.gameObject.SetActive(false);
        menuCreateServerButton.gameObject.SetActive(false);
        browserRoomsButton.gameObject.SetActive(false);
        quickJoinButton.gameObject.SetActive(false);
        StopSearchingButton.gameObject.SetActive(false);

        groupMultiplayer.SetActive(false);
        groupSinglePlayer.SetActive(false);
        SettingsGroup?.SetActive(false);

        serverListParent.gameObject.SetActive(false);
        statusText?.gameObject.SetActive(false);
        debugText?.gameObject.SetActive(false);

        leaveButton?.gameObject.SetActive(false);
        ResumeButton?.gameObject.SetActive(false);
    }

    private bool isLeavingEverything = false;

    private async Task LeaveEverything()
    {
        if (isLeavingEverything)
            return;

        isLeavingEverything = true;

        Debug.Log("=== LEAVE EVERYTHING START ===");

        try
        {
            // -----------------------------
            // 0. STOP ALL ACTIVITY FIRST
            // -----------------------------
            forceCancelled = true;
            searching = false;
            isJoining = false;

            sessionId++;
            joinSessionId++;

            Time.timeScale = 1f;

            // -----------------------------
            // 1. HARD GAME SYSTEM RESET (BEFORE NGO SHUTDOWN)
            // -----------------------------

            if (ElevatorPlayers.Instance != null)
            {
                ElevatorPlayers.Instance.ResetElevatorState();
            }

            if (GameInstanceManager.Instance != null)
            {
                GameInstanceManager.Instance.ResetAllInstances();
            }

            var door = FindFirstObjectByType<OpenDoorEntrance>();
            if (door != null)
            {
                door.ResetDoorState();
            }

            if (ElevatorMenu.Instance != null)
            {
                ElevatorMenu.Instance.ForceResetUI();
            }

            // -----------------------------
            // 2. CLEAN LOCAL PLAYERS (SAFE BEFORE SHUTDOWN)
            // -----------------------------
            CleanupAllPlayers();

            // -----------------------------
            // 3. LOBBY CLEANUP
            // -----------------------------
            if (!string.IsNullOrEmpty(currentLobbyId))
            {
                try
                {
                    Debug.Log("Cleaning lobby...");

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

                    Debug.Log("Lobby cleanup success");
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("Lobby cleanup failed: " + e.Message);
                }
            }

            currentLobby = null;
            currentLobbyId = "";

            // -----------------------------
            // 4. NGO SHUTDOWN
            // -----------------------------
            if (networkManager != null)
            {
                networkManager.OnClientDisconnectCallback -= HandleClientDisconnect;
                networkManager.OnClientDisconnectCallback -= OnClientDisconnect;

                if (networkManager.IsListening ||
                    networkManager.IsClient ||
                    networkManager.IsHost)
                {
                    Debug.Log("Shutdown NGO...");

                    networkManager.Shutdown();

                    float timeout = 0f;
                    while (networkManager.IsListening && timeout < 5f)
                    {
                        await Task.Delay(100);
                        timeout += 0.1f;
                    }
                }
            }

            // -----------------------------
            // 5. TRANSPORT RESET
            // -----------------------------
            var transport = networkManager.GetComponent<UnityTransport>();

            if (transport != null)
            {
                Debug.Log("Shutdown Transport...");

                transport.Shutdown();
                transport.SetConnectionData("0.0.0.0", 7777);
            }

            await Task.Delay(300);

            // -----------------------------
            // 6. RESET STATE
            // -----------------------------
            isHost = false;
            inMatch = false;
            searching = false;
            isJoining = false;

            hostAllocation = null;
            currentServerName = "";

            // -----------------------------
            // 7. UI RESET
            // -----------------------------
            ClearButtons();


            CloseSettingsMenu();

            if (cameraMovement != null)
            {
                cameraMovement.ResetCameraToMenu();
            }

            ShowStartMenuOnly();

            if (statusText != null)
                statusText.gameObject.SetActive(false);

            if (debugText != null)
                debugText.gameObject.SetActive(false);

            // -----------------------------
            // 8. CURSOR RESET
            // -----------------------------
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // -----------------------------
            // 9. CALLBACKS HERREGISTREREN
            // -----------------------------
            RegisterCallbacks();

            Debug.Log("=== LEAVE EVERYTHING DONE ===");

            networkManager.Shutdown();
            Destroy(networkManager.gameObject);
        }
        finally
        {
            isLeavingEverything = false;

cameraMovement.ResetCameraToMenu();

cameraMovement.OnMenuCameraFinished = () =>
{
    SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
};
        }
    }

    private void ResetQuickJoinUI()
    {
        searching = false;
        forceCancelled = false;
        isQuickJoining = false;

        StopSearchingButton.gameObject.SetActive(false);

        quickJoinButton.gameObject.SetActive(true);
        browserRoomsButton.gameObject.SetActive(true);
        menuCreateServerButton.gameObject.SetActive(true);
        backButton.gameObject.SetActive(true);

        HideDebug();
    }

    public bool IsSettingsOpen()
    {
        return SettingsGroup != null && SettingsGroup.activeSelf;
    }
}