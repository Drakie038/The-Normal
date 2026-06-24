using UnityEngine;
using TMPro;
using Unity.Netcode;

public class LuggageChecker : NetworkBehaviour
{
    [Header("UI")]
    public TMP_Text progressText;

    [Header("Wall")]
    public Transform wall;

    [Header("Wall Movement")]
    public Vector3 moveAmount;   // hoeveel de muur moet bewegen
    public float moveSpeed = 2f; // snelheid

    [Header("Detection")]
    public BoxCollider detectionBox;

    private bool wallMoved;
    private bool hasLuggage;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip wallMoveSound;

    [ClientRpc]
    private void PlayWallMoveSoundClientRpc()
    {
        if (audioSource != null && wallMoveSound != null)
        {
            audioSource.PlayOneShot(wallMoveSound);
        }
    }

    private void Update()
    {
        if (IsClient)
        {
            if (Input.GetKey(KeyCode.Alpha1) && Input.GetKey(KeyCode.Alpha0))
            {
                RequestForceMoveWallServerRpc();
            }
        }

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

        bool checkerInside = false;
        LuggageCart foundCart = null;

        foreach (var hit in hits)
        {
            // Check of dit de LuggageChecker zelf is
            if (hit.GetComponentInParent<LuggageChecker>() == this)
                checkerInside = true;

            // Check of er een cart aanwezig is
            if (foundCart == null)
                foundCart = hit.GetComponentInParent<LuggageCart>();
        }

        // Als de checker NIET in de box staat → reset UI
        if (!checkerInside)
        {
            if (hasLuggage)
            {
                hasLuggage = false;
                SetUIColorClientRpc(Color.white);
            }

            UpdateUIClientRpc(0);
            return;
        }

        // Checker staat WEL in de box → UI geel + cart evalueren
        if (!hasLuggage)
        {
            hasLuggage = true;
            SetUIColorClientRpc(Color.yellow);
        }

        if (foundCart != null)
            EvaluateCart(foundCart);
        else
            UpdateUIClientRpc(0);
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
        PlayWallMoveSoundClientRpc(); // 🔊 geluid voor iedereen

        Vector3 targetPos = wall.position + moveAmount;
        StartCoroutine(MoveWall(targetPos));
    }

    private System.Collections.IEnumerator MoveWall(Vector3 targetPos)
    {
        while (Vector3.Distance(wall.position, targetPos) > 0.01f)
        {
            wall.position = Vector3.MoveTowards(
                wall.position,
                targetPos,
                moveSpeed * Time.deltaTime
            );

            yield return null;
        }

        wall.position = targetPos;
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestForceMoveWallServerRpc()
    {
        // alleen als hij nog niet bewogen is
        if (wallMoved)
            return;

        wallMoved = true;

        SetUIColorClientRpc(Color.cyan); // optioneel: andere kleur voor forced move
        MoveWallClientRpc();
    }
}
