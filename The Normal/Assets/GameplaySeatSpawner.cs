using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

public class GameplaySeatSpawner : NetworkBehaviour
{
    public static GameplaySeatSpawner Instance;

    [SerializeField] private Transform[] seatPoints;

    private bool[] seatOccupied;
    private Dictionary<ulong, int> playerSeatIndex = new Dictionary<ulong, int>();

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        Instance = this;
        seatOccupied = new bool[seatPoints.Length];

        StartCoroutine(DelayedAssign());
    }

    private IEnumerator DelayedAssign()
    {
        yield return new WaitForSeconds(1.0f);
        AssignAllPlayers();
    }

    public void AssignAllPlayers()
    {
        if (!IsServer) return;

        StartCoroutine(AssignRoutine());
    }

    private IEnumerator AssignRoutine()
    {
        yield return new WaitForSeconds(0.5f);

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject == null)
                continue;

            AssignSeat(client.ClientId, client.PlayerObject);
        }
    }

    private void AssignSeat(ulong clientId, NetworkObject playerObj)
    {
        int seatIndex = GetFreeSeat();
        if (seatIndex == -1) return;

        seatOccupied[seatIndex] = true;
        playerSeatIndex[clientId] = seatIndex;

        Transform seat = seatPoints[seatIndex];

        StartCoroutine(MoveToSeat(playerObj.transform, seat.position, seat.rotation));
    }

    private int GetFreeSeat()
    {
        for (int i = 0; i < seatOccupied.Length; i++)
            if (!seatOccupied[i]) return i;

        return -1;
    }

    private IEnumerator MoveToSeat(Transform player, Vector3 pos, Quaternion rot)
    {
        float speed = 6f;

        while (Vector3.Distance(player.position, pos) > 0.02f)
        {
            player.position = Vector3.MoveTowards(player.position, pos, speed * Time.deltaTime);
            player.rotation = Quaternion.RotateTowards(player.rotation, rot, 360f * Time.deltaTime);
            yield return null;
        }

        player.position = pos;
        player.rotation = rot;
    }
}