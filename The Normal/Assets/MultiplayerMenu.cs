using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using Unity.Services.Core;
using Unity.Services.Authentication;
using System.Threading.Tasks;
using System.Collections.Generic;

public class MultiplayerMenu : NetworkBehaviour
{
    [Header("UI Buttons")]
    [SerializeField] private Button createGameButton;
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button leaveButton;

    [Header("Cubus")]
    [SerializeField] private GameObject baseCubePrefab;
    [SerializeField] private Transform cubeParent;
    [SerializeField] private float xOffset = 2f;

    // 🔥 FIX: 1 cube per client
    private Dictionary<ulong, NetworkObject> playerCubes = new Dictionary<ulong, NetworkObject>();

    private void Awake()
    {
        createGameButton.onClick.AddListener(() => _ = CreateGame());
        startGameButton.onClick.AddListener(() => _ = StartGame());
        leaveButton.onClick.AddListener(LeaveGame);

        leaveButton.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
    }

    private void OnDisable()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    private async Task InitializeUnityServices()
    {
        if (!UnityServices.State.Equals(ServicesInitializationState.Initialized))
        {
            await UnityServices.InitializeAsync();
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            Debug.Log("Unity Services ready.");
        }
    }

    // ==============================
    // HOST
    // ==============================
    private async Task CreateGame()
    {
        await InitializeUnityServices();

        if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            NetworkManager.Singleton.StartHost();

            leaveButton.gameObject.SetActive(true);

            // 🔥 voorkom dubbele subscriptions
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;

            Debug.Log("Host gestart!");

            // Spawn host cube
            SpawnCube(NetworkManager.Singleton.LocalClientId);
        }
    }

    // ==============================
    // CLIENT
    // ==============================
    private async Task StartGame()
    {
        await InitializeUnityServices();

        if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;

            NetworkManager.Singleton.StartClient();

            Debug.Log("Client gestart...");
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        // Alleen voor jezelf
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            leaveButton.gameObject.SetActive(true);

            if (!NetworkManager.Singleton.IsServer)
            {
                // client vraagt spawn
                RequestSpawnCubeServerRpc();
            }
        }

        // Host spawnt voor nieuwe clients
        if (NetworkManager.Singleton.IsServer && clientId != NetworkManager.Singleton.LocalClientId)
        {
            SpawnCube(clientId);
        }
    }

    // ==============================
    // SPAWN
    // ==============================
    private void SpawnCube(ulong clientId)
    {
        // 🔥 BELANGRIJK: check of al bestaat
        if (playerCubes.ContainsKey(clientId))
        {
            Debug.LogWarning($"Client {clientId} heeft al een cube!");
            return;
        }

        Vector3 spawnPos = Vector3.right * xOffset * playerCubes.Count;

        NetworkObject cube = Instantiate(baseCubePrefab, spawnPos, Quaternion.identity, cubeParent)
            .GetComponent<NetworkObject>();

        cube.SpawnWithOwnership(clientId);

        playerCubes.Add(clientId, cube);

        Debug.Log($"Cube gespawned voor client {clientId}");
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestSpawnCubeServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        SpawnCube(clientId);
    }

    // ==============================
    // LEAVE
    // ==============================
    private void LeaveGame()
    {
        if (NetworkManager.Singleton.IsHost)
        {
            Debug.Log("Host stopt...");
            DespawnAllCubes();
        }

        // ❗ GEEN RPC MEER
        NetworkManager.Singleton.Shutdown();

        leaveButton.gameObject.SetActive(false);
    }

    // ==============================
    // DISCONNECT CLEANUP (KEY FIX)
    // ==============================
    private void OnClientDisconnected(ulong clientId)
    {
        if (!NetworkManager.Singleton.IsServer) return;

        if (playerCubes.TryGetValue(clientId, out NetworkObject cube))
        {
            if (cube != null && cube.IsSpawned)
            {
                cube.Despawn(true);
            }

            playerCubes.Remove(clientId);

            Debug.Log($"Cube van client {clientId} verwijderd.");
        }
    }

    // ==============================
    // HOST CLEANUP
    // ==============================
    private void DespawnAllCubes()
    {
        foreach (var cube in playerCubes.Values)
        {
            if (cube != null && cube.IsSpawned)
            {
                cube.Despawn(true);
            }
        }

        playerCubes.Clear();
    }
}