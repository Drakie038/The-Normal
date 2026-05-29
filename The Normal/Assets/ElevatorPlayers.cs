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

    // ✅ ONLY source of truth for UI
    private NetworkVariable<int> playerCount =
        new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // server-only logic tracking
    private HashSet<ulong> playersInside = new HashSet<ulong>();

    public override void OnNetworkSpawn()
    {
        playerCount.OnValueChanged += OnCountChanged;

        UpdateUI(playerCount.Value);
        UpdateLockCollider();
    }

    private void OnCountChanged(int oldValue, int newValue)
    {
        UpdateUI(newValue);
        UpdateLockCollider();
    }

    private void UpdateUI(int value)
    {
        if (playerCountText != null)
            playerCountText.text = $"{value}/{maxPlayers}";
    }

    private void UpdateLockCollider()
    {
        if (lockCollider == null) return;

        lockCollider.enabled = playerCount.Value >= maxPlayers;
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

        // full check
        if (playerCount.Value >= maxPlayers)
            return;

        if (!playersInside.Add(id))
            return;

        playerCount.Value = playersInside.Count;

        StartCoroutine(SmoothEnterElevator(player));
    }

    // =========================
    // EXIT TRIGGER SAFETY
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
            playerCount.Value = playersInside.Count;
        }
    }

    // =========================
    // LEAVE BUTTON
    // =========================

    [ServerRpc(RequireOwnership = false)]
    public void RequestLeaveElevatorServerRpc(ulong clientId)
    {
        playersInside.Remove(clientId);

        playerCount.Value = playersInside.Count;

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
            t.position = Vector3.MoveTowards(t.position, targetPos, moveSpeed * Time.deltaTime);
            t.rotation = Quaternion.RotateTowards(t.rotation, targetRot, rotationSpeed * Time.deltaTime);
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
            t.position = Vector3.MoveTowards(t.position, targetPos, moveSpeed * Time.deltaTime);
            t.rotation = Quaternion.RotateTowards(t.rotation, targetRot, rotationSpeed * Time.deltaTime);
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