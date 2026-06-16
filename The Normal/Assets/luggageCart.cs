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

        for (int i = 0; i < luggageItems.Count; i++)
        {
            if (occupied[i] != null)
                continue;

            Transform slot = luggageItems[i];

            occupied[i] = suitCase;

            // 🔥 kleur activeren
            SetColorBool(suitCase.color, true);

            suitCase.PlaceOnLuggage(slot, this, i);

            return true;
        }

        return false;
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
}