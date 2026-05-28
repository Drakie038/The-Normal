using UnityEngine;
using Unity.Netcode;
using System.Collections;
using Unity.Collections;
using TMPro;

[RequireComponent(typeof(CharacterController))]
public class PlayerCubeController : NetworkBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float gravity = -9.81f;

    private CharacterController controller;
    private Vector3 velocity;
    private Vector2 moveInput;

    private bool canMove;
    private bool frozen;

    public bool inElevator;

    [Header("Camera Pivot")]
    public Transform cameraPivot;

    [Header("Camera Reference")]
    public CameraMovement cam;

    [Header("Player Name")]
    public NetworkVariable<FixedString32Bytes> PlayerName =
        new NetworkVariable<FixedString32Bytes>();

    [SerializeField] private TMP_Text nameText;

    private ElevatorPlayers currentElevator;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    public override void OnNetworkSpawn()
    {
        velocity = Vector3.zero;
        canMove = false;
        frozen = false;
        inElevator = false;

        PlayerName.OnValueChanged += OnNameChanged;
        UpdateNameVisual(PlayerName.Value.ToString());

        if (IsOwner)
        {
            cam = FindObjectOfType<CameraMovement>();

            if (cam != null)
                cam.SetTarget(
                    cameraPivot != null ? cameraPivot : transform,
                    this
                );

            var menu = FindObjectOfType<MultiplayerMenu>();
            if (menu != null)
                SetNameServerRpc(menu.GetPlayerName());
        }
    }

    public override void OnNetworkDespawn()
    {
        PlayerName.OnValueChanged -= OnNameChanged;
    }

    private void OnNameChanged(FixedString32Bytes oldName, FixedString32Bytes newName)
    {
        UpdateNameVisual(newName.ToString());
    }

    private void UpdateNameVisual(string playerName)
    {
        if (nameText != null)
            nameText.text = string.IsNullOrEmpty(playerName) ? "Player" : playerName;
    }

    public void EnableMovement()
    {
        canMove = true;
    }

    public void SetFrozen(bool value)
    {
        frozen = value;

        if (value)
        {
            moveInput = Vector2.zero;
            velocity = Vector3.zero;
            Input.ResetInputAxes();
        }
    }

    public void SetInElevator(bool value)
    {
        inElevator = value;

        if (value)
        {
            moveInput = Vector2.zero;
            velocity = Vector3.zero;
            Input.ResetInputAxes();
        }
    }

    public void SetCurrentElevator(ElevatorPlayers elevator)
    {
        currentElevator = elevator;
    }

    public void LeaveElevator()
    {
        if (!IsOwner || currentElevator == null)
            return;

        currentElevator.RequestLeaveElevatorServerRpc(OwnerClientId);
    }

    public void SetCameraLocked(bool value)
    {
        if (!IsOwner) return;

        if (cam == null)
            cam = FindObjectOfType<CameraMovement>();

        if (cam != null)
        {
            cam.inputLocked = value;
            cam.elevatorLocked = value;
        }
    }

    [ClientRpc]
    public void SetCameraLockedClientRpc(bool value)
    {
        if (!IsOwner) return;

        if (cam == null)
            cam = FindObjectOfType<CameraMovement>();

        if (cam != null)
        {
            cam.inputLocked = value;
            cam.elevatorLocked = value;
        }
    }

    private void Update()
    {
        if (!IsOwner || !canMove || frozen || inElevator)
            return;

        moveInput = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        );
    }

    private void FixedUpdate()
    {
        if (!IsOwner || frozen || inElevator)
            return;

        MoveServerRpc(moveInput, Time.fixedDeltaTime);
    }

    [ServerRpc]
    private void MoveServerRpc(Vector2 input, float dt)
    {
        if (frozen || inElevator)
            return;

        ApplyGravityServer(dt);

        Vector3 move =
            transform.right * input.x +
            transform.forward * input.y;

        controller.Move((move * moveSpeed + velocity) * dt);
    }

    private void ApplyGravityServer(float dt)
    {
        if (controller.isGrounded && velocity.y < 0)
            velocity.y = -2f;

        velocity.y += gravity * dt;
    }

    [ServerRpc]
    public void SendLookInputServerRpc(float mouseX)
    {
        if (inElevator) return;

        transform.Rotate(Vector3.up * mouseX);
    }

    [ServerRpc]
    public void SetNameServerRpc(string name)
    {
        PlayerName.Value = new FixedString32Bytes(name);
    }

    public void ForceRotation(Quaternion rot)
    {
        transform.rotation = rot;
    }

    public void TeleportFromServer(Vector3 pos)
    {
        StartCoroutine(TeleportRoutine(pos));
    }

    private IEnumerator TeleportRoutine(Vector3 pos)
    {
        controller.enabled = false;
        transform.position = pos;
        yield return null;
        controller.enabled = true;
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestExitElevatorServerRpc(Vector3 exitPos)
    {
        StartCoroutine(TeleportRoutine(exitPos));

        SetInElevator(false);
        SetFrozen(false);

        SetCameraLockedClientRpc(false);
    }
}