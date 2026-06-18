using System.Collections;
using System.Collections.Generic;
using Unity.Multiplayer.PlayMode;
using Unity.Netcode;
using UnityEngine;

public class LuggageCart : NetworkBehaviour
{
    [Header("Luggage Colliders (CHILDREN)")]
    public Collider frontCollider;
    public Collider backCollider;

    [Header("Highlight")]
    public Color highlightColor = Color.yellow;
    [Range(0f, 2f)] public float intensity = 0.3f;

    [Header("Push Points")]
    public Transform pushFor;
    public Transform pushBack;

    [Header("Luggage Items (slots)")]
    public List<Transform> luggageItems = new List<Transform>();

    [Header("SuitCaseType")]
    public bool blue;
    public bool red;
    public bool black;
    public bool green;
    public bool yellow;
    public bool magenta;
    public bool teal;
    public bool purple;

    private SuitCase[] occupied;

    private Renderer rend;
    private Material mat;
    private Rigidbody rb;

    private PlayerCubeController currentPlayer;

    private void Awake()
    {
        rend = GetComponentInChildren<Renderer>();
        if (rend != null)
            mat = rend.material;

        rb = GetComponent<Rigidbody>();

        occupied = new SuitCase[luggageItems.Count];

        // alles resetten
        blue = false;
        red = false;
        black = false;
        green = false;
        yellow = false;
        magenta = false;
        teal = false;
        purple = false;
    }

    private void LateUpdate()
    {
        if (!IsServer) return; // 🔥 BELANGRIJK: alleen server stuurt posities

        for (int i = 0; i < luggageItems.Count; i++)
        {
            if (occupied[i] == null)
                continue;

            SuitCase s = occupied[i];
            if (s == null)
                continue;

            Transform slot = luggageItems[i];

            s.NetworkObject.TrySetParent(slot, false); // 🔥 NETCODE parenting sync
        }
    }

    private void SetColorBool(SuitCase.ColorSuit color, bool value)
    {
        switch (color)
        {
            case SuitCase.ColorSuit.red: red = value; break;
            case SuitCase.ColorSuit.blue: blue = value; break;
            case SuitCase.ColorSuit.black: black = value; break;
            case SuitCase.ColorSuit.green: green = value; break;
            case SuitCase.ColorSuit.magenta: magenta = value; break;
            case SuitCase.ColorSuit.teal: teal = value; break;
            case SuitCase.ColorSuit.yellow: yellow = value; break;
            case SuitCase.ColorSuit.purple: purple = value; break; // typo in enum
        }
    }

    public bool TryPlaceSuitcase(SuitCase suitCase)
    {
        if (suitCase == null) return false;

        TryPlaceSuitcaseServerRpc(suitCase.NetworkObject);

        return true;
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

    public void ClearSlot(int index)
    {
        if (index < 0 || index >= occupied.Length) return;

        SuitCase suitCase = occupied[index];

        if (suitCase != null)
        {
            suitCase.SetLuggageStateClientRpc(false, -1);
            // kleur uitzetten op server
            ClearColorBoolServerRpc(suitCase.color, false);
            SetSuitcaseColliderServerRpc(suitCase.NetworkObject, false);
        }

        occupied[index] = null;
    }

    // SAFE overload (camera gebruikt soms andere signature)
    public void SetFromCollider(Collider hitCollider)
    {
        SetFromCollider(hitCollider, null);
    }

    public void SetFromCollider(Collider hitCollider, PlayerCubeController player)
    {
        currentPlayer = player;

        if (hitCollider == frontCollider)
        {
            // front interact
        }
        else if (hitCollider == backCollider)
        {
            // back interact
        }
    }

    public void SetPushPhysics(bool active)
    {
        if (rb == null) return;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        rb.constraints = RigidbodyConstraints.FreezeRotation;
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetColorBoolServerRpc(SuitCase.ColorSuit color, bool value)
    {
        SetColorBool(color, value);
    }

    [ServerRpc(RequireOwnership = false)]
    public void ClearColorBoolServerRpc(SuitCase.ColorSuit color, bool value)
    {
        SetColorBool(color, value);
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetSuitcaseColliderServerRpc(NetworkObjectReference suitCaseRef, bool state)
    {
        if (suitCaseRef.TryGet(out NetworkObject netObj))
        {
            SuitCase suitCase = netObj.GetComponent<SuitCase>();
            if (suitCase != null)
            {
                suitCase.colliderEnabled.Value = state;
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void TryPlaceSuitcaseServerRpc(NetworkObjectReference suitCaseRef)
    {
        if (!suitCaseRef.TryGet(out NetworkObject netObj))
            return;

        SuitCase suitCase = netObj.GetComponent<SuitCase>();
        if (suitCase == null)
            return;

        for (int i = 0; i < luggageItems.Count; i++)
        {
            if (occupied[i] != null)
                continue;

            Transform slot = luggageItems[i];

            occupied[i] = suitCase;

            SetColorBoolServerRpc(suitCase.color, true);
            SetSuitcaseColliderServerRpc(suitCase.NetworkObject, true);

            // 🔥 SERVER triggert visuals op iedereen
            PlaceSuitcaseClientRpc(suitCase.NetworkObject, i);

            return;
        }
    }

    [ClientRpc]
    private void PlaceSuitcaseClientRpc(NetworkObjectReference suitCaseRef, int index)
    {
        if (!suitCaseRef.TryGet(out NetworkObject netObj))
            return;

        SuitCase suitCase = netObj.GetComponent<SuitCase>();
        if (suitCase == null)
            return;

        Transform slot = luggageItems[index];

        // 🔥 hier start weer jouw fly + placement
        suitCase.PlaceOnLuggage(slot, this, index);
        suitCase.SetLuggageStateClientRpc(true, index);
    }

    private bool TryPlaceSuitcaseInternal(SuitCase suitCase)
    {
        if (suitCase == null) return false;

        for (int i = 0; i < luggageItems.Count; i++)
        {
            if (occupied[i] != null)
                continue;

            Transform slot = luggageItems[i];

            occupied[i] = suitCase;

            SetColorBoolServerRpc(suitCase.color, true);
            SetSuitcaseColliderServerRpc(suitCase.NetworkObject, true);

            suitCase.PlaceOnLuggage(slot, this, i);
            suitCase.SetLuggageStateClientRpc(true, i);

            return true;
        }

        return false;
    }

    [ServerRpc(RequireOwnership = false)]
    public void ClearSlotServerRpc(int index)
    {
        ClearSlot(index);
    }
}