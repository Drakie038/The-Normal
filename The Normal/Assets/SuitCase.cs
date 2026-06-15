using UnityEngine;

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
        // ✅ BELANGRIJK: altijd physics actief bij spawn
        EnablePhysics();
    }

    private void EnablePhysics()
    {
        if (rb == null) return;

        rb.isKinematic = false;
        rb.useGravity = true;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    public void SetHighlight(bool active)
    {
        if (mat == null)
            return;

        if (active)
        {
            mat.EnableKeyword("_EMISSION");

            float finalIntensity = intensity;

            if (isPickedUp)
                finalIntensity *= 4f;

            mat.SetColor("_EmissionColor", highlightColor * finalIntensity);
        }
        else
        {
            mat.DisableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", Color.black);
        }
    }

    public void PickUp(Transform cameraTransform)
    {
        if (isHeldActive || cameraTransform == null)
            return;

        if (Time.time < pickupCooldownEnd)
            return;

        camTarget = cameraTransform;
        isHeldActive = true;

        if (col != null)
            col.enabled = false;

        if (rb != null)
            rb.isKinematic = true;

        isFlyingToHand = true;
        flyT = 0f;

        flyStartPos = transform.position;
        flyStartRot = transform.rotation;
    }

    void LateUpdate()
    {
        // ❌ BELANGRIJK: alleen checken als je echt vastgehouden wordt
        if (!isHeldActive)
            return;

        if (camTarget == null)
            return;

        Vector3 targetPos =
            camTarget.position +
            camTarget.forward * distanceInFront +
            camTarget.up * heightOffset;

        Quaternion targetRot = camTarget.rotation;

        if (isFlyingToHand)
        {
            flyT += Time.deltaTime * flySpeed;

            float curve = Mathf.SmoothStep(0f, 1f, flyT);

            transform.position = Vector3.Lerp(flyStartPos, targetPos, curve);
            transform.rotation = Quaternion.Slerp(flyStartRot, targetRot, curve);

            if (flyT >= 1f)
            {
                isFlyingToHand = false;
                isPickedUp = true;
            }

            return;
        }

        float distance = Vector3.Distance(transform.position, targetPos);

        float minSpeed = 12f;
        float maxSpeed = 20f;

        float t = Mathf.InverseLerp(0.2f, 2.5f, distance);
        float speed = Mathf.Lerp(minSpeed, maxSpeed, t);

        transform.position = Vector3.Lerp(transform.position, targetPos, speed * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, speed * Time.deltaTime);
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

            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            rb.AddForce(Vector3.down * 2f, ForceMode.Impulse);
        }

        if (col != null)
            col.enabled = true;

        SetHighlight(false);
    }

    public float GetPickupCooldown()
    {
        return pickupCooldownEnd;
    }
}