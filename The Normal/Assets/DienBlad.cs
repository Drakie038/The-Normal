using UnityEngine;
using System.Collections;
using Unity.Netcode;

public class DienBlad : NetworkBehaviour
{
    [Header("Highlight")]
    public Color highlightColor = Color.white;
    [Range(0f, 2f)] public float intensity = 0.05f;

    [Header("Hold In Front Of Camera")]
    public float distanceInFront = 1.2f;
    public float heightOffset = -0.2f;

    [Header("Pickup Animation")]
    public float flySpeed = 6f;

    private Renderer rend;
    private Material mat;
    private Collider col;
    private Rigidbody rb;

    private bool isPickedUp;
    private bool isHeldActive;
    private Transform camTarget;

    private bool isFlyingToHand;
    private float flyT;
    private Vector3 flyStartPos;
    private Quaternion flyStartRot;

    private float pickupCooldownEnd;

    public bool IsHeld => isPickedUp;


    private bool isPlacing;

    public bool IsPlacing => isPlacing;

    public NetworkVariable<bool> colliderEnabled = new NetworkVariable<bool>(
    true,
    NetworkVariableReadPermission.Everyone,
    NetworkVariableWritePermission.Server
);

    void Awake()
    {
        rend = GetComponentInChildren<Renderer>();
        col = GetComponent<Collider>();
        rb = GetComponent<Rigidbody>();

        if (rend != null)
            mat = rend.material;

        netObj = GetComponent<NetworkObject>();
    }

    void Start()
    {
        EnablePhysics();

        colliderEnabled.OnValueChanged += OnColliderChanged;

        OnColliderChanged(colliderEnabled.Value, colliderEnabled.Value);
    }

    private void OnColliderChanged(bool oldValue, bool newValue)
    {
        if (col != null)
            col.enabled = newValue;
    }

    private void EnablePhysics()
    {
        rb.isKinematic = false;
        rb.useGravity = true;
    }

    public void PickUp(Transform cameraTransform)
    {
        if (isHeldActive || cameraTransform == null)
            return;

        if (Time.time < pickupCooldownEnd)
            return;

        camTarget = cameraTransform;
        isHeldActive = true;

        if (rb != null) rb.isKinematic = true;

        isFlyingToHand = true;
        flyT = 0f;

        flyStartPos = transform.position;
        flyStartRot = transform.rotation;

        SetPickupStateServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetPickupStateServerRpc()
    {
        colliderEnabled.Value = false;
    }

    void LateUpdate()
    {
        if (!isHeldActive || camTarget == null)
            return;

        Vector3 targetPos =
            camTarget.position +
            camTarget.forward * distanceInFront +
            camTarget.up * heightOffset;

        if (isFlyingToHand)
        {
            flyT += Time.deltaTime * flySpeed;

            float curve = Mathf.SmoothStep(0f, 1f, flyT);

            transform.position = Vector3.Lerp(flyStartPos, targetPos, curve);
            transform.rotation = Quaternion.Slerp(flyStartRot, camTarget.rotation, curve);

            if (flyT >= 1f)
                isFlyingToHand = false;

            return;
        }

        transform.position = Vector3.Lerp(transform.position, targetPos, 12f * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, camTarget.rotation, 12f * Time.deltaTime);
    }

    private IEnumerator SmoothPlace(Transform slot)
    {
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;

        float t = 0f;
        float duration = 0.5f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float n = t / duration;

            transform.position = Vector3.Lerp(startPos, slot.position, n);
            transform.rotation = Quaternion.Slerp(startRot, slot.rotation, n);

            yield return null;
        }

        // 🔥 BELANGRIJK: release anim lock
        isPlacing = false;
    }

    public void Drop()
    {
        DropServerRpc();
    }



    [ServerRpc(RequireOwnership = false)]
    public void DropServerRpc()
    {
        colliderEnabled.Value = true;

        if (transform.parent != null)
        {
            transform.SetParent(null);
        }

        NetworkObject.ChangeOwnership(NetworkManager.ServerClientId);

        DropClientRpc(); // 🔥 sync naar iedereen
    }

    public float GetPickupCooldown()
    {
        return pickupCooldownEnd;
    }

    public void SetHighlight(bool active)
    {
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

    [ServerRpc(RequireOwnership = false)]
    public void RequestPickupServerRpc()
    {
        ApplyPickup();
    }

    private void ApplyPickup()
    {
        if (isHeldActive)
            return;

        isHeldActive = true;
        isPickedUp = true;

        if (col != null) col.enabled = false;
        if (rb != null) rb.isKinematic = true;

        if (transform.parent != null)
            transform.SetParent(null);
    }

    public NetworkObject netObj;

    [ServerRpc(RequireOwnership = false)]
    public void PickupServerRpc(ulong clientId)
    {
        if (!netObj.IsSpawned)
            return;

        netObj.ChangeOwnership(clientId);

        PickupClientRpc(clientId);
    }

    [ClientRpc]
    private void PickupClientRpc(ulong clientId)
    {
        if (NetworkManager.Singleton.LocalClientId != clientId)
            return;

        CameraMovement cam = FindObjectOfType<CameraMovement>();

        if (cam != null)
        {
            PickUp(cam.transform);
        }
    }

    [ClientRpc]
    private void DropClientRpc()
    {
        isHeldActive = false;
        isPickedUp = false;
        isFlyingToHand = false;

        camTarget = null;

        pickupCooldownEnd = Time.time + 0.25f;

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }

        // 🔥 BELANGRIJK: collider altijd terug aan bij drop
        ForceEnableCollider();

        SetHighlight(false);
    }

    public void ForceEnableCollider()
    {
        if (col != null)
            col.enabled = true;

        // server sync (belangrijk voor netcode state consistency)
        colliderEnabled.Value = true;
    }
}