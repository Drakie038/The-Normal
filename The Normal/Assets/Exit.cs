using UnityEngine;

public class Exit : MonoBehaviour
{
    [Header("Highlight Settings")]
    public Color highlightColor = Color.yellow;
    [Range(0f, 2f)] public float intensity = 0.05f;

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

    // =========================
    // SAME AS PRULLENBAK
    // =========================
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

    public void TryExit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}