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

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            lastPosition = transform.position;
            StartCoroutine(PatrolRoutine());
        }
    }

    private void Update()
    {
        if (!IsServer) return;

        if (currentTargetPlayer != null)
        {
            MoveTo(currentTargetPlayer.position);
            return;
        }

        MoveTo(patrolTarget);

        HandleFootsteps();
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

        currentTargetPlayer = player;
        isResponding = true;

        PlayBellResponseSoundClientRpc(); // 👈 HIER TOEVOEGEN

        // restart timer als hij al bezig is
        if (returnRoutine != null)
            StopCoroutine(returnRoutine);

        returnRoutine = StartCoroutine(ReturnAfterDelay());
    }

    private IEnumerator ReturnAfterDelay()
    {
        yield return new WaitForSeconds(5f);

        currentTargetPlayer = null;
        isResponding = false;

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

        // set NPC follow target
        suitCase.SetNPCHold(suitcaseHand);

        netObj.TrySetParent(suitcaseHand, false);
    }
}