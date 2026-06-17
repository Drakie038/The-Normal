using UnityEngine;
using TMPro;
using Unity.Netcode;

public class LuggageChecker : NetworkBehaviour
{
    [Header("UI")]
    public TMP_Text progressText;

    [Header("Wall")]
    public Transform wall;
    public Vector3 moveOffset = new Vector3(0, 5f, 0);
    public float moveSpeed = 2f;

    [Header("Detection")]
    public BoxCollider detectionBox;

    private bool wallMoved;
    private bool hasLuggage;

    private void Update()
    {
        if (!IsServer) return;

        CheckForLuggage();
    }

    private void CheckForLuggage()
    {
        Collider[] hits = Physics.OverlapBox(
            detectionBox.bounds.center,
            detectionBox.bounds.extents,
            detectionBox.transform.rotation
        );

        LuggageCart foundCart = null;

        foreach (var hit in hits)
        {
            foundCart = hit.GetComponentInParent<LuggageCart>();
            if (foundCart != null)
                break;
        }

        if (foundCart != null)
        {
            if (!hasLuggage)
            {
                hasLuggage = true;
                SetUIColorClientRpc(Color.yellow);
            }

            EvaluateCart(foundCart);
        }
        else
        {
            if (hasLuggage)
            {
                hasLuggage = false;
                SetUIColorClientRpc(Color.white);
            }

            UpdateUIClientRpc(0);
        }
    }

    private void EvaluateCart(LuggageCart cart)
    {
        int count = 0;

        if (cart.red) count++;
        if (cart.blue) count++;
        if (cart.black) count++;
        if (cart.green) count++;
        if (cart.yellow) count++;
        if (cart.magenta) count++;
        if (cart.teal) count++;
        if (cart.purple) count++;

        UpdateUIClientRpc(count);

        if (count >= 8 && !wallMoved)
        {
            wallMoved = true;
            SetUIColorClientRpc(Color.green);
            MoveWallClientRpc();
        }
    }

    [ClientRpc]
    private void UpdateUIClientRpc(int count)
    {
        if (progressText != null)
            progressText.text = $"{count}/8";
    }

    [ClientRpc]
    private void SetUIColorClientRpc(Color color)
    {
        if (progressText != null)
            progressText.color = color;
    }

    [ClientRpc]
    private void MoveWallClientRpc()
    {
        StartCoroutine(MoveWall());
    }

    private System.Collections.IEnumerator MoveWall()
    {
        Vector3 startPos = wall.position;
        Vector3 targetPos = startPos + moveOffset;

        while (Vector3.Distance(wall.position, targetPos) > 0.01f)
        {
            wall.position = Vector3.Lerp(
                wall.position,
                targetPos,
                Time.deltaTime * moveSpeed
            );

            yield return null;
        }

        wall.position = targetPos;
    }
}