using UnityEngine;
using Unity.Netcode;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System.Linq;

public class ElevatorPlayers : NetworkBehaviour
{
    [SerializeField] private string gameplaySceneName = "GamePlay";

    private HashSet<ulong> playersGoingToGame = new HashSet<ulong>();

    private List<ulong> elevatorPassengers = new List<ulong>();

    private bool elevatorBusy;

    public static ElevatorPlayers Instance;

    [Header("UI")]
    [SerializeField] private TMP_Text playerCountText;

    [Header("Elevator Center")]
    [SerializeField] private Transform centerPoint;

    [Header("Exit Point")]
    [SerializeField] private Transform exitPoint;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 1.2f;

    [Header("Rotation")]
    [SerializeField] private float rotationSpeed = 60f;

    [Header("Limits")]
    [SerializeField] private int maxPlayers = 4;

    [Header("Lock Collider")]
    [SerializeField] private BoxCollider lockCollider;

    [Header("Timer")]
    [SerializeField] private float fullTimerDuration = 5f;

    [Header("Elevator Platform")]
    [SerializeField] private Transform elevatorPlatform;

    [Header("Elevator Movement")]
    [SerializeField] private float elevatorMoveSpeed = 2f;

    [Header("Start & End Positions")]
    [SerializeField] private Vector3 startPosition;
    [SerializeField] private Vector3 endPosition;

    [Header("Spawn Points")]
    [SerializeField] private Transform[] spawnPoints;

    private Dictionary<ulong, int> playerSpawnIndex = new Dictionary<ulong, int>();
    private bool[] spawnOccupied;

    private bool timerRunning;

    private HashSet<ulong> playersInside = new HashSet<ulong>();

    private NetworkVariable<int> syncedPlayerCount =
        new NetworkVariable<int>(0);

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            Instance = this;

            playersInside.Clear();
            syncedPlayerCount.Value = 0;

            elevatorPlatform.position = startPosition;

            spawnOccupied = new bool[spawnPoints.Length];
        }

        syncedPlayerCount.OnValueChanged += OnPlayerCountChanged;

        UpdateUI(syncedPlayerCount.Value);
        UpdateLockCollider();

        if (IsServer)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
    }

    public override void OnNetworkDespawn()
    {
        syncedPlayerCount.OnValueChanged -= OnPlayerCountChanged;

        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    private void OnPlayerCountChanged(int oldValue, int newValue)
    {
        UpdateUI(newValue);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer) return;

        if (playersInside.Remove(clientId))
        {
            syncedPlayerCount.Value = playersInside.Count;
            UpdateUI(syncedPlayerCount.Value);
            UpdateLockCollider();
            CheckFullState();
        }
    }

    private void UpdateUI(int count)
    {
        if (playerCountText != null)
            playerCountText.text = $"{count}/{maxPlayers}";
    }

    private void UpdateLockCollider()
    {
        if (lockCollider == null) return;

        bool isAtStart = Vector3.Distance(elevatorPlatform.position, startPosition) < 0.01f;
        bool isFull = playersInside.Count >= maxPlayers;

        // LOCK = aan als NIET op start OR vol
        bool shouldLock = !isAtStart || isFull;

        lockCollider.enabled = shouldLock;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        PlayerCubeController player = other.GetComponent<PlayerCubeController>();
        if (player == null) return;

        ulong id = player.OwnerClientId;

        if (playersInside.Count >= maxPlayers) return;
        if (!playersInside.Add(id)) return;

        syncedPlayerCount.Value = playersInside.Count;

        UpdateUI(syncedPlayerCount.Value);
        UpdateLockCollider();
        CheckFullState();

        StartCoroutine(SmoothEnterElevator(player));
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsServer) return;

        PlayerCubeController player = other.GetComponent<PlayerCubeController>();
        if (player == null) return;

        ulong id = player.OwnerClientId;

        if (playersInside.Remove(id))
        {
            syncedPlayerCount.Value = playersInside.Count;
            UpdateUI(syncedPlayerCount.Value);
            UpdateLockCollider();
            CheckFullState();
        }
    }

    private void CheckFullState()
    {
        if (playersInside.Count >= maxPlayers)
        {
            if (!timerRunning)
            {
                timerRunning = true;
                StartFullTimerClientRpc(fullTimerDuration);
            }
        }
        else
        {
            timerRunning = false;

            // 🔥 FIX: alleen timer stoppen, NIET UI forceren resetten
            StopFullTimerClientRpc();
        }
    }

    [ClientRpc]
    private void StartFullTimerClientRpc(float duration)
    {
        if (ElevatorMenu.Instance != null)
            ElevatorMenu.Instance.StartTimer(duration);
    }

    [ClientRpc]
    private void StopFullTimerClientRpc()
    {
        // 🔥 FIX: GEEN HideLeaveButton meer hier
        if (ElevatorMenu.Instance != null)
            ElevatorMenu.Instance.StopTimer();
    }

    [ServerRpc(RequireOwnership = false)]
    public void TriggerElevatorStartServerRpc()
    {
        if (elevatorBusy)
            return;

        elevatorPassengers.Clear();

        foreach (ulong id in playersInside)
        {
            elevatorPassengers.Add(id);
        }

        StartCoroutine(MoveElevatorDown());
    }

    private IEnumerator MoveElevatorDown()
    {
        elevatorBusy = true;

        List<ulong> passengers = new List<ulong>(playersInside);

        Vector3 target = endPosition;

        // --- SMOOTH DOWN ---
        while (Vector3.Distance(elevatorPlatform.position, target) > 0.01f)
        {
            elevatorPlatform.position = Vector3.MoveTowards(
                elevatorPlatform.position,
                target,
                elevatorMoveSpeed * Time.deltaTime
            );

            UpdateLockCollider(); // 🔥 live update

            yield return null;
        }

        elevatorPlatform.position = target;

        StopFullTimerClientRpc();
        HideLeaveButtonClientRpc();

        yield return new WaitForSeconds(0.25f);

        // --- LOAD GAMEPLAY ONLY FOR PASSENGERS ---
        StartGameplayForElevatorPlayersClientRpc(passengers.ToArray());

        yield return new WaitForSeconds(0.5f);

        // --- DESPAWN PLAYERS ---
        foreach (ulong clientId in passengers)
        {
            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
            {
                if (client.PlayerObject != null)
                {
                    client.PlayerObject.Despawn(true);
                }
            }
        }

        yield return new WaitForSeconds(0.1f);

        // --- GO BACK UP SMOOTH ---
        StartCoroutine(ReturnElevatorToStart());

        elevatorBusy = false;
    }

    [ClientRpc]
    private void HideLeaveButtonClientRpc()
    {
        if (ElevatorMenu.Instance != null)
            ElevatorMenu.Instance.ShowLeaveButton(false);
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestLeaveElevatorServerRpc(ulong clientId)
    {
        if (playersInside.Remove(clientId))
        {
            syncedPlayerCount.Value = playersInside.Count;
            UpdateUI(syncedPlayerCount.Value);
            UpdateLockCollider();
            CheckFullState();
        }

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.ClientId != clientId) continue;

            PlayerCubeController player =
                client.PlayerObject.GetComponent<PlayerCubeController>();

            if (player != null)
                StartCoroutine(SmoothExitElevator(player));

            break;
        }
    }

    private IEnumerator SmoothEnterElevator(PlayerCubeController player)
    {
        player.SetFrozen(true);
        player.SetInElevator(true);
        player.SetCameraLockedClientRpc(true);

        Transform t = player.transform;

        ulong id = player.OwnerClientId;

        int index = 0;

        for (int i = 0; i < spawnPoints.Length; i++)
        {
            if (!spawnOccupied[i])
            {
                index = i;
                spawnOccupied[i] = true;
                break;
            }
        }

        playerSpawnIndex[id] = index;

        Vector3 targetPos =
            new Vector3(
                spawnPoints[index].position.x,
                t.position.y,
                spawnPoints[index].position.z
            );

        Quaternion targetRot =
            Quaternion.Euler(0f, 180f, 0f);

        while (true)
        {
            t.position = Vector3.MoveTowards(t.position, targetPos, moveSpeed * Time.deltaTime);
            t.rotation = Quaternion.RotateTowards(t.rotation, targetRot, rotationSpeed * Time.deltaTime);

            if (Vector3.Distance(t.position, targetPos) < 0.03f &&
                Quaternion.Angle(t.rotation, targetRot) < 1f)
                break;

            yield return null;
        }

        t.position = targetPos;
        t.rotation = targetRot;

        t.SetParent(elevatorPlatform);

        player.SetCurrentElevator(this);

        ShowElevatorUIClientRpc(player.OwnerClientId);
    }

    private IEnumerator SmoothExitElevator(PlayerCubeController player)
    {
        Transform t = player.transform;

        ulong id = player.OwnerClientId;

        if (playerSpawnIndex.TryGetValue(id, out int index))
        {
            if (index >= 0 && index < spawnOccupied.Length)
                spawnOccupied[index] = false;

            playerSpawnIndex.Remove(id);
        }

        Vector3 targetPos =
            new Vector3(exitPoint.position.x, t.position.y, exitPoint.position.z);

        Quaternion targetRot = Quaternion.Euler(0f, 180f, 0f);

        while (true)
        {
            t.position = Vector3.MoveTowards(t.position, targetPos, moveSpeed * Time.deltaTime);
            t.rotation = Quaternion.RotateTowards(t.rotation, targetRot, rotationSpeed * Time.deltaTime);

            if (Vector3.Distance(t.position, targetPos) < 0.03f &&
                Quaternion.Angle(t.rotation, targetRot) < 1f)
                break;

            yield return null;
        }

        t.position = targetPos;
        t.rotation = targetRot;

        t.SetParent(null);

        player.SetInElevator(false);
        player.SetFrozen(false);

        player.SetCameraLockedClientRpc(false);

        HideElevatorUIClientRpc(player.OwnerClientId);
    }

    [ClientRpc]
    private void ShowElevatorUIClientRpc(ulong targetClientId)
    {
        if (NetworkManager.Singleton.LocalClientId != targetClientId)
            return;

        if (ElevatorMenu.Instance != null)
        {
            ElevatorMenu.Instance.ShowLeaveButton(true);
        }
    }

    [ClientRpc]
    private void HideElevatorUIClientRpc(ulong targetClientId)
    {
        if (NetworkManager.Singleton.LocalClientId != targetClientId)
            return;

        if (ElevatorMenu.Instance != null)
        {
            ElevatorMenu.Instance.ShowLeaveButton(false);
        }
    }

    private void ResetElevatorAfterTrip()
    {
        if (!IsServer)
            return;

        timerRunning = false;

        playersInside.Clear();
        elevatorPassengers.Clear();

        syncedPlayerCount.Value = 0;

        playerSpawnIndex.Clear();

        if (spawnOccupied != null)
        {
            for (int i = 0; i < spawnOccupied.Length; i++)
            {
                spawnOccupied[i] = false;
            }
        }

        UpdateUI(0);

        // 🔥 BELANGRIJK: opnieuw correct state checken
        UpdateLockCollider();
    }

    [ClientRpc]
    private void LoadGameplaySceneClientRpc(ulong targetClientId)
    {
        if (NetworkManager.Singleton.LocalClientId != targetClientId)
            return;

        SceneManager.LoadScene("GamePlay");
    }

    [ClientRpc]
    private void StartGameplayForElevatorPlayersClientRpc(ulong[] passengerIds)
    {
        ulong localId = NetworkManager.Singleton.LocalClientId;

        bool shouldLoad = false;

        foreach (ulong id in passengerIds)
        {
            if (id == localId)
            {
                shouldLoad = true;
                break;
            }
        }

        if (!shouldLoad)
            return;

        SceneManager.LoadScene(gameplaySceneName);
    }

    public void ResetElevatorState()
    {
        if (!IsServer)
            return;

        elevatorPlatform.position = startPosition;

        timerRunning = false;
        elevatorBusy = false;

        playersInside.Clear();
        elevatorPassengers.Clear();

        syncedPlayerCount.Value = 0;

        playerSpawnIndex.Clear();

        if (spawnOccupied != null)
        {
            for (int i = 0; i < spawnOccupied.Length; i++)
            {
                spawnOccupied[i] = false;
            }
        }

        UpdateUI(0);
        UpdateLockCollider();

        if (ElevatorMenu.Instance != null)
            ElevatorMenu.Instance.ForceResetUI();
    }

    private IEnumerator ReturnElevatorToStart()
    {
        Vector3 target = startPosition;

        while (Vector3.Distance(elevatorPlatform.position, target) > 0.01f)
        {
            elevatorPlatform.position = Vector3.MoveTowards(
                elevatorPlatform.position,
                target,
                elevatorMoveSpeed * Time.deltaTime
            );

            UpdateLockCollider(); // 🔥 live update

            yield return null;
        }

        elevatorPlatform.position = target;

        ResetElevatorAfterTrip();
    }
}