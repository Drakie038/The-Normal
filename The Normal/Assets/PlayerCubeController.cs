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
    public float pushSpeed = 2.5f;
    public float PushRotationSpeed = 0.25f;
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

    public LuggageCart currentLuggage;
    private Vector3 lastPlayerPos;

    private bool pushApproaching;
    private float approachSpeed = 6f;
    private float alignSpeed = 10f;

    private Vector3 luggageOffsetLocal;
    private bool hasLuggageOffset;

    private Quaternion luggageRotationOffset;

    private void Start()
    {
        lastPlayerPos = transform.position;
    }

    public void SetLuggage(LuggageCart luggage)
    {
        currentLuggage = luggage;

        if (currentLuggage != null)
        {
            currentLuggage.SetPushPhysics(true);
        }
    }
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
            float turn = 0f;

            // WASD input
            if (Input.GetKey(KeyCode.W))
                forward = 1f;
            else if (Input.GetKey(KeyCode.S))
                forward = -1f;

            if (Input.GetKey(KeyCode.D))
                turn = 1f;
            else if (Input.GetKey(KeyCode.A))
                turn = -1f;

            // richting correctie
            if (pushTarget != null && currentLuggage != null)
            {
                if (pushTarget == currentLuggage.pushFor)
                {
                    forward *= -1f;
                }
            }

            // ===== ROTATIE LOGICA =====
            float rotationInput = 0f;

            bool isFront =
                currentLuggage != null &&
                pushTarget == currentLuggage.pushFor;

            if (forward > 0f)
            {
                rotationInput = turn;
            }
            else if (forward < 0f)
            {
                rotationInput = -turn;
            }

            // voorkant van de trolley = stuurrichting omdraaien
            if (isFront)
            {
                rotationInput *= -1f;
            }

            if (rotationInput != 0f)
                SendLookInputServerRpc(rotationInput * PushRotationSpeed);

            moveInput = new Vector2(0f, forward);

            // =====================================================
            // LUGGAGE FOLLOW (blijft vóór speler + roteert mee)
            // =====================================================
            if (currentLuggage != null && hasLuggageOffset)
            {
                Vector3 targetPos =
                    transform.position +
                    transform.forward * Vector3.Distance(
                        currentLuggage.transform.position,
                        transform.position
                    );

                currentLuggage.transform.position = Vector3.Lerp(
                    currentLuggage.transform.position,
                    targetPos,
                    20f * Time.deltaTime
                );

                Quaternion targetRot =
                    transform.rotation * luggageRotationOffset;

                currentLuggage.transform.rotation = Quaternion.Slerp(
                    currentLuggage.transform.rotation,
                    targetRot,
                    8f * Time.deltaTime
                );
            }

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

        if (frozen && !inPushMode)
            return;

        var menu = FindObjectOfType<MultiplayerMenu>();
        if (menu != null && menu.IsSettingsOpen())
            return;

        if (inPushMode && pushTarget != null)
        {
            if (pushApproaching)
            {
                HandlePushApproach();
                return;
            }

            float input = moveInput.y;
            PushMoveServerRpc(pushTarget.forward, input);
            return;
        }

        MoveServerRpc(moveInput, Time.fixedDeltaTime);
    }

    private void HandlePushApproach()
    {
        if (currentLuggage == null || pushTarget == null)
            return;

        float dt = Time.fixedDeltaTime;

        // ================= POSITION ALIGN =================
        Vector3 targetPos = pushTarget.position;

        Vector3 newPos = Vector3.Lerp(
            transform.position,
            targetPos,
            approachSpeed * dt
        );

        controller.Move(newPos - transform.position);

        // ================= ROTATION ALIGN =================
        Vector3 dir = pushTarget.forward;
        dir.y = 0f;

        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRot,
                alignSpeed * dt
            );
        }

        // ================= FINISH CONDITION =================
        float dist = Vector3.Distance(transform.position, targetPos);
        float angle = Vector3.Angle(transform.forward, dir);

        if (dist < 0.05f && angle < 5f)
        {
            luggageRotationOffset =
    Quaternion.Inverse(transform.rotation) *
    currentLuggage.transform.rotation;

            pushApproaching = false;
            pushReady = true;
        }

        if (!inPushMode)
            return;
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
        if (!IsOwner) return;

        lastPlayerPos = transform.position;

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

        if (!value)
        {
            pushApproaching = false;
            pushReady = false;
        }
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

            if (currentLuggage != null)
                currentLuggage.SetPushPhysics(false);

            currentLuggage = null;
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
    private void PushMoveServerRpc(Vector3 forwardDir, float input)
    {
        if (currentLuggage == null || pushTarget == null)
            return;

        Vector3 dir = forwardDir;
        dir.y = 0f;
        dir.Normalize();

        // =========================
        // bepaal correcte richting per kant
        // =========================
        bool isFront = (pushTarget == currentLuggage.pushFor);

        // als je aan de voorkant staat:
        // forward = van speler AF
        if (isFront)
        {
            dir = -dir;
        }

        // input blijft normaal:
        // W = +1, S = -1
        float moveInput = input;

        Vector3 move =
            dir *
            moveInput *
            pushSpeed *
            Time.fixedDeltaTime;

        Vector3 oldPos = currentLuggage.transform.position;

        currentLuggage.transform.position += move;

        if (Physics.SphereCast(oldPos, 0.3f, dir, out RaycastHit hit, move.magnitude))
        {
            currentLuggage.transform.position = oldPos;
            return;
        }

        Vector3 delta = currentLuggage.transform.position - oldPos;
        controller.Move(delta);
    }

    private bool IsCorrectPushSide()
    {
        if (pushTarget == null) return false;

        Vector3 toPlayer = (transform.position - pushTarget.position);
        toPlayer.y = 0f;

        Vector3 forward = pushTarget.forward;
        forward.y = 0f;

        float frontDot = Vector3.Dot(forward, toPlayer.normalized);

        // front side
        if (pushTarget == currentLuggage?.pushFor)
        {
            return frontDot > 0.5f;
        }

        // back side
        if (pushTarget == currentLuggage?.pushBack)
        {
            return frontDot < -0.5f;
        }

        return false;
    }

    public void StartPush(LuggageCart luggage, Transform target)
    {
        if (luggage == null || target == null)
            return;

        currentLuggage = luggage;

        hasLuggageOffset = true;
        luggageOffsetLocal =
            Quaternion.Inverse(transform.rotation) *
            (currentLuggage.transform.position - transform.position);

        SetPushMode(true, target); // ← BELANGRIJK: first set canonical state

        pushTarget = target;

        frozen = false;
        canMove = true;

        moveInput = Vector2.zero;
        velocity = Vector3.zero;

        pushReady = false;
        pushApproaching = true;

        inPushMode = true; // 🔥 HARD GUARANTEE (fixes your bug)

        currentLuggage.SetPushPhysics(true);
    }
    public void StopPush()
    {
        SetPushMode(false, null);

        if (currentLuggage != null)
        {
            currentLuggage.SetPushPhysics(false);
            currentLuggage = null;
        }

        hasLuggageOffset = false;
    }
}