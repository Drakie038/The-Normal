using UnityEngine;
using Unity.Netcode;
using System.Collections;

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

    private BordenHouder houder;

    public enum DienBladType
    {
        Bord,
        DienBlad
    }

    public DienBladType type;

    void Awake()
    {
        rend = GetComponentInChildren<Renderer>();
        col = GetComponent<Collider>();
        rb = GetComponent<Rigidbody>();

        if (rend != null)
            mat = rend.material;

        houder = GetComponentInParent<BordenHouder>();
    }

    public void PickUp(Transform cam)
    {
        if (!IsOwner)
        {
            PickUpServerRpc(NetworkManager.Singleton.LocalClientId);
            return;
        }

        DoPickUp(cam);
    }

    [ServerRpc(RequireOwnership = false)]
    private void PickUpServerRpc(ulong clientId)
    {
        PickUpClientRpc(clientId);
    }

    [ClientRpc]
    private void PickUpClientRpc(ulong clientId)
    {
        if (NetworkManager.Singleton.LocalClientId != clientId)
            return;

        DoPickUp(Camera.main.transform);
    }

    private void DoPickUp(Transform cam)
    {
        if (isHeld || cam == null)
            return;

        if (houder != null && houder.GetTopPlate() != this)
            return;

        isHeld = true;
        camTarget = cam;

        if (houder != null)
            houder.RemovePlate(this);

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

    public void PlaceToSlot(Transform slot)
    {
        if (!isHeld || slot == null)
            return;

        PlaceToSlotServerRpc(slot.position, slot.rotation);
    }

    [ServerRpc(RequireOwnership = false)]
    private void PlaceToSlotServerRpc(Vector3 pos, Quaternion rot)
    {
        PlaceToSlotClientRpc(pos, rot);
    }

    [ClientRpc]
    private void PlaceToSlotClientRpc(Vector3 pos, Quaternion rot)
    {
        GameObject temp = new GameObject("TempSlot");
        temp.transform.position = pos;
        temp.transform.rotation = rot;

        StartCoroutine(FlyToSlot(temp.transform));
    }

    private IEnumerator FlyToSlot(Transform slot)
    {
        isHeld = false;

        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;

        float t = 0f;
        float duration = 0.4f;

        Rigidbody rb = GetComponent<Rigidbody>();
        Collider col = GetComponent<Collider>();

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (col != null)
            col.enabled = false;

        // 🎯 bepaal target rotatie
        Quaternion targetRot = slot.rotation;

        if (type == DienBladType.DienBlad)
        {
            targetRot *= Quaternion.Euler(90f, 0f, 0f);
        }

        while (t < duration)
        {
            t += Time.deltaTime;
            float n = Mathf.SmoothStep(0, 1, t / duration);

            transform.position = Vector3.Lerp(startPos, slot.position, n);
            transform.rotation = Quaternion.Slerp(startRot, targetRot, n);

            yield return null;
        }

        transform.position = slot.position;
        transform.rotation = targetRot;

        transform.SetParent(slot);
    }

    public IEnumerator FlyIntoTrash(Prullenbak trash)
    {
        isHeld = false;

        trash.OpenLid();

        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;

        Vector3 topPos = trash.trashTarget.position + Vector3.up * 1.2f; // boven de bin
        Vector3 endPos = trash.trashTarget.position;

        float t = 0f;
        float duration = 0.5f;

        Rigidbody rb = GetComponent<Rigidbody>();
        Collider col = GetComponent<Collider>();

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (col != null)
            col.enabled = false;

        while (t < duration)
        {
            t += Time.deltaTime;
            float n = Mathf.SmoothStep(0, 1, t / duration);

            // 🎯 curve: eerst boven de bak, dan naar beneden erin
            Vector3 pos = Vector3.Lerp(startPos, topPos, n);

            if (n > 0.5f)
            {
                float downT = (n - 0.5f) * 2f;
                pos = Vector3.Lerp(topPos, endPos, downT);
            }

            transform.position = pos;
            transform.rotation = Quaternion.Slerp(startRot, trash.lid.rotation, n);

            yield return null;
        }

        transform.position = endPos;

        yield return new WaitForSeconds(0.1f);

        trash.CloseLid();

        if (IsServer)
        {
            GetComponent<NetworkObject>().Despawn(true);
        }
        else
        {
            TrashServerRpc();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void TrashServerRpc()
    {
        GetComponent<NetworkObject>().Despawn(true);
    }
}