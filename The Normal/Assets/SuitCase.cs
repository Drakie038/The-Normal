using UnityEngine;

public class SuitCase : MonoBehaviour
{

    [Header("Highlight")]
    public Color highlightColor = Color.white;
    [Range(0f, 2f)] public float intensity = 0.05f;

    private Renderer rend;
    private Material mat;
    private bool isHighlighted;

    void Awake()
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
            mat.DisableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", Color.black);
        }
    }

    public void PickUpSuitCase()
    {

    }
}
