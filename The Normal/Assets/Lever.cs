using UnityEngine;
using Unity.Netcode;

public class Lever : NetworkBehaviour
{
    [Header("Lever Rotation")]
    public float openAngleX = -90f;
    public float smoothSpeed = 6f;

    [Header("Highlight")]
    public Color highlightColor = Color.yellow;
    [Range(0f, 2f)] public float intensity = 0.3f;

    private Renderer rend;
    private Material mat;

    private bool isHighlighted;

    private NetworkVariable<bool> isUsed =
        new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private Quaternion startLocalRotation;
    private Quaternion targetLocalRotation;

    void Awake()
    {
        rend = GetComponentInChildren<Renderer>();
        if (rend != null)
            mat = rend.material;

        startLocalRotation = transform.localRotation;

        // 🔥 FIX: absolute correct -90 X vanaf start
        targetLocalRotation = Quaternion.Euler(openAngleX, startLocalRotation.eulerAngles.y, startLocalRotation.eulerAngles.z);
    }

    void Update()
    {
        HandleRotation();
    }

    // ================= ACTIVATION =================

    public void TryActivate()
    {
        if (isUsed.Value)
            return;

        RequestActivateServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestActivateServerRpc()
    {
        if (isUsed.Value)
            return;

        isUsed.Value = true;
    }

    // ================= ROTATION =================

    private void HandleRotation()
    {
        Quaternion target = isUsed.Value ? targetLocalRotation : startLocalRotation;

        // 🔥 FIX: snap exact when close enough
        transform.localRotation = Quaternion.Slerp(
            transform.localRotation,
            target,
            Time.deltaTime * smoothSpeed
        );

        // HARD SNAP zodat hij NIET eindigt op bijna -90 maar exact -90
        if (isUsed.Value && Quaternion.Angle(transform.localRotation, targetLocalRotation) < 0.5f)
        {
            transform.localRotation = targetLocalRotation;
        }
    }

    // ================= HIGHLIGHT =================

    public void SetHighlight(bool active)
    {
        if (isUsed.Value)
            return;

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
}