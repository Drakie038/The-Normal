using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class SecondElevator : MonoBehaviour
{
    [Header("Platform")]
    [SerializeField] private Transform elevatorPlatform;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2f;

    [Header("Spawn Points")]
    [SerializeField] private Transform[] spawnPoints;

    [Header("Drop Distance (Y only)")]
    [SerializeField] private float dropDistance = 20f;

    private Vector3 startPosition;
    private bool startCaptured;

    private void Awake()
    {
        startPosition = elevatorPlatform.position;
    }

    public void StartSecondElevator(List<ulong> passengers)
    {
        // 🔥 BELANGRIJK: pak EXACT scene positie op moment van starten
        startPosition = elevatorPlatform.position;
        startCaptured = true;

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

        while (Vector3.Distance(elevatorPlatform.position, target) > 0.01f)
        {
            elevatorPlatform.position = Vector3.MoveTowards(
                elevatorPlatform.position,
                target,
                moveSpeed * Time.deltaTime
            );

            // 🔥 FORCE PLAYERS MEETRACKEN
            foreach (ulong id in passengers)
            {
                if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(id, out var client))
                    continue;

                var player = client.PlayerObject.GetComponent<PlayerCubeController>();

                if (player != null)
                {
                    Vector3 pos = player.transform.position;
                    pos.y = elevatorPlatform.position.y;
                    player.transform.position = pos;
                }
            }

            yield return null;
        }

        elevatorPlatform.position = target;

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

            Vector3 spawnPos = spawnPoints[index].position;

            player.transform.SetParent(null);
            player.transform.position = spawnPos;
            player.transform.rotation = Quaternion.identity;

            if (cc != null) cc.enabled = true;

            player.SetElevatorFollow(elevatorPlatform);
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

            player.transform.SetParent(null);

            player.SetInElevator(false);
            player.SetFrozen(false);
            player.ResetVelocity();

            player.SetCameraLockedClientRpc(false);
        }
    }
}