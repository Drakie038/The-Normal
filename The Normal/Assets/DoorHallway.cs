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

    [Header("Exit Targets (after peek)")]
    public Transform front2;
    public Transform back2;

    private DoorState prevState;

    private void HandlePlayerPeekMovement()
    {
        if (currentPlayer == null)
            return;

        if (netState.Value != DoorState.Peek)
            return;

        Transform target = (netDirection.Value > 0f) ? frontPoint : backPoint;

        if (target == null)
            return;

        currentPlayer.transform.position = Vector3.MoveTowards(
            currentPlayer.transform.position,
            target.position,
            openSpeed * Time.deltaTime
        );
    }

    // ================= NETWORK STATE =================

    private enum DoorState : byte
    {
        Closed,
        Open,
        Peek
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
        HandleCameraLean();

        HandlePlayerPeekMovement();

        if (prevState == DoorState.Peek && netState.Value == DoorState.Closed)
        {
            StartCoroutine(MovePlayerToExitPoint());
        }

        prevState = netState.Value;
    }

    // ================= INPUT =================

    private void HandleInput()
    {
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

            if (holdTimer >= holdThreshold && netState.Value != DoorState.Peek)
            {
                RequestStartPeekServerRpc();
            }
        }

        if (Input.GetKeyUp(KeyCode.E))
        {
            holding = false;

            if (netState.Value == DoorState.Peek)
            {
                RequestCloseDoorServerRpc();
                return;
            }

            if (holdTimer < holdThreshold)
            {
                RequestToggleDoorServerRpc();
            }

            holdTimer = 0f;
        }
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
        netDirection.Value = dir;
    }

    // ================= SERVER ACTIONS =================

    [ServerRpc(RequireOwnership = false)]
    private void RequestToggleDoorServerRpc()
    {
        netState.Value =
            (netState.Value == DoorState.Open)
                ? DoorState.Closed
                : DoorState.Open;
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestStartPeekServerRpc()
    {
        netState.Value = DoorState.Peek;

        if (currentPlayer != null)
            currentPlayer.SetFrozen(true);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestCloseDoorServerRpc()
    {
        netState.Value = DoorState.Closed;

        if (currentPlayer != null)
            currentPlayer.SetFrozen(false);

        if (currentCamera != null)
            currentCamera.SetDoorLeanTarget(0f);
    }

    // ================= CAMERA =================

    private void HandleCameraLean()
    {
        if (currentCamera == null)
            return;

        float lean = (netState.Value == DoorState.Peek)
            ? cameraLeanAngle * netDirection.Value
            : 0f;

        currentCamera.SetDoorLean(lean);
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

    private IEnumerator MovePlayerToExitPoint()
    {
        if (currentPlayer == null)
            yield break;

        Transform target =
            (netDirection.Value > 0f) ? front2 : back2;

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

        currentPlayer.SetFrozen(false);
    }
}