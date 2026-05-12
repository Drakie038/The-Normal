using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(CharacterController))]
public class PlayerCubeController : NetworkBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float gravity = -9.81f;

    private CharacterController controller;
    private Vector3 velocity;

    private Vector2 moveInput;

    private Transform cameraTarget;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;

        // 🎥 camera koppelen
        Transform t = transform.Find("CameraTarget");
        cameraTarget = (t != null) ? t : transform;

        CameraMovement cam = FindObjectOfType<CameraMovement>();
        if (cam != null)
        {
            cam.SetTarget(cameraTarget);
        }
    }

    private void Update()
    {
        if (!IsOwner) return;

        // 📥 input alleen client
        moveInput = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        );
    }

    private void FixedUpdate()
    {
        if (!IsOwner) return;

        // 🚀 stuur input naar server
        MoveServerRpc(moveInput);
    }

    [ServerRpc]
    private void MoveServerRpc(Vector2 input)
    {
        Vector3 move =
            transform.right * input.x +
            transform.forward * input.y;

        controller.Move(move * moveSpeed * Time.fixedDeltaTime);

        // 🌍 gravity
        if (controller.isGrounded && velocity.y < 0)
            velocity.y = -2f;

        velocity.y += gravity * Time.fixedDeltaTime;
        controller.Move(velocity * Time.fixedDeltaTime);
    }
}