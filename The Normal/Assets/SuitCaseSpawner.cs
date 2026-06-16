using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class SuitCaseSpawner : NetworkBehaviour
{
    [Header("Suitcase Prefabs")]
    public List<GameObject> suitCases = new();

    [Header("Spawn Points")]
    public List<Transform> spawnPoints = new();

    private bool hasSpawned;

    public void SpawnSuitCases()
    {
        if (!IsServer)
            return;

        if (hasSpawned)
            return;

        hasSpawned = true;

        if (suitCases.Count == 0)
        {
            Debug.LogWarning("No suitcases assigned.");
            return;
        }

        if (spawnPoints.Count < suitCases.Count)
        {
            Debug.LogError(
                $"Not enough spawn points. Need {suitCases.Count}, have {spawnPoints.Count}."
            );
            return;
        }

        List<Transform> availablePoints = new(spawnPoints);

        foreach (GameObject suitCasePrefab in suitCases)
        {
            int randomIndex = Random.Range(0, availablePoints.Count);

            Transform spawnPoint = availablePoints[randomIndex];

            GameObject suitcase = Instantiate(
                suitCasePrefab,
                spawnPoint.position,
                spawnPoint.rotation
            );

            NetworkObject netObj = suitcase.GetComponent<NetworkObject>();

            if (netObj == null)
            {
                Debug.LogError(
                    $"Suitcase prefab '{suitCasePrefab.name}' has NO NetworkObject on the root!"
                );

                Destroy(suitcase);
                continue;
            }

            netObj.Spawn(true);

            Debug.Log(
                $"Spawned suitcase '{suitCasePrefab.name}' NetworkId={netObj.NetworkObjectId}"
            );

            availablePoints.RemoveAt(randomIndex);
        }
    }
}