using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class DoorHallway : NetworkBehaviour
{
    [Header("Door Colliders (CHILDREN)")]
    public Collider frontCollider;
    public Collider backCollider;

    [Header("Door Settings")]
    public float openAngle = 90f;
    public float peekAngleMultiplier = 0.5f;
    public float openSpeed = 6f;

    [Header("Camera Lean")]
    public float cameraLeanAngle = 8f;

    [Header("Highlight")]
    public Color highlightColor = Color.yellow;
    [Range(0f, 2f)] public float intensity = 0.3f;

    private Renderer rend;
    private Material mat;

    private PlayerCubeController currentPlayer;
    private CameraMovement currentCamera;

    private bool isHighlighted;
    private float holdTimer;
    private const float holdThreshold = 0.25f;
    private bool holding;

    private Quaternion closedRotation;
    private Quaternion openRotation;
    private Quaternion peekRotation;

    [Header("Front / Back Targets")]
    public Transform frontPoint;
    public Transform backPoint;

    [Header("Front / Back Exit Targets")]
    public Transform front2;
    public Transform back2;

    private Vector3 playerVelocity;
    private bool isMovingPlayer;
    private Transform playerMoveTarget;

    private bool isTransitioning;

    private NetworkVariable<ulong> peekOwnerClientId =
    new NetworkVariable<ulong>(
        ulong.MaxValue,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public float GetLean()
    {
        // ❌ NO LEAN unless actively peeking
        if (netState.Value != DoorState.Peek)
            return 0f;

        float currentAngle = Quaternion.Angle(
            closedRotation,
            transform.rotation
        );

        float targetPeekAngle = openAngle * peekAngleMultiplier;

        float progress = Mathf.InverseLerp(
            0f,
            targetPeekAngle,
            currentAngle
        );

        return cameraLeanAngle *
               progress *
               netDirection.Value;
    }


    // ================= NETWORK STATE =================

    public enum DoorState : byte
    {
        Closed,
        Open,
        Peek
    }

    private IEnumerator MovePlayerToExitPoint()
    {
        if (currentPlayer == null)
            yield break;

        Transform target = (netDirection.Value > 0f) ? front2 : back2;

        if (target == null)
            yield break;

        currentPlayer.SetFrozen(true);

        while (Vector3.Distance(currentPlayer.transform.position, target.position) > 0.05f)
        {
            currentPlayer.transform.position = Vector3.MoveTowards(
                currentPlayer.transform.position,
                target.position,
                openSpeed * Time.deltaTime
            );

            yield return null;
        }

        currentPlayer.transform.position = target.position;

        // ✅ BELANGRIJK: speler weer vrijgeven
        currentPlayer.SetFrozen(false);
    }

    private void HandlePlayerPeekMovement()
    {
        if (currentPlayer == null)
        {
            currentPlayer = FindObjectOfType<PlayerCubeController>();
        }

        if (currentPlayer == null)
            return;

        if (netState.Value != DoorState.Peek)
        {
            isMovingPlayer = false;
            return;
        }

        playerMoveTarget = (netDirection.Value > 0f) ? frontPoint : backPoint;

        if (playerMoveTarget == null)
            return;

        isMovingPlayer = true;

        currentPlayer.transform.position = Vector3.SmoothDamp(
            currentPlayer.transform.position,
            playerMoveTarget.position,
            ref playerVelocity,
            0.25f
        );
    }

    private NetworkVariable<DoorState> netState =
        new NetworkVariable<DoorState>(
            DoorState.Closed,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private NetworkVariable<float> netDirection =
        new NetworkVariable<float>(
            1f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private void Awake()
    {
        rend = GetComponentInChildren<Renderer>();
        if (rend != null) mat = rend.material;

        closedRotation = transform.rotation;

        // ❗ FIX: detach targets zodat ze niet mee roteren
        if (frontPoint != null) frontPoint.SetParent(null);
        if (backPoint != null) backPoint.SetParent(null);
        if (front2 != null) front2.SetParent(null);
        if (back2 != null) back2.SetParent(null);
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            netState.Value = DoorState.Closed;
            netDirection.Value = 1f;
        }
    }

    private void Update()
    {
        HandleInput();
        UpdateRotationsFromState();
        HandleRotation();

        HandlePlayerPeekMovement();
    }

    // ================= INPUT =================

    private void HandleInput()
    {
        // ❌ BLOCK OTHER PLAYERS WHILE SOMEONE IS PEEKING
        if (netState.Value == DoorState.Peek)
        {
            if (currentPlayer == null || !currentPlayer.IsOwner)
                return;
        }

        // ================= ALWAYS EXIT PEek =================
        if (Input.GetKeyUp(KeyCode.E))
        {
            holding = false;
            holdTimer = 0f;

            if (netState.Value == DoorState.Peek)
            {
                RequestCloseDoorServerRpc();
            }
        }

        // ================= ONLY START INTERACTION IF HIGHLIGHTED =================
        if (!isHighlighted)
            return;

        if (Input.GetKeyDown(KeyCode.E))
        {
            holding = true;
            holdTimer = 0f;
        }

        if (holding)
        {
            holdTimer += Time.deltaTime;

            if (holdTimer >= holdThreshold && netState.Value == DoorState.Closed)
            {
                RequestStartPeekServerRpc();
            }
        }

        if (Input.GetKeyUp(KeyCode.E))
        {
            holding = false;

            if (netState.Value != DoorState.Peek && holdTimer < holdThreshold)
            {
                RequestToggleDoorServerRpc();
            }

            holdTimer = 0f;
        }

        if (isTransitioning)
            return;
    }

    // ================= ROTATIONS =================

    private void UpdateRotationsFromState()
    {
        float angle = openAngle * netDirection.Value;

        openRotation = closedRotation * Quaternion.Euler(0f, angle, 0f);
        peekRotation = closedRotation * Quaternion.Euler(0f, angle * peekAngleMultiplier, 0f);
    }

    private void HandleRotation()
    {
        Quaternion target = netState.Value switch
        {
            DoorState.Open => openRotation,
            DoorState.Peek => peekRotation,
            _ => closedRotation
        };

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            target,
            Time.deltaTime * openSpeed
        );
    }

    // ================= FRONT / BACK =================

    public void SetFromCollider(Collider hitCollider)
    {
        float dir = (hitCollider == frontCollider) ? 1f : -1f;
        RequestSetDirectionServerRpc(dir);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestSetDirectionServerRpc(float dir)
    {
        if (peekOwnerClientId.Value != ulong.MaxValue)
            return;

        if (netState.Value != DoorState.Closed)
            return; // ❗ lock direction while open/peek

        netDirection.Value = dir;
    }

    // ================= SERVER ACTIONS =================

    [ServerRpc(RequireOwnership = false)]
    private void RequestToggleDoorServerRpc()
    {
        if (peekOwnerClientId.Value != ulong.MaxValue)
            return;

        if (isTransitioning)
            return;

        isTransitioning = true;

        netState.Value =
            (netState.Value == DoorState.Open)
                ? DoorState.Closed
                : DoorState.Open;

        StartCoroutine(UnlockAfterDelay());
    }

    private IEnumerator UnlockAfterDelay()
    {
        yield return new WaitForSeconds(0.6f); // match openSpeed gevoel

        isTransitioning = false;
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestStartPeekServerRpc(ServerRpcParams rpcParams = default)
    {
        if (netState.Value != DoorState.Closed)
            return;

        // ❌ ALREADY IN USE
        if (peekOwnerClientId.Value != ulong.MaxValue)
            return;

        peekOwnerClientId.Value = rpcParams.Receive.SenderClientId;
        netState.Value = DoorState.Peek;

        if (currentPlayer != null)
            currentPlayer.SetFrozen(true);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestCloseDoorServerRpc()
    {
        if (netState.Value != DoorState.Peek)
        {
            netState.Value = DoorState.Closed;
            return;
        }

        netState.Value = DoorState.Closed;
        peekOwnerClientId.Value = ulong.MaxValue;

        if (currentPlayer != null)
        {
            StartCoroutine(ForceExitPeek());
        }
    }

    private IEnumerator ForceExitPeek()
    {
        PlayerCubeController player = currentPlayer;

        if (player == null)
            yield break;

        Transform target = (netDirection.Value > 0f) ? front2 : back2;

        if (target == null)
            yield break;

        player.SetFrozen(true);

        Vector3 velocity = Vector3.zero;

        while (player != null)
        {
            float dist = Vector3.Distance(player.transform.position, target.position);

            if (dist < 0.05f)
                break;

            player.transform.position = Vector3.SmoothDamp(
                player.transform.position,
                target.position,
                ref velocity,
                0.3f
            );

            yield return null;
        }

        if (player != null)
            player.transform.position = target.position;

        if (player != null)
            player.SetFrozen(false);
    }

    // ================= PLAYER =================

    public void SetCurrentPlayer(PlayerCubeController player)
    {
        currentPlayer = player;
        currentCamera = Camera.main.GetComponent<CameraMovement>();
    }

    // ================= HIGHLIGHT =================

    public void SetHighlight(bool active)
    {
        isHighlighted = active;

        if (mat == null) return;

        if (active)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", highlightColor * intensity);
        }
        else
        {
            mat.DisableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", Color.black);
        }
    }

    public bool IsPeeking()
    {
        return netState.Value == DoorState.Peek;
    }
}