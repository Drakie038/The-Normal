using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkTransform))]
public class PlayerCubeController : NetworkBehaviour
{
    public float moveSpeed = 5f;

    private void Update()
    {
        // 🔥 alleen input owner
        if (!IsOwner) return;

        Vector3 input = new Vector3(
            Input.GetAxisRaw("Horizontal"),
            0f,
            Input.GetAxisRaw("Vertical")
        ).normalized;

        if (input == Vector3.zero) return;

        MoveServerRpc(input);
    }

    // =====================================================
    // SERVER MOVEMENT (FAIR + REALISTIC)
    // =====================================================
    [ServerRpc]
    private void MoveServerRpc(Vector3 direction)
    {
        transform.position += direction * moveSpeed * Time.deltaTime;
    }
}