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

    private HashSet<ulong> playersInside = new HashSet<ulong>();

    public override void OnNetworkSpawn()
    {
        UpdateUI();
        UpdateLockCollider();

        // 🔥 SERVER ONLY: listen for disconnects
        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    // =========================
    // DISCONNECT FIX (IMPORTANT)
    // =========================

    private void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer) return;

        if (playersInside.Remove(clientId))
        {
            UpdateUI();
            UpdateLockCollider();
        }
    }

    // =========================
    // UI
    // =========================

    private void UpdateUI()
    {
        if (playerCountText != null)
            playerCountText.text = $"{playersInside.Count}/{maxPlayers}";
    }

    private void UpdateLockCollider()
    {
        if (lockCollider == null) return;

        lockCollider.enabled = playersInside.Count >= maxPlayers;
    }

    // =========================
    // ENTER
    // =========================

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        PlayerCubeController player =
            other.GetComponent<PlayerCubeController>();

        if (player == null) return;

        ulong id = player.OwnerClientId;

        if (playersInside.Count >= maxPlayers)
            return;

        if (!playersInside.Add(id))
            return;

        UpdateUI();
        UpdateLockCollider();

        StartCoroutine(SmoothEnterElevator(player));
    }

    // =========================
    // EXIT TRIGGER
    // =========================

    private void OnTriggerExit(Collider other)
    {
        if (!IsServer) return;

        PlayerCubeController player =
            other.GetComponent<PlayerCubeController>();

        if (player == null) return;

        ulong id = player.OwnerClientId;

        if (playersInside.Remove(id))
        {
            UpdateUI();
            UpdateLockCollider();
        }
    }

    // =========================
    // LEAVE BUTTON
    // =========================

    [ServerRpc(RequireOwnership = false)]
    public void RequestLeaveElevatorServerRpc(ulong clientId)
    {
        playersInside.Remove(clientId);

        UpdateUI();
        UpdateLockCollider();

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

    // =========================
    // ENTER ANIMATION
    // =========================

    private IEnumerator SmoothEnterElevator(PlayerCubeController player)
    {
        player.SetFrozen(true);
        player.SetInElevator(true);
        player.SetCameraLockedClientRpc(true);

        Transform t = player.transform;

        Vector3 targetPos = new Vector3(
            centerPoint.position.x,
            t.position.y,
            centerPoint.position.z
        );

        Quaternion targetRot = Quaternion.Euler(0f, 180f, 0f);

        while (Vector3.Distance(t.position, targetPos) > 0.03f)
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

            yield return null;
        }

        t.position = targetPos;
        t.rotation = targetRot;

        player.SetCurrentElevator(this);

        ShowElevatorUIClientRpc(player.OwnerClientId);
    }

    // =========================
    // EXIT ANIMATION
    // =========================

    private IEnumerator SmoothExitElevator(PlayerCubeController player)
    {
        Transform t = player.transform;

        Vector3 targetPos = new Vector3(
            exitPoint.position.x,
            t.position.y,
            exitPoint.position.z
        );

        Quaternion targetRot = Quaternion.Euler(0f, 180f, 0f);

        while (Vector3.Distance(t.position, targetPos) > 0.03f)
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

            yield return null;
        }

        t.position = targetPos;
        t.rotation = targetRot;

        player.SetInElevator(false);
        player.SetFrozen(false);
        player.SetCameraLockedClientRpc(false);

        HideElevatorUIClientRpc(player.OwnerClientId);
    }

    // =========================
    // UI CLIENT RPC
    // =========================

    [ClientRpc]
    private void ShowElevatorUIClientRpc(ulong targetClientId)
    {
        if (NetworkManager.Singleton.LocalClientId != targetClientId)
            return;

        ElevatorMenu.Instance?.ShowLeaveButton(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    [ClientRpc]
    private void HideElevatorUIClientRpc(ulong targetClientId)
    {
        if (NetworkManager.Singleton.LocalClientId != targetClientId)
            return;

        ElevatorMenu.Instance?.ShowLeaveButton(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}