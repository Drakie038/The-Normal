using UnityEngine;

public class DoorHallway : MonoBehaviour
{
    public enum DoorDirection
    {
        Left,
        Right
    }

    [Header("Door Settings")]
    public DoorDirection doorDirection = DoorDirection.Left;
    public float openAngle = 90f;
    public float openSpeed = 3f;

    [Header("Highlight Settings")]
    public Color highlightColor = Color.yellow;

    [Range(0f, 2f)]
    public float intensity = 0.3f;

    private Renderer rend;
    private Material mat;

    private bool isHighlighted;
    private bool isOpen;

    private Quaternion closedRotation;
    private Quaternion openRotation;
    private Quaternion targetRotation;

    private void Awake()
    {
        rend = GetComponentInChildren<Renderer>();

        if (rend != null)
            mat = rend.material;

        closedRotation = transform.rotation;

        float directionMultiplier = (doorDirection == DoorDirection.Left) ? -1f : 1f;
        openRotation = closedRotation * Quaternion.Euler(0f, openAngle * directionMultiplier, 0f);

        targetRotation = closedRotation;
    }

    private void Start()
    {
        SetHighlight(false);
    }

    private void Update()
    {
        if (isHighlighted && Input.GetKeyDown(KeyCode.E))
        {
            ToggleDoor();
        }

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            Time.deltaTime * openSpeed
        );
    }

    private void ToggleDoor()
    {
        isOpen = !isOpen;
        targetRotation = isOpen ? openRotation : closedRotation;
    }

    public void SetHighlight(bool active)
    {
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