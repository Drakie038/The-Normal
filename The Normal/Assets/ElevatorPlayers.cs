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

    private NetworkVariable<int> playerCount =
        new NetworkVariable<int>(0);

    private HashSet<ulong> playersInside =
        new HashSet<ulong>();

    public override void OnNetworkSpawn()
    {
        playerCount.OnValueChanged += OnCountChanged;
        UpdateUI(playerCount.Value);
    }

    private void OnCountChanged(int oldValue, int newValue)
    {
        UpdateUI(newValue);
    }

    private void UpdateUI(int value)
    {
        if (playerCountText != null)
            playerCountText.text = $"{value}";
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

        // 🔥 CLIENT UI + CURSOR
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

        // 🔥 CLIENT UI + CURSOR
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

    [ServerRpc(RequireOwnership = false)]
    public void RequestLeaveElevatorServerRpc(ulong clientId)
    {
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.ClientId != clientId)
                continue;

            PlayerCubeController player =
                client.PlayerObject.GetComponent<PlayerCubeController>();

            if (player == null)
                return;

            playersInside.Remove(clientId);

            playerCount.Value =
                Mathf.Max(0, playerCount.Value - 1);

            StartCoroutine(SmoothExitElevator(player));

            break;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        PlayerCubeController player =
            other.GetComponent<PlayerCubeController>();

        if (player == null) return;

        ulong id = player.OwnerClientId;

        if (playersInside.Add(id))
        {
            playerCount.Value++;

            StartCoroutine(SmoothEnterElevator(player));
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsServer) return;

        PlayerCubeController player =
            other.GetComponent<PlayerCubeController>();

        if (player == null) return;

        ulong id = player.OwnerClientId;

        if (playersInside.Remove(id))
        {
            playerCount.Value =
                Mathf.Max(0, playerCount.Value - 1);
        }
    }
}