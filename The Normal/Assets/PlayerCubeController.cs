using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(NetworkObject))]
public class PlayerCubeController : NetworkBehaviour
{
    public float moveSpeed = 5f;

    private Vector3 inputDir;

    private void Update()
    {
        if (!IsOwner) return;

        inputDir = new Vector3(
            Input.GetAxisRaw("Horizontal"),
            0f,
            Input.GetAxisRaw("Vertical")
        ).normalized;
    }

    private void FixedUpdate()
    {
        if (!IsOwner) return;

        if (inputDir == Vector3.zero) return;

        MoveServerRpc(inputDir);
    }

    [ServerRpc]
    private void MoveServerRpc(Vector3 direction)
    {
        transform.position += direction * moveSpeed * Time.fixedDeltaTime;
    }
}