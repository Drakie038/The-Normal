using UnityEngine;
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

    private bool isHeld;
    private bool isFlying;
    private Transform camTarget;

    private Vector3 flyStartPos;
    private Quaternion flyStartRot;
    private float flyT;

    void Awake()
    {
        rend = GetComponentInChildren<Renderer>();
        col = GetComponent<Collider>();
        rb = GetComponent<Rigidbody>();

        if (rend != null)
            mat = rend.material;
    }

    public void PickUp(Transform cam)
    {
        if (isHeld || cam == null)
            return;

        isHeld = true;
        camTarget = cam;

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (col != null)
            col.enabled = false;

        flyStartPos = transform.position;
        flyStartRot = transform.rotation;

        flyT = 0f;
        isFlying = true;
    }

    void LateUpdate()
    {
        if (!isHeld || camTarget == null)
            return;

        Vector3 targetPos =
            camTarget.position +
            camTarget.forward * distanceInFront +
            camTarget.up * heightOffset;

        if (isFlying)
        {
            flyT += Time.deltaTime * flySpeed;
            float t = Mathf.SmoothStep(0f, 1f, flyT);

            transform.position = Vector3.Lerp(flyStartPos, targetPos, t);
            transform.rotation = Quaternion.Slerp(flyStartRot, camTarget.rotation, t);

            if (flyT >= 1f)
                isFlying = false;

            return;
        }

        transform.position = Vector3.Lerp(transform.position, targetPos, 12f * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, camTarget.rotation, 12f * Time.deltaTime);
    }

    // =========================
    // HIGHLIGHT (FIXED)
    // =========================

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
}