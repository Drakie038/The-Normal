using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(NetworkObject))]
public class PlayerCubeController : NetworkBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;

    private void Update()
    {
        // Alleen de eigenaar van deze cubus mag bewegen
        if (!IsOwner) return;

        float moveX = 0f;
        float moveZ = 0f;

        if (Input.GetKey(KeyCode.W)) moveZ += 1f;
        if (Input.GetKey(KeyCode.S)) moveZ -= 1f;
        if (Input.GetKey(KeyCode.A)) moveX -= 1f;
        if (Input.GetKey(KeyCode.D)) moveX += 1f;

        Vector3 movement = new Vector3(moveX, 0f, moveZ).normalized * moveSpeed * Time.deltaTime;

        if (movement != Vector3.zero)
        {
            MoveServerRpc(movement);
        }
    }

    // ServerRPC: beweeg cubus op server, gesynchroniseerd naar alle clients
    [ServerRpc]
    private void MoveServerRpc(Vector3 delta)
    {
        transform.position += delta;
    }
}