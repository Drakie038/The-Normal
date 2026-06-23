using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class BordenHouder : NetworkBehaviour
{
    [Header("Borden van boven naar beneden")]
    public List<DienBlad> borden = new List<DienBlad>();

    [System.Serializable]
    public class PlacementSlot
    {
        public Transform slot;
        public GameObject ghostBord;
        public GameObject ghostDienblad;
    }

    public PlacementSlot[] placementSlots;

    private DienBlad[] placed;

    private void Awake()
    {
        placed = new DienBlad[placementSlots.Length];
    }

    public bool TryPlace(DienBlad board, int index)
    {
        if (!IsServer)
        {
            TryPlaceServerRpc(board.NetworkObjectId, index);
            return false;
        }

        if (index < 0 || index >= placementSlots.Length)
            return false;

        if (placed[index] != null)
            return false;

        placed[index] = board;
        return true;
    }

    [ServerRpc(RequireOwnership = false)]
    private void TryPlaceServerRpc(ulong boardId, int index)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(boardId, out NetworkObject obj))
        {
            DienBlad bord = obj.GetComponent<DienBlad>();

            if (bord == null)
                return;

            if (index < 0 || index >= placementSlots.Length)
                return;

            if (placed[index] != null)
                return;

            placed[index] = bord;

            // 🔥 HIER VOEG JE DE SYNC TOE
            ApplyPlaceClientRpc(boardId, index);
        }
    }

    // Geeft altijd het bovenste bord terug
    public DienBlad GetTopPlate()
    {
        if (borden.Count == 0)
            return null;

        return borden[0];
    }

    // Verwijder een bord uit de stapel zodra het wordt opgepakt
    public void RemovePlate(DienBlad bord)
    {
        if (IsServer)
        {
            if (borden.Contains(bord))
                borden.Remove(bord);
        }
        else
        {
            RemovePlateServerRpc(bord.NetworkObjectId);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RemovePlateServerRpc(ulong boardId)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(boardId, out NetworkObject obj))
        {
            DienBlad bord = obj.GetComponent<DienBlad>();

            if (borden.Contains(bord))
                borden.Remove(bord);
        }
    }
    public void HideAllGhosts()
    {
        for (int i = 0; i < placementSlots.Length; i++)
        {
            if (placementSlots[i].ghostBord != null)
                placementSlots[i].ghostBord.SetActive(false);

            if (placementSlots[i].ghostDienblad != null)
                placementSlots[i].ghostDienblad.SetActive(false);
        }
    }

    public void ShowGhost(int index, DienBlad.DienBladType type)
    {
        if (index < 0 || index >= placementSlots.Length)
            return;

        if (placed[index] != null)
            return;

        HideAllGhosts();

        var slot = placementSlots[index];

        if (type == DienBlad.DienBladType.Bord)
        {
            if (slot.ghostBord != null)
                slot.ghostBord.SetActive(true);
        }
        else
        {
            if (slot.ghostDienblad != null)
                slot.ghostDienblad.SetActive(true);
        }
    }

    [ClientRpc]
    private void ApplyPlaceClientRpc(ulong boardId, int index)
    {
        if (index < 0 || index >= placementSlots.Length)
            return;

        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(boardId, out NetworkObject obj))
        {
            placed[index] = obj.GetComponent<DienBlad>();
        }
    }
}