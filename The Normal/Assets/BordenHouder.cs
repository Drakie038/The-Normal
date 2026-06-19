using System.Collections.Generic;
using UnityEngine;

public class BordenHouder : MonoBehaviour
{
    [Header("Borden van boven naar beneden")]
    public List<DienBlad> borden = new List<DienBlad>();

    [Header("Placement Slots")]
    public Transform[] placementSlots;

    private DienBlad[] placed = new DienBlad[3];

    public bool TryPlace(DienBlad board, int index)
    {
        if (index < 0 || index >= placementSlots.Length)
            return false;

        if (placed[index] != null)
            return false;

        placed[index] = board;

        board.transform.position = placementSlots[index].position;
        board.transform.rotation = placementSlots[index].rotation;

        return true;
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
        if (borden.Contains(bord))
            borden.Remove(bord);
    }
}