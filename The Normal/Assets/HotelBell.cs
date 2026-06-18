using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class HotelBell : NetworkBehaviour
{
    [Header("Audio")]
    public AudioClip bellClip;
    public AudioSource audioSource;

    [Header("Highlight")]
    public Color highlightColor = Color.white;
    [Range(0f, 2f)] public float intensity = 0.05f;

    private Renderer rend;
    private Material mat;

    private bool isPlaying;
    private bool isHighlighted;

    private void Awake()
    {
        rend = GetComponentInChildren<Renderer>();

        if (rend != null)
            mat = rend.material;

        // start safe state
        SetHighlight(false);
    }

    // =========================
    // HIGHLIGHT (Suitcase style)
    // =========================
    public void SetHighlight(bool value)
    {
        if (mat == null) return;

        isHighlighted = value;

        if (value && !isPlaying)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", highlightColor * intensity);
        }
        else
        {
            mat.SetColor("_EmissionColor", Color.black);
            mat.DisableKeyword("_EMISSION");
        }
    }

    // =========================
    // INTERACT ENTRY (from Camera)
    // =========================
    public void TryRing(PlayerCubeController player)
    {
        if (isPlaying) return;

        RingServerRpc();
    }

    // =========================
    // SERVER AUTHORITY
    // =========================
    [ServerRpc(RequireOwnership = false)]
    private void RingServerRpc(ServerRpcParams rpcParams = default)
    {
        if (isPlaying) return;

        PlayBellClientRpc();
    }

    // =========================
    // ALL CLIENTS PLAY SOUND
    // =========================
    [ClientRpc]
    private void PlayBellClientRpc()
    {
        StartCoroutine(BellRoutine());

        // NPC trigger (server side request)
        NotifyNPCServerRpc();
    }

    // =========================
    // MAIN LOGIC
    // =========================
    private IEnumerator BellRoutine()
    {
        isPlaying = true;

        // turn OFF highlight while playing
        SetHighlight(false);

        if (audioSource != null && bellClip != null)
        {
            audioSource.PlayOneShot(bellClip);
            yield return new WaitForSeconds(bellClip.length);
        }

        isPlaying = false;

        // restore highlight ONLY if still looked at
        if (isHighlighted)
        {
            SetHighlight(true);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void NotifyNPCServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;

        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
            return;

        if (client.PlayerObject == null)
            return;

        LobbyNPC npc = FindFirstObjectByType<LobbyNPC>();

        if (npc == null)
            return;

        npc.GoToPlayer(client.PlayerObject.transform);
    }
}