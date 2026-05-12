using UnityEngine;
using Unity.Netcode;
using System.Collections;

[RequireComponent(typeof(CharacterController))]
public class PlayerCubeController : NetworkBehaviour
{
    public float moveSpeed = 5f;
    public float gravity = -9.81f;

    private CharacterController controller;
    private Vector3 velocity;

    private Vector2 moveInput;

    private bool canMove = false;
    private float spawnLockTime = 1.5f;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;

        Transform t = transform.Find("CameraTarget");

        CameraMovement cam = FindObjectOfType<CameraMovement>();
        if (cam != null)
            cam.SetTarget(t != null ? t : transform);

        StartCoroutine(EnableMovementAfterDelay());
    }

    private IEnumerator EnableMovementAfterDelay()
    {
        canMove = false;
        yield return new WaitForSeconds(spawnLockTime);
        canMove = true;
    }

    private void Update()
    {
        if (!IsOwner || !canMove) return;

        moveInput = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        );
    }

    private void FixedUpdate()
    {
        if (!IsOwner || !canMove) return;

        MoveServerRpc(moveInput);
    }

    [ServerRpc]
    private void MoveServerRpc(Vector2 input)
    {
        Vector3 move =
            transform.right * input.x +
            transform.forward * input.y;

        controller.Move(move * moveSpeed * Time.fixedDeltaTime);

        if (controller.isGrounded && velocity.y < 0)
            velocity.y = -2f;

        velocity.y += gravity * Time.fixedDeltaTime;
        controller.Move(velocity * Time.fixedDeltaTime);
    }

    [ServerRpc]
    public void SendLookInputServerRpc(float mouseX)
    {
        transform.Rotate(Vector3.up * mouseX);
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner)
        {
            CameraMovement cam = FindObjectOfType<CameraMovement>();
            if (cam != null)
                cam.ResetCameraToMenu();

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        Destroy(gameObject);
    }
}