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
    public bool frozen;

    public NetworkVariable<bool> inElevator = new NetworkVariable<bool>(false);

    [Header("Camera Pivot")]
    public Transform cameraPivot;

    [Header("Camera Reference")]
    public CameraMovement cam;

    [Header("Player Name")]
    public NetworkVariable<FixedString32Bytes> PlayerName =
        new NetworkVariable<FixedString32Bytes>();

    [SerializeField] private TMP_Text nameText;
    [SerializeField] private Transform nameCanvas;

    // ===== ELEVATOR FIX =====
    private Transform elevatorFollowTarget;
    private Vector3 lastPlatformPos;
    private Coroutine followRoutine;

    private NetworkObjectReference currentElevator;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    public override void OnNetworkSpawn()
    {
        velocity = Vector3.zero;
        canMove = false;
        frozen = false;
        inElevator.Value = false;

        PlayerName.OnValueChanged += OnNameChanged;
        UpdateNameVisual(PlayerName.Value.ToString());

        if (IsOwner)
        {
            cam = FindObjectOfType<CameraMovement>();

            if (cam != null)
                cam.SetTarget(cameraPivot != null ? cameraPivot : transform, this);

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

    // =========================
    // ELEVATOR FIX CORE
    // =========================

    public void SetElevatorFollow(Transform platform)
    {
        elevatorFollowTarget = platform;
        lastPlatformPos = platform.position;

        if (followRoutine != null)
            StopCoroutine(followRoutine);

        followRoutine = StartCoroutine(FollowElevator());
    }

    private IEnumerator FollowElevator()
    {
        while (inElevator.Value && elevatorFollowTarget != null)
        {
            Vector3 delta = elevatorFollowTarget.position - lastPlatformPos;

            // BELANGRIJK: CharacterController move i.p.v. transform.position
            controller.Move(delta);

            lastPlatformPos = elevatorFollowTarget.position;

            yield return null;
        }
    }

    public void SetInElevator(bool value)
    {

        if (!value && followRoutine != null)
        {
            StopCoroutine(followRoutine);
            followRoutine = null;
        }

        inElevator.Value = value;

        if (value)
        {
            moveInput = Vector2.zero;
            velocity = Vector3.zero;
            Input.ResetInputAxes();
        }
    }

    public void SetCurrentElevator(ElevatorPlayers elevator)
    {
        currentElevator = elevator.NetworkObject;
    }

    private void Update()
    {
        if (!IsOwner || !canMove || frozen)
            return;

        var menu = FindObjectOfType<MultiplayerMenu>();
        if (menu != null && menu.IsSettingsOpen())
            return;

        if (inElevator.Value)
        {
            moveInput = Vector2.zero;
            return;
        }

        moveInput = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        );
    }

    private void FixedUpdate()
    {
        if (!IsOwner || frozen)
            return;

        var menu = FindObjectOfType<MultiplayerMenu>();
        if (menu != null && menu.IsSettingsOpen())
            return;

        MoveServerRpc(moveInput, Time.fixedDeltaTime);
    }

    [ServerRpc]
    private void MoveServerRpc(Vector2 input, float dt)
    {
        if (frozen)
            return;

        ApplyGravityServer(dt);

        Vector3 move = Vector3.zero;

        if (!inElevator.Value)
        {
            move =
                transform.right * input.x +
                transform.forward * input.y;
        }

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
        if (inElevator.Value)
            return;

        transform.Rotate(Vector3.up * mouseX);
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

    [ServerRpc]
    public void SetNameServerRpc(string name)
    {
        PlayerName.Value = new FixedString32Bytes(name);
    }

    public void EnableMovement()
    {
        canMove = true;
    }

    public void LeaveElevator()
    {
        if (!IsOwner)
            return;

        if (ElevatorMenu.Instance != null)
            ElevatorMenu.Instance.ShowLeaveButton(false);

        RequestLeaveElevatorServerRpc();
    }

    [ClientRpc]
    public void SetCameraLockedClientRpc(bool value)
    {
        if (!IsOwner) return;

        if (cam == null)
            cam = FindObjectOfType<CameraMovement>();

        if (cam != null)
        {
            if (value)
            {
                cam.PlayElevatorEnterCinematic(transform);
            }
            else
            {
                cam.inputLocked = false;
                cam.elevatorLocked = false;
            }
        }
    }

    public void ResetVelocity()
    {
        velocity = Vector3.zero;
    }

    [ServerRpc]
    private void RequestLeaveElevatorServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;

        if (!currentElevator.TryGet(out NetworkObject obj))
            return;

        ElevatorPlayers elevator = obj.GetComponent<ElevatorPlayers>();

        if (elevator == null)
            return;

        elevator.RequestLeaveElevatorServerRpc(clientId);
    }

    private void LateUpdate()
    {
        if (nameCanvas == null)
            return;

        Camera cam = Camera.main;

        if (cam == null)
            return;

        nameCanvas.LookAt(
            nameCanvas.position + cam.transform.rotation * Vector3.forward,
            cam.transform.rotation * Vector3.up
        );
    }

    public void ForceEnterExitState()
    {
        frozen = true;
        canMove = false;

        moveInput = Vector2.zero;
        velocity = Vector3.zero;

        inElevator.Value = false;

        SetCameraLockedClientRpc(true);
    }
}