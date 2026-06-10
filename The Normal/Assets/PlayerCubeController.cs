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

    public bool inPushMode;
    private Transform pushTarget;

    private Quaternion pushRotationVelocity;
    private Vector3 pushPositionVelocity;

    private Vector3 pushForwardDir;
    private bool pushReady;

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

        if (inPushMode && pushReady)
        {
            float forward = 0f;

            if (Input.GetKey(KeyCode.W))
                forward = 1f;
            else if (Input.GetKey(KeyCode.S))
                forward = -1f;

            moveInput = new Vector2(0f, forward);
            return;
        }

        moveInput = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        );
    }

    private void FixedUpdate()
    {
        if (!IsOwner)
            return;

        // ⭐ ADD DIT HIER (helemaal bovenaan)
        if (inPushMode && pushTarget != null)
        {
            pushForwardDir = pushTarget.forward;
            pushForwardDir.y = 0f;
            pushReady = true;
        }

        if (frozen && !inPushMode)
            return;

        var menu = FindObjectOfType<MultiplayerMenu>();
        if (menu != null && menu.IsSettingsOpen())
            return;

        if (inPushMode && pushTarget != null)
        {
            PushMoveServerRpc(pushTarget.position, pushTarget.forward);
            return;
        }

        MoveServerRpc(moveInput, Time.fixedDeltaTime);
    }

    private void HandlePushMode()
    {
        float dt = Time.fixedDeltaTime;

        // ================= POSITION SMOOTH =================
        Vector3 targetPos = pushTarget.position;

        Vector3 newPos = Vector3.SmoothDamp(
            transform.position,
            targetPos,
            ref pushPositionVelocity,
            0.12f
        );

        controller.Move(newPos - transform.position);

        // ================= ROTATION FORCE =================
        Vector3 dir = pushTarget.forward;
        dir.y = 0f;

        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRot,
                dt * 10f
            );
        }
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
            if (inPushMode && pushReady)
            {
                move = pushForwardDir * input.y;
            }
            else
            {
                move =
                    transform.right * input.x +
                    transform.forward * input.y;
            }
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

    public void SetPushMode(bool value, Transform target)
    {
        inPushMode = value;
        pushTarget = target;

        frozen = false;
        canMove = true;

        moveInput = Vector2.zero;
        velocity = Vector3.zero;

        pushReady = false;

        if (!value)
        {
            pushTarget = null;
            pushForwardDir = Vector3.zero;
        }
    }

    public void ForcePushUpdate()
    {
        if (!inPushMode || pushTarget == null)
            return;

        float dt = Time.fixedDeltaTime;

        // ================= SMOOTH POSITION =================
        Vector3 targetPos = pushTarget.position;

        Vector3 nextPos = Vector3.Lerp(
            transform.position,
            targetPos,
            12f * dt
        );

        controller.Move(nextPos - transform.position);

        // ================= FORCE ROTATION =================
        Vector3 dir = pushTarget.forward;
        dir.y = 0f;

        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion rot = Quaternion.LookRotation(dir);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                rot,
                15f * dt
            );
        }
    }

    [ServerRpc]
    private void PushMoveServerRpc(Vector3 targetPos, Vector3 forwardDir)
    {
        Vector3 nextPos = Vector3.Lerp(transform.position, targetPos, 0.35f);

        controller.Move(nextPos - transform.position);

        Vector3 dir = forwardDir;
        dir.y = 0f;

        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion rot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, 0.35f);
        }
    }
}