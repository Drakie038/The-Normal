using UnityEngine;
using Unity.Netcode;
using TMPro;
using System.Collections.Generic;

public class ElevatorPlayers : NetworkBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text playerCountText;

    // netwerk gesynchroniseerde count
    private NetworkVariable<int> playerCount = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // voorkomt dubbele counts per player
    private HashSet<ulong> playersInside = new HashSet<ulong>();

    private void Start()
    {
        UpdateUI(playerCount.Value);
    }

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
        {
            playerCountText.text = $"Players: {value}";
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        PlayerCubeController player = other.GetComponent<PlayerCubeController>();
        if (player == null) return;

        ulong id = player.OwnerClientId;

        if (playersInside.Add(id))
        {
            playerCount.Value++;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsServer) return;

        PlayerCubeController player = other.GetComponent<PlayerCubeController>();
        if (player == null) return;

        ulong id = player.OwnerClientId;

        if (playersInside.Remove(id))
        {
            playerCount.Value = Mathf.Max(0, playerCount.Value - 1);
        }
    }
}