using UnityEngine;
using System.Collections;

public class Prullenbak : MonoBehaviour
{
    [Header("Trash Target (bin inside)")]
    public Transform trashTarget;

    [Header("Lid (deksel)")]
    public Transform lid;

    [Header("Highlight Settings")]
    public Color highlightColor = Color.yellow;
    [Range(0f, 2f)] public float intensity = 0.05f;

    public Vector3 openRotation = new Vector3(-110f, 0f, 0f);
    public float lidSpeed = 6f;

    private Quaternion closedRot;
    private Quaternion openRot;

    private Renderer[] renderers;
    private Material[] mats;

    void Awake()
    {
        if (lid != null)
        {
            closedRot = lid.localRotation;
            openRot = Quaternion.Euler(openRotation);
        }

        renderers = GetComponentsInChildren<Renderer>();
        mats = new Material[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            mats[i] = renderers[i].material;
        }
    }

    // =========================
    // FLEXIBLE HIGHLIGHT
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

    // =========================
    // LID ANIMATION
    // =========================
    public void OpenLid()
    {
        StopAllCoroutines();
        StartCoroutine(LidRoutine(true));
    }

    public void CloseLid()
    {
        StopAllCoroutines();
        StartCoroutine(LidRoutine(false));
    }

    private IEnumerator LidRoutine(bool open)
    {
        Quaternion start = lid.localRotation;
        Quaternion end = open ? openRot : closedRot;

        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime * lidSpeed;
            lid.localRotation = Quaternion.Slerp(start, end, t);
            yield return null;
        }

        lid.localRotation = end;
    }
}