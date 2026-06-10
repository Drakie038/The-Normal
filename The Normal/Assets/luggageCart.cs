using UnityEngine;

public class LuggageCart : MonoBehaviour
{
    [Header("Luggage Colliders (CHILDREN)")]
    public Collider frontCollider;
    public Collider backCollider;

    [Header("Highlight")]
    public Color highlightColor = Color.yellow;
    [Range(0f, 2f)] public float intensity = 0.3f;

    private Renderer rend;
    private Material mat;

    public Transform pushFor;
    public Transform pushBack;

    private bool playerInside;
    private PlayerCubeController currentPlayer;

    private bool isHighlighted;

    private void Awake()
    {
        rend = GetComponentInChildren<Renderer>();
        if (rend != null)
            mat = rend.material;
    }

    // Called from camera raycast
    public void SetFromCollider(Collider hitCollider)
    {
        // Hier kun je later direction logic doen (push/back system)
        if (hitCollider == frontCollider)
        {
            // Debug.Log("Front geraakt");
        }
        else if (hitCollider == backCollider)
        {
            // Debug.Log("Back geraakt");
        }
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
            mat.DisableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", Color.black);
        }
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
}