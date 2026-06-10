using UnityEngine;

public class LuggageCart : MonoBehaviour
{
    [Header("Child Colliders")]
    public Collider frontCollider;
    public Collider backCollider;

    [Header("Highlight")]
    public Color highlightColor = Color.yellow;
    [Range(0f, 2f)] public float intensity = 0.4f;

    private Renderer rend;
    private Material mat;

    private bool isHighlighted;

    public Transform pushFor;
    public Transform pushBack;

    public enum LuggageSide
    {
        Front = 0,
        Back = 1
    }

    public LuggageSide GetSide(Collider hit)
    {
        if (hit == frontCollider) return LuggageSide.Front;
        if (hit == backCollider) return LuggageSide.Back;
        return LuggageSide.Front;
    }

    private void Awake()
    {
        rend = GetComponentInChildren<Renderer>();

        if (rend != null)
            mat = rend.material;
    }

    public void SetHighlight(bool active)
    {
        isHighlighted = active;

        if (mat == null)
            return;

        if (active)
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

    public void SetFromCollider(Collider hitCollider)
    {
        // optioneel richting-gedrag (net als deur)
        if (hitCollider == frontCollider)
        {
            // front side hit
        }
        else if (hitCollider == backCollider)
        {
            // back side hit
        }
    }

    public bool IsHighlighted() => isHighlighted;
}