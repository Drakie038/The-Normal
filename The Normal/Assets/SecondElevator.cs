using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class SecondElevator : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2f;

    [Header("Spawn Points")]
    [SerializeField] private Transform[] spawnPoints;

    [Header("Drop Distance (Y only)")]
    [SerializeField] private float dropDistance = 20f;

    private Vector3 startPosition;

    [Header("Doors")]
    [SerializeField] private Transform doorLeft;
    [SerializeField] private Transform doorRight;

    [SerializeField] private float doorMoveDistance = 1f;
    [SerializeField] private float doorMoveSpeed = 2f;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            startPosition = transform.position;
        }
    }

    public void StartSecondElevator(List<ulong> passengers)
    {
        if (!IsServer) return;

        startPosition = transform.position;
        StartCoroutine(RunElevator(passengers));
    }

    private IEnumerator RunElevator(List<ulong> passengers)
    {
        TeleportPlayers(passengers);

        Vector3 target = new Vector3(
            startPosition.x,
            startPosition.y - dropDistance,
            startPosition.z
        );

        while (Vector3.Distance(transform.position, target) > 0.01f)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                target,
                moveSpeed * Time.deltaTime
            );

            yield return null;
        }

        transform.position = target;

        StartCoroutine(OpenDoors());
        ReleasePlayers(passengers);
    }

    private void TeleportPlayers(List<ulong> passengers)
    {
        for (int i = 0; i < passengers.Count; i++)
        {
            ulong clientId = passengers[i];

            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
                continue;

            PlayerCubeController player =
                client.PlayerObject.GetComponent<PlayerCubeController>();

            if (player == null)
                continue;

            int index = Mathf.Clamp(i, 0, spawnPoints.Length - 1);

            player.SetFrozen(true);
            player.SetInElevator(true);
            player.SetCameraLockedClientRpc(true);

            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            player.transform.position = spawnPoints[index].position;
            player.transform.rotation = Quaternion.identity;

            if (cc != null) cc.enabled = true;

            // 👇 BELANGRIJK: laat players volgen via network-safe logic
            player.SetElevatorFollow(transform);
        }
    }

    private void ReleasePlayers(List<ulong> passengers)
    {
        foreach (ulong clientId in passengers)
        {
            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
                continue;

            PlayerCubeController player =
                client.PlayerObject.GetComponent<PlayerCubeController>();

            if (player == null)
                continue;

            player.SetInElevator(false);
            player.SetFrozen(false);
            player.ResetVelocity();
            player.SetCameraLockedClientRpc(false);
        }
    }

    private IEnumerator OpenDoors()
    {
        Vector3 leftStart = doorLeft.position;
        Vector3 rightStart = doorRight.position;

        Vector3 leftTarget = leftStart + Vector3.right * doorMoveDistance;
        Vector3 rightTarget = rightStart + Vector3.left * doorMoveDistance;

        while (Vector3.Distance(doorLeft.position, leftTarget) > 0.01f ||
               Vector3.Distance(doorRight.position, rightTarget) > 0.01f)
        {
            doorLeft.position = Vector3.MoveTowards(
                doorLeft.position,
                leftTarget,
                doorMoveSpeed * Time.deltaTime
            );

            doorRight.position = Vector3.MoveTowards(
                doorRight.position,
                rightTarget,
                doorMoveSpeed * Time.deltaTime
            );

            yield return null;
        }

        doorLeft.position = leftTarget;
        doorRight.position = rightTarget;
    }
}