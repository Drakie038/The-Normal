using UnityEngine;
using Unity.Netcode;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class ElevatorPlayers : NetworkBehaviour
{
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
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        if (ElevatorMenu.Instance != null)
            ElevatorMenu.Instance.ForceResetUI();
    }

    private void OnPlayerCountChanged(int oldValue, int newValue)
    {
        UpdateUI(newValue);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer)
            return;

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
        if (lockCollider == null)
            return;

        lockCollider.enabled = playersInside.Count >= maxPlayers;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer)
            return;

        PlayerCubeController player =
            other.GetComponent<PlayerCubeController>();

        if (player == null)
            return;

        ulong id = player.OwnerClientId;

        if (playersInside.Count >= maxPlayers)
            return;

        if (!playersInside.Add(id))
            return;

        syncedPlayerCount.Value = playersInside.Count;

        UpdateUI(syncedPlayerCount.Value);
        UpdateLockCollider();

        CheckFullState();

        StartCoroutine(SmoothEnterElevator(player));
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsServer)
            return;

        PlayerCubeController player =
            other.GetComponent<PlayerCubeController>();

        if (player == null)
            return;

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
        if (ElevatorMenu.Instance != null)
        {
            ElevatorMenu.Instance.StopTimer();
            ElevatorMenu.Instance.ForceHideLeaveButton();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void TriggerElevatorStartServerRpc()
    {
        StartCoroutine(MoveElevatorDown());
    }

    private IEnumerator MoveElevatorDown()
    {
        Vector3 target = endPosition;

        while (Vector3.Distance(elevatorPlatform.position, target) > 0.01f)
        {
            elevatorPlatform.position = Vector3.MoveTowards(
                elevatorPlatform.position,
                target,
                elevatorMoveSpeed * Time.deltaTime
            );

            yield return null;
        }

        elevatorPlatform.position = target;

        StopFullTimerClientRpc();

        HideLeaveButtonClientRpc();

        if (ElevatorMenu.Instance != null)
            ElevatorMenu.Instance.ForceResetUI();
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
            if (client.ClientId != clientId)
                continue;

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

        Vector3 targetPos =
            new Vector3(
                centerPoint.position.x,
                t.position.y,
                centerPoint.position.z
            );

        Quaternion targetRot =
            Quaternion.Euler(0f, 180f, 0f);

        while (true)
        {
            t.position = Vector3.MoveTowards(
                t.position,
                targetPos,
                moveSpeed * Time.deltaTime
            );

            t.rotation = Quaternion.RotateTowards(
                t.rotation,
                targetRot,
                rotationSpeed * Time.deltaTime
            );

            if (Vector3.Distance(t.position, targetPos) < 0.03f &&
                Quaternion.Angle(t.rotation, targetRot) < 1f)
                break;

            yield return null;
        }

        t.position = targetPos;
        t.rotation = targetRot;

        // 🔥 PLAYER AAN ELEVATOR VASTMAKEN
        t.SetParent(elevatorPlatform);

        player.SetCurrentElevator(this);

        ShowElevatorUIClientRpc(player.OwnerClientId);
    }

    private IEnumerator SmoothExitElevator(PlayerCubeController player)
    {
        Transform t = player.transform;

        Vector3 targetPos =
            new Vector3(
                exitPoint.position.x,
                t.position.y,
                exitPoint.position.z
            );

        Quaternion targetRot =
            Quaternion.Euler(0f, 180f, 0f);

        while (true)
        {
            t.position = Vector3.MoveTowards(
                t.position,
                targetPos,
                moveSpeed * Time.deltaTime
            );

            t.rotation = Quaternion.RotateTowards(
                t.rotation,
                targetRot,
                rotationSpeed * Time.deltaTime
            );

            if (Vector3.Distance(t.position, targetPos) < 0.03f &&
                Quaternion.Angle(t.rotation, targetRot) < 1f)
                break;

            yield return null;
        }

        t.position = targetPos;
        t.rotation = targetRot;

        // 🔥 LOSMAKEN VAN ELEVATOR
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
            ElevatorMenu.Instance.ShowLeaveButton(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    [ClientRpc]
    private void HideElevatorUIClientRpc(ulong targetClientId)
    {
        if (NetworkManager.Singleton.LocalClientId != targetClientId)
            return;

        if (ElevatorMenu.Instance != null)
            ElevatorMenu.Instance.ShowLeaveButton(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void ResetElevatorState()
    {
        if (!IsServer)
            return;

        elevatorPlatform.position = startPosition;

        timerRunning = false;

        playersInside.Clear();

        syncedPlayerCount.Value = 0;

        UpdateUI(0);
        UpdateLockCollider();

        StopAllCoroutines();

        if (ElevatorMenu.Instance != null)
            ElevatorMenu.Instance.ForceResetUI();
    }
}