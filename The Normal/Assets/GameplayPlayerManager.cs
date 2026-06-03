using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

public class GamePlayPlayerSpawner : NetworkBehaviour
{
    public static GamePlayPlayerSpawner Instance;

    [SerializeField] private GameObject playerPrefab;

    private Dictionary<ulong, GameObject> spawnedPlayers = new Dictionary<ulong, GameObject>();

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        Instance = this;

        SpawnAllPlayers();
    }

    private void SpawnAllPlayers()
    {
        if (!IsServer) return;

        StartCoroutine(SpawnRoutine());
    }

    private IEnumerator SpawnRoutine()
    {
        yield return new WaitForSeconds(0.5f);

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            ulong id = client.ClientId;

            if (spawnedPlayers.ContainsKey(id))
                continue;

            GameObject obj = Instantiate(playerPrefab);
            NetworkObject netObj = obj.GetComponent<NetworkObject>();

            netObj.SpawnAsPlayerObject(id, true);

            spawnedPlayers[id] = obj;

            // 🔥 CAMERA ATTACH FIX
            StartCoroutine(AttachCameraNextFrame(obj));

            yield return null;
        }

        yield return new WaitForSeconds(0.5f);

        GameplaySeatSpawner.Instance?.AssignAllPlayers();
    }

    private IEnumerator AttachCameraNextFrame(GameObject obj)
    {
        yield return null;

        var cam = FindFirstObjectByType<CameraMovement>();
        var player = obj.GetComponent<PlayerCubeController>();

        if (cam != null && player != null)
        {
            cam.SetDirectFPS(player.transform, player);
        }
    }
}