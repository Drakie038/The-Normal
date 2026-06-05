using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class SecondElevator : NetworkBehaviour
{
    [Header("Platform")]
    [SerializeField] private Transform elevatorPlatform;

    [Header("Movement")]
    [SerializeField] private Vector3 endPosition;
    [SerializeField] private float moveSpeed = 2f;

    [Header("Spawn Points")]
    [SerializeField] private Transform[] spawnPoints;

    [Header("Trigger")]
    [SerializeField] private Collider elevatorTrigger;

    private Vector3 startPosition;

    public override void OnNetworkSpawn()
    {
        if (IsServer && elevatorPlatform != null)
        {
            startPosition = elevatorPlatform.position; // 🔥 BELANGRIJK
        }
    }

    public void StartSecondElevator(List<ulong> passengers)
    {
        if (!IsServer) return;

        TeleportPlayers(passengers);
        StartCoroutine(MoveElevator(passengers));
    }

    private void TeleportPlayers(List<ulong> passengers)
    {
        for (int i = 0; i < passengers.Count; i++)
        {
            ulong clientId = passengers[i];

            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
                continue;

            PlayerCubeController player = client.PlayerObject.GetComponent<PlayerCubeController>();
            if (player == null) continue;

            int index = Mathf.Clamp(i, 0, spawnPoints.Length - 1);

            player.SetFrozen(true);
            player.SetInElevator(true);
            player.SetCameraLockedClientRpc(true);

            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            player.transform.position = spawnPoints[index].position;
            player.transform.rotation = Quaternion.identity;

            if (cc != null) cc.enabled = true;

            player.transform.SetParent(elevatorPlatform);
        }
    }

    private IEnumerator MoveElevator(List<ulong> passengers)
    {
        while (Vector3.Distance(elevatorPlatform.position, endPosition) > 0.01f)
        {
            elevatorPlatform.position = Vector3.MoveTowards(
                elevatorPlatform.position,
                endPosition,
                moveSpeed * Time.deltaTime
            );

            yield return null;
        }

        elevatorPlatform.position = endPosition;

        if (elevatorTrigger != null)
            elevatorTrigger.enabled = false;

        ReleasePlayers(passengers);
    }

    private void ReleasePlayers(List<ulong> passengers)
    {
        foreach (ulong clientId in passengers)
        {
            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
                continue;

            PlayerCubeController player = client.PlayerObject.GetComponent<PlayerCubeController>();
            if (player == null) continue;

            player.transform.SetParent(null);

            player.SetInElevator(false);
            player.SetFrozen(false);
            player.ResetVelocity();

            player.SetCameraLockedClientRpc(false);
        }
    }
}