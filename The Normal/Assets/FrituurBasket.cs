using UnityEngine;
using Unity.Netcode;

public class FrituurBasket : NetworkBehaviour
{
    [Header("Highlight Settings")]
    public Color highlightColor = Color.yellow;
    [Range(0f, 2f)] public float intensity = 0.05f;

    [Header("Lift Settings")]
    public float liftAmount = 1.2f;
    public float liftSpeed = 5f;

    [Header("Fries Physics Objects")]
    public Rigidbody[] fries;   // 👈 HIER sleep je alle frietjes in

    public NetworkVariable<bool> up = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private Vector3 startPos;
    private Vector3 targetPos;

    private Renderer[] renderers;
    private Material[] mats;

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
        mats = new Material[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            mats[i] = renderers[i].material;
        }
    }

    public override void OnNetworkSpawn()
    {
        startPos = transform.position;
        targetPos = startPos + Vector3.up * liftAmount;

        EnableFriesPhysics(); // 👈 BELANGRIJK
    }

    private void EnableFriesPhysics()
    {
        if (fries == null) return;

        foreach (Rigidbody rb in fries)
        {
            if (rb == null) continue;

            rb.isKinematic = false;   // laten vallen
            rb.useGravity = true;     // gravity aan
            rb.WakeUp();              // direct activeren
        }
    }

    private void Update()
    {
        Vector3 target = up.Value ? targetPos : startPos;
        transform.position = Vector3.Lerp(transform.position, target, Time.deltaTime * liftSpeed);
    }

    public void Toggle()
    {
        ToggleServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void ToggleServerRpc(ServerRpcParams rpcParams = default)
    {
        up.Value = !up.Value;
    }

    public void SetHighlight(bool active)
    {
        SetHighlight(active, highlightColor, intensity);
    }

    public void SetHighlight(bool active, Color color, float strength)
    {
        if (mats == null) return;

        for (int i = 0; i < mats.Length; i++)
        {
            if (mats[i] == null) continue;

            if (active)
            {
                mats[i].EnableKeyword("_EMISSION");
                mats[i].SetColor("_EmissionColor", color * strength);
            }
            else
            {
                mats[i].DisableKeyword("_EMISSION");
                mats[i].SetColor("_EmissionColor", Color.black);
            }
        }
    }
}