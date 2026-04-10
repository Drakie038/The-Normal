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

    [Header("Cubus")]
    [SerializeField] private GameObject baseCubePrefab; // prefab van de cubus (met PlayerCubeController + NetworkObject)
    [SerializeField] private Transform cubeParent;      // parent voor alle cubussen
    [SerializeField] private float xOffset = 2f;        // afstand tussen cubussen

    // Houd alle NetworkObjects van cubussen bij
    private List<NetworkObject> spawnedCubes = new List<NetworkObject>();

    private void Awake()
    {
        // Buttons via inspector slepen
        createGameButton.onClick.AddListener(() => _ = CreateGame());
        startGameButton.onClick.AddListener(() => _ = StartGame());
    }

    private async Task InitializeUnityServices()
    {
        if (!UnityServices.State.Equals(ServicesInitializationState.Initialized))
        {
            await UnityServices.InitializeAsync();
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            Debug.Log("Unity Services ready & signed in anonymously.");
        }
    }

    // ==============================
    // Host maken
    // ==============================
    private async Task CreateGame()
    {
        await InitializeUnityServices();

        if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            NetworkManager.Singleton.StartHost();
            Debug.Log("Host gestart! Spel is nu online.");

            // Spawn originele cubus voor host
            SpawnCubeForHost(Vector3.zero);

            // Callback voor client connect
            NetworkManager.Singleton.OnClientConnectedCallback += ClientConnectedToHost;
        }
        else
        {
            Debug.LogWarning("Netcode al actief, kan geen host starten.");
        }
    }

    // ==============================
    // Client joinen
    // ==============================
    private async Task StartGame()
    {
        await InitializeUnityServices();

        if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.StartClient();
            Debug.Log("StartClient() aangeroepen, wacht op echte verbinding...");
        }
        else
        {
            Debug.LogWarning("Netcode al actief, kan geen client starten.");
        }
    }

    // ==============================
    // Callback client verbonden
    // ==============================
    private void OnClientConnected(ulong clientId)
    {
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            Debug.Log("Client echt verbonden met host!");
            // Vraag host om een nieuwe cubus te spawnen voor deze client
            RequestSpawnCubeServerRpc();
        }
    }

    // ==============================
    // Callback host bij client connect
    // ==============================
    private void ClientConnectedToHost(ulong clientId)
    {
        Debug.Log($"Host: client {clientId} verbonden.");
    }

    // ==============================
    // ServerRPC: spawn een nieuwe cubus voor de client
    // ==============================
    [ServerRpc(RequireOwnership = false)]
    private void RequestSpawnCubeServerRpc(ServerRpcParams rpcParams = default)
    {
        Vector3 spawnPos = Vector3.right * xOffset * spawnedCubes.Count;

        NetworkObject newCube = Instantiate(baseCubePrefab, spawnPos, Quaternion.identity, cubeParent)
            .GetComponent<NetworkObject>();

        // Geef ownership aan de client die deze cubus moet kunnen bewegen
        newCube.SpawnWithOwnership(rpcParams.Receive.SenderClientId);

        spawnedCubes.Add(newCube);

        Debug.Log($"Nieuwe cubus gespawned op {spawnPos} voor client {rpcParams.Receive.SenderClientId}");
    }

    // ==============================
    // Spawn originele cubus voor host
    // ==============================
    private void SpawnCubeForHost(Vector3 position)
    {
        NetworkObject hostCube = Instantiate(baseCubePrefab, position, Quaternion.identity, cubeParent)
            .GetComponent<NetworkObject>();

        hostCube.Spawn(true); // Host wordt eigenaar
        spawnedCubes.Add(hostCube);
        Debug.Log("Originele cubus gespawned voor host en clients.");
    }
}