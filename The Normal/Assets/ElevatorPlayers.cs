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

    [Header("Smooth")]
    [SerializeField] private float rotateDuration = 2f;

    private NetworkVariable<int> playerCount =
        new NetworkVariable<int>(0);

    private HashSet<ulong> playersInside = new HashSet<ulong>();
    private Coroutine enterRoutine;

    public override void OnNetworkSpawn()
    {
        playerCount.OnValueChanged += OnCountChanged;
        UpdateUI(playerCount.Value);
    }

    public override void OnNetworkDespawn()
    {
        playerCount.OnValueChanged -= OnCountChanged;
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

        Transform t = player.transform;

        Vector3 startPos = t.position;
        Quaternion startRot = t.rotation;

        Vector3 targetPos =
            new Vector3(
                centerPoint.position.x,
                t.position.y,
                centerPoint.position.z
            );

        Vector3 dir = centerPoint.position - t.position;
        dir.y = 0f;

        Quaternion targetRot = Quaternion.LookRotation(dir);

        CameraMovement cam = FindObjectOfType<CameraMovement>();

        ElevatorLeaveButton leaveBtn =
            FindObjectOfType<ElevatorLeaveButton>();

        if (leaveBtn != null && player.IsOwner)
            leaveBtn.ShowButton(true);

        if (cam != null && player.IsOwner)
        {
            StartCoroutine(
                cam.ElevatorLookAt(
                    player.cameraPivot != null ? player.cameraPivot : player.transform,
                    rotateDuration
                )
            );
        }

        float tValue = 0f;

        while (tValue < rotateDuration)
        {
            tValue += Time.deltaTime;

            float n = Mathf.Clamp01(tValue / rotateDuration);

            t.position = Vector3.Lerp(startPos, targetPos, n);
            t.rotation = Quaternion.Slerp(startRot, targetRot, n);

            yield return null;
        }

        t.position = targetPos;
        t.rotation = targetRot;
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
            enterRoutine = StartCoroutine(SmoothEnterElevator(player));
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