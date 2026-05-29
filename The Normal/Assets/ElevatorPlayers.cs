using UnityEngine;
using Unity.Netcode;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class ElevatorPlayers : NetworkBehaviour
{
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

    // 🔥 SERVER AUTHORITATIVE
    private HashSet<ulong> playersInside = new HashSet<ulong>();

    // 🔥 NETWORK SYNCED COUNT
    private NetworkVariable<int> syncedPlayerCount =
        new NetworkVariable<int>(0);

    public override void OnNetworkSpawn()
    {
        // 🔥 HARD RESET BIJ NIEUWE SESSION/SPAWN
        if (IsServer)
        {
            playersInside.Clear();
            syncedPlayerCount.Value = 0;
        }

        syncedPlayerCount.OnValueChanged += OnPlayerCountChanged;

        UpdateUI(syncedPlayerCount.Value);
        UpdateLockCollider();

        // 🔥 disconnect cleanup
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

        // 🔥 HARD UI RESET
        if (ElevatorMenu.Instance != null)
        {
            ElevatorMenu.Instance.ForceResetUI();
        }
    }

    private void OnPlayerCountChanged(int oldValue, int newValue)
    {
        UpdateUI(newValue);
    }

    // 🔥 remove disconnected player
    private void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer) return;

        if (playersInside.Remove(clientId))
        {
            syncedPlayerCount.Value = playersInside.Count;

            UpdateUI(syncedPlayerCount.Value);
            UpdateLockCollider();
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

        lockCollider.enabled =
            playersInside.Count >= maxPlayers;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        PlayerCubeController player =
            other.GetComponent<PlayerCubeController>();

        if (player == null)
            return;

        ulong id = player.OwnerClientId;

        // 🔥 elevator full
        if (playersInside.Count >= maxPlayers)
            return;

        // 🔥 already inside
        if (!playersInside.Add(id))
            return;

        syncedPlayerCount.Value = playersInside.Count;

        UpdateUI(syncedPlayerCount.Value);
        UpdateLockCollider();

        StartCoroutine(SmoothEnterElevator(player));
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsServer) return;

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
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestLeaveElevatorServerRpc(ulong clientId)
    {
        if (playersInside.Remove(clientId))
        {
            syncedPlayerCount.Value = playersInside.Count;

            UpdateUI(syncedPlayerCount.Value);
            UpdateLockCollider();
        }

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.ClientId != clientId)
                continue;

            PlayerCubeController player =
                client.PlayerObject.GetComponent<PlayerCubeController>();

            if (player != null)
            {
                StartCoroutine(SmoothExitElevator(player));
            }

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

            if (
                Vector3.Distance(t.position, targetPos) < 0.03f &&
                Quaternion.Angle(t.rotation, targetRot) < 1f
            )
                break;

            yield return null;
        }

        t.position = targetPos;
        t.rotation = targetRot;

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

            if (
                Vector3.Distance(t.position, targetPos) < 0.03f &&
                Quaternion.Angle(t.rotation, targetRot) < 1f
            )
                break;

            yield return null;
        }

        t.position = targetPos;
        t.rotation = targetRot;

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

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
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

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}