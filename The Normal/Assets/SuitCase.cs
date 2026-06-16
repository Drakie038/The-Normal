using UnityEngine;
using System.Collections;
using Unity.Netcode;

public class SuitCase : MonoBehaviour
{
    [Header("Highlight")]
    public Color highlightColor = Color.white;
    [Range(0f, 2f)] public float intensity = 0.05f;

    [Header("Hold In Front Of Camera")]
    public float distanceInFront = 1.2f;
    public float heightOffset = -0.2f;

    [Header("Pickup Animation")]
    public float flySpeed = 6f;

    public enum ColorSuit
    {
        red,
        blue,
        black
    }

    public ColorSuit color;

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

    // NEW
    private bool isPlacedOnLuggage;
    private LuggageCart currentCart;
    private int currentSlot;

    public bool IsHeld => isPickedUp;

    public bool IsOnLuggage => isPlacedOnLuggage;

    void Awake()
    {
        rend = GetComponentInChildren<Renderer>();
        col = GetComponent<Collider>();
        rb = GetComponent<Rigidbody>();

        if (rend != null)
            mat = rend.material;
    }

    void Start()
    {
        EnablePhysics();
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

        if (col != null) col.enabled = false;
        if (rb != null) rb.isKinematic = true;

        isFlyingToHand = true;
        flyT = 0f;

        flyStartPos = transform.position;
        flyStartRot = transform.rotation;

        // 🔥 IMPORTANT: detach from luggage properly
        if (currentCart != null)
        {
            currentCart.ClearSlot(currentSlot);
            currentCart = null;
        }

        if (transform.parent != null)
            transform.SetParent(null);

        isPlacedOnLuggage = false;
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

    // =========================
    // NEW: PLACE ON LUGGAGE
    // =========================
    public void PlaceOnLuggage(Transform slot, LuggageCart cart, int index)
    {
        StopAllCoroutines();

        isHeldActive = false;
        isPickedUp = false;
        camTarget = null;

        currentCart = cart;
        currentSlot = index;
        isPlacedOnLuggage = true;

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // 🔥 KEY FIX: make it follow luggage like a child
        transform.SetParent(slot);

        StartCoroutine(SmoothPlace(slot));

        if (col != null)
            col.enabled = true;
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

        transform.position = slot.position;
        transform.rotation = slot.rotation;
    }

    public void Drop()
    {
        isPickedUp = false;
        isFlyingToHand = false;
        isHeldActive = false;

        camTarget = null;

        pickupCooldownEnd = Time.time + 0.25f;

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }

        if (col != null)
            col.enabled = true;

        SetHighlight(false);
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

        isPlacedOnLuggage = false;
    }
}