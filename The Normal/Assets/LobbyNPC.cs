using UnityEngine;
using Unity.Netcode;
using System.Collections;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class LobbyNPC : NetworkBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 2.5f;
    public float rotationSpeed = 6f;

    [Header("Desk Area")]
    public Transform areaCenter;
    public float areaSizeX = 3f;
    public float areaSizeZ = 3f;

    [Header("Player Target")]
    private Transform currentTargetPlayer;

    private Vector3 patrolTarget;
    private bool isResponding;

    private CharacterController controller;

    private Coroutine returnRoutine;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip bellResponseClip;

    [Header("Footsteps")]
    [SerializeField] private AudioSource footstepSource;
    [SerializeField] private AudioClip footstepClip;

    [SerializeField] private float stepInterval = 0.5f;

    private float stepTimer;
    private Vector3 lastPosition;

    public Transform suitcaseHand; // child object bij NPC (hand socket)

    private CapsuleCollider npcCollider;

    private SuitCase carriedSuitcase;
    private Transform currentDropTarget;
    private int currentDropSlot = -1;

    [Header("Suitcase Drop Slots")]
    public Transform[] suitcaseDropSlots;
    private bool[] slotOccupied;

    private bool isDroppingSuitcase;

    private bool hasDropTask;
    private float dropTime;
    private float minDropDelay = 3f;
    private float maxDropDelay = 7.5f;

    [Header("Counter")]
    public Transform counterTarget;
    private bool isDeliveringToCounter;

    private bool isFetchingFromSlot;
    private Transform fetchSlot;

    private bool isOnCounter;

    public NetworkVariable<bool> colliderEnabled = new NetworkVariable<bool>(
    false,
    NetworkVariableReadPermission.Everyone,
    NetworkVariableWritePermission.Server
);

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        npcCollider = GetComponent<CapsuleCollider>();

        if (npcCollider != null)
            npcCollider.enabled = false; // 🔥 default uit
    }

    private void SetNpcCollider(bool value)
    {
        if (!IsServer) return;

        colliderEnabled.Value = value;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            slotOccupied = new bool[suitcaseDropSlots.Length];
            lastPosition = transform.position;
            StartCoroutine(PatrolRoutine());
        }

        colliderEnabled.OnValueChanged += OnColliderChanged;
        OnColliderChanged(false, colliderEnabled.Value);
    }

    private void OnColliderChanged(bool oldValue, bool newValue)
    {
        if (npcCollider != null)
            npcCollider.enabled = newValue;
    }

    private int GetFreeSlotIndex()
    {
        for (int i = 0; i < suitcaseDropSlots.Length; i++)
        {
            if (!slotOccupied[i])
                return i;
        }
        return -1;
    }

    private void Update()
    {
        if (!IsServer) return;

        // =========================
        // 0. BEL PRIORITY (FIX)
        // =========================
        if (currentTargetPlayer != null)
        {
            MoveTo(currentTargetPlayer.position);

            float dist = Vector3.Distance(transform.position, currentTargetPlayer.position);

            // =========================
            // FETCH FROM DROPTARGET
            // =========================
            if (isFetchingFromSlot && dist < 0.4f)
            {
                SuitCase sc = fetchSlot != null ? fetchSlot.GetComponentInChildren<SuitCase>() : null;

                if (sc != null)
                {
                    sc.SetNPCHold(suitcaseHand);

                    NetworkObject netObj = sc.GetComponent<NetworkObject>();
                    if (netObj != null)
                        netObj.TrySetParent(suitcaseHand, false);

                    carriedSuitcase = sc;

                    isFetchingFromSlot = false;
                    fetchSlot = null;

                    // 🔥 NIEUW: direct naar counter sturen
                    isDeliveringToCounter = true;
                    currentTargetPlayer = counterTarget;
                    isResponding = true;
                }
            }

            HandleFootsteps();
            return;
        }

        // =========================
        // 1. DROP PRIORITY
        // =========================
        if (carriedSuitcase != null && currentDropTarget != null)
        {
            MoveTo(currentDropTarget.position);

            float dist = Vector3.Distance(transform.position, currentDropTarget.position);

            if (dist < 0.3f && !isDroppingSuitcase)
            {
                StartCoroutine(DropSuitcaseRoutine());
            }

            HandleFootsteps();
            return;
        }

        // =========================
        // 2. COUNTER DROP PRIORITY
        // =========================
        if (isDeliveringToCounter && carriedSuitcase != null)
        {
            MoveTo(counterTarget.position);

            float dist = Vector3.Distance(transform.position, counterTarget.position);

            if (dist < 2f && !isDroppingSuitcase)
            {
                StartCoroutine(DropAtCounterRoutine());
            }

            HandleFootsteps();
            return;
        }

        // =========================
        // 3. PATROL
        // =========================
        MoveTo(patrolTarget);
        HandleFootsteps();
    }

    private IEnumerator DropSuitcaseRoutine()
    {
        if (carriedSuitcase == null)
            yield break;

        if (isDroppingSuitcase)
            yield break;

        isDroppingSuitcase = true;

        isResponding = false;

        // reserveer slot
        slotOccupied[currentDropSlot] = true;

        Transform slot = suitcaseDropSlots[currentDropSlot];
        NetworkObject suitcaseNet =
            carriedSuitcase.GetComponent<NetworkObject>();

        // NPC laat suitcase los
        carriedSuitcase.ReleaseFromNPC();

        // physics uit zodat de lerp niet verstoord wordt
        Rigidbody rb = carriedSuitcase.GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        carriedSuitcase.ForceDisableCollider();

        Vector3 startPos = carriedSuitcase.transform.position;
        Quaternion startRot = carriedSuitcase.transform.rotation;

        float t = 0f;
        float duration = 0.35f;

        while (t < duration)
        {
            t += Time.deltaTime;

            float n = Mathf.SmoothStep(0f, 1f, t / duration);

            carriedSuitcase.transform.position =
                Vector3.Lerp(startPos, slot.position, n);

            carriedSuitcase.transform.rotation =
                Quaternion.Slerp(startRot, slot.rotation, n);

            yield return null;
        }

        // SNAP EXACT CENTER
        carriedSuitcase.transform.SetParent(slot, true);

        // force LOCAL ZERO (dit is jouw fix)
        carriedSuitcase.transform.localPosition = Vector3.zero;
        carriedSuitcase.transform.localRotation = Quaternion.identity;

        // nu parenten zodat hij netjes blijft liggen
        if (suitcaseNet != null)
            suitcaseNet.TrySetParent(slot, false);

        // reset NPC state
        carriedSuitcase = null;
        currentDropTarget = null;
        currentDropSlot = -1;

        // 🔥 BELANGRIJK: drop moet BEL state niet blokkeren
        isDroppingSuitcase = false;
        hasDropTask = false;

        // reset respond state veilig
        isResponding = false;
    }

    private void ResetDropState()
    {
        carriedSuitcase = null;
        currentDropTarget = null;
        currentDropSlot = -1;
        isDroppingSuitcase = false;
        hasDropTask = false;
    }

    // =========================
    // PATROL LOGIC
    // =========================
    private IEnumerator PatrolRoutine()
    {
        while (true)
        {
            if (!isResponding && currentTargetPlayer == null)
            {
                patrolTarget = GetRandomPointInArea();
            }

            yield return new WaitForSeconds(Random.Range(2f, 4f));
        }
    }

    private Vector3 GetRandomPointInArea()
    {
        Vector3 center = areaCenter.position;

        float x = Random.Range(-areaSizeX, areaSizeX);
        float z = Random.Range(-areaSizeZ, areaSizeZ);

        return new Vector3(
            center.x + x,
            center.y,
            center.z + z
        );
    }

    // =========================
    // MOVEMENT
    // =========================
    private void MoveTo(Vector3 target)
    {
        Vector3 dir = (target - transform.position);
        dir.y = 0f;

        float distance = dir.magnitude;

        if (distance < 0.2f) return;

        dir.Normalize();

        // rotate
        Quaternion look = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            look,
            rotationSpeed * Time.deltaTime
        );

        // move
        controller.Move(dir * moveSpeed * Time.deltaTime);
    }

    // =========================
    // CALLED FROM BELL
    // =========================
    public void GoToPlayer(Transform player)
    {
        if (!IsServer) return;

        // ❌ NIET reageren als NPC in drop state zit
        if (carriedSuitcase != null || isDroppingSuitcase || hasDropTask)
        {
            currentTargetPlayer = null;
            isResponding = false;
            SetNpcCollider(false);
            return;
        }

        // =========================
        // 🔥 HARD INTERRUPT ALL STATES
        // =========================
        carriedSuitcase = null;
        currentDropTarget = null;
        currentDropSlot = -1;
        hasDropTask = false;
        isDroppingSuitcase = false;

        isOnCounter = true; // 🔥 ADD THIS

        StopAllCoroutines(); // BELANGRIJK: voorkomt race met drop planner
        StartCoroutine(PatrolRoutine()); // optioneel: reset flow stabiel

        // =========================
        // BEL STATE
        // =========================
        currentTargetPlayer = player;
        isResponding = true;

        SetNpcCollider(true);

        PlayBellResponseSoundClientRpc();

        if (returnRoutine != null)
            StopCoroutine(returnRoutine);

        returnRoutine = StartCoroutine(ReturnAfterDelay());

        ResetDropState();
    }

    private IEnumerator ReturnAfterDelay()
    {
        yield return new WaitForSeconds(5f);

        currentTargetPlayer = null;
        isResponding = false;

        SetNpcCollider(false); // 🔥 UIT zodra hij stopt met targeten

        returnRoutine = null;
    }

    public void ReturnToDesk()
    {
        if (!IsServer) return;

        currentTargetPlayer = null;
        isResponding = false;
    }

    private void OnDrawGizmos()
    {
        if (areaCenter == null) return;

        Gizmos.color = Color.green;

        Vector3 c = areaCenter.position;

        Vector3 topLeft = c + new Vector3(-areaSizeX, 0, areaSizeZ);
        Vector3 topRight = c + new Vector3(areaSizeX, 0, areaSizeZ);
        Vector3 bottomLeft = c + new Vector3(-areaSizeX, 0, -areaSizeZ);
        Vector3 bottomRight = c + new Vector3(areaSizeX, 0, -areaSizeZ);

        Gizmos.DrawLine(topLeft, topRight);
        Gizmos.DrawLine(topRight, bottomRight);
        Gizmos.DrawLine(bottomRight, bottomLeft);
        Gizmos.DrawLine(bottomLeft, topLeft);

        Gizmos.DrawSphere(c, 0.1f);
    }

    [ClientRpc]
    private void PlayBellResponseSoundClientRpc()
    {
        if (audioSource != null && bellResponseClip != null)
        {
            audioSource.PlayOneShot(bellResponseClip);
        }
    }

    private void HandleFootsteps()
    {
        if (!IsServer) return;

        float moved = Vector3.Distance(transform.position, lastPosition);

        bool isMoving = moved > 0.01f;

        lastPosition = transform.position;

        if (!isMoving)
        {
            stepTimer = 0f;
            return;
        }

        stepTimer += Time.deltaTime;

        if (stepTimer >= stepInterval)
        {
            stepTimer = 0f;
            PlayFootstepClientRpc();
        }
    }

    [ClientRpc]
    private void PlayFootstepClientRpc()
    {
        if (footstepSource == null || footstepClip == null)
            return;

        footstepSource.PlayOneShot(footstepClip);
    }

    [ServerRpc(RequireOwnership = false)]
    public void TakeSuitcaseServerRpc(NetworkObjectReference suitcaseRef)
    {
        if (!suitcaseRef.TryGet(out NetworkObject netObj))
            return;

        SuitCase suitCase = netObj.GetComponent<SuitCase>();
        if (suitCase == null)
            return;

        // 🔥 stop direct met naar speler lopen
        currentTargetPlayer = null;
        isResponding = false;

        if (returnRoutine != null)
        {
            StopCoroutine(returnRoutine);
            returnRoutine = null;
        }

        SetNpcCollider(false);

        // suitcase oppakken
        suitCase.SetNPCHold(suitcaseHand);

        netObj.TrySetParent(suitcaseHand, false);

        carriedSuitcase = suitCase;

        // BELANGRIJK: eerst rondlopen
        PlanDropTask(suitCase);
    }

    private void PlanDropTask(SuitCase suitcase)
    {
        if (!IsServer) return;

        hasDropTask = true;

        float delay = Random.Range(minDropDelay, maxDropDelay);
        dropTime = Time.time + delay;

        StartCoroutine(DropPlannerRoutine());
    }

    private IEnumerator DropPlannerRoutine()
    {
        while (hasDropTask)
        {
            // nog niet tijd om te droppen → NPC blijft gewoon patrouilleren
            if (Time.time < dropTime)
            {
                yield return null;
                continue;
            }

            // kies drop slot pas op het moment zelf
            int slotIndex = GetFreeSlotIndex();

            if (slotIndex != -1 && carriedSuitcase != null)
            {
                currentDropSlot = slotIndex;
                currentDropTarget = suitcaseDropSlots[slotIndex];

                isResponding = true; // mag naar drop gaan
            }

            hasDropTask = false;
            yield break;
        }
    }

    public void TryDropSuitcase(SuitCase suitcase)
    {
        if (!IsServer) return;

        int slotIndex = GetFreeSlotIndex();
        if (slotIndex == -1) return;

        carriedSuitcase = suitcase;
        currentDropSlot = slotIndex;
        currentDropTarget = suitcaseDropSlots[slotIndex];

        isResponding = true;
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestScanDropTargetsServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;

        if (carriedSuitcase != null) return; // NPC mag niet dubbel iets doen

        // alleen als NPC niet al bezig is
        if (isDroppingSuitcase || isFetchingFromSlot) return;

        ScanDropTargetsForPlayer();
    }

    private void ScanDropTargetsForPlayer()
    {
        for (int i = 0; i < suitcaseDropSlots.Length; i++)
        {
            Transform slot = suitcaseDropSlots[i];

            if (slot.childCount > 0 && slot != counterTarget)
            {
                SuitCase sc = slot.GetComponentInChildren<SuitCase>();

                if (sc != null && sc.transform.parent != counterTarget)
                {
                    isFetchingFromSlot = true;
                    fetchSlot = slot;

                    currentTargetPlayer = slot; // 👈 NPC loopt naar drop slot
                    isResponding = true;

                    return;
                }
            }
        }
    }

    private IEnumerator DropAtCounterRoutine()
    {
        if (carriedSuitcase == null)
            yield break;

        isDroppingSuitcase = true;

        Vector3 startPos = carriedSuitcase.transform.position;
        Quaternion startRot = carriedSuitcase.transform.rotation;

        float t = 0f;
        float duration = 0.4f;

        while (t < duration)
        {
            t += Time.deltaTime;

            float n = Mathf.SmoothStep(0f, 1f, t / duration);

            carriedSuitcase.transform.position =
                Vector3.Lerp(startPos, counterTarget.position, n);

            carriedSuitcase.transform.rotation =
                Quaternion.Slerp(startRot, counterTarget.rotation, n);

            yield return null;
        }

        carriedSuitcase.transform.SetParent(counterTarget, true);
        carriedSuitcase.transform.localPosition = Vector3.zero;
        carriedSuitcase.transform.localRotation = Quaternion.identity;

        NetworkObject netObj = carriedSuitcase.GetComponent<NetworkObject>();
        if (netObj != null)
            netObj.TrySetParent(counterTarget, false);

        carriedSuitcase = null;
        isDeliveringToCounter = false;
        isDroppingSuitcase = false;
    }
}