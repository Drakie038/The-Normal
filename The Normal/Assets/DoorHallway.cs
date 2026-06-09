using UnityEngine;

public class DoorHallway : MonoBehaviour
{
    [Header("Door Settings")]
    public float openAngle = 90f;
    public float peekAngleMultiplier = 0.35f;
    public float openSpeed = 6f;

    [Header("Camera Lean")]
    public float cameraLeanAngle = 8f;
    public float cameraLeanSpeed = 6f;

    [Header("Highlight")]
    public Color highlightColor = Color.yellow;
    [Range(0f, 2f)] public float intensity = 0.3f;

    [Header("Colliders")]
    public Collider frontCollider;
    public Collider backCollider;

    private Renderer rend;
    private Material mat;

    private bool isHighlighted;

    private bool isPeeking;
    private bool isOpen;

    private float holdTimer;
    private const float holdThreshold = 0.25f;
    private bool holding;

    private Quaternion targetRotation;

    private PlayerCubeController currentPlayer;
    private CameraMovement currentCamera;

    private bool isTransitioning;

    // 🔥 direction
    private int sideSign = 1;

    // 🔥 base rotation Y
    private float baseY;

    private void Awake()
    {
        rend = GetComponentInChildren<Renderer>();

        if (rend != null)
            mat = rend.material;

        baseY = transform.eulerAngles.y;

        targetRotation = transform.rotation;
    }

    private void Update()
    {
        HandleInput();

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            Time.deltaTime * openSpeed
        );

        // 🔥 NEW: transition finish detect
        if (Quaternion.Angle(transform.rotation, targetRotation) < 0.5f)
        {
            transform.rotation = targetRotation;
            isTransitioning = false;
        }

        HandleCameraLean();
    }

    private void HandleInput()
    {
        if (!isHighlighted)
            return;

        if (Input.GetKeyDown(KeyCode.E))
        {
            holding = true;
            holdTimer = 0f;
        }

        if (holding)
        {
            holdTimer += Time.deltaTime;

            if (holdTimer >= holdThreshold && !isPeeking)
            {
                StartPeek();
            }
        }

        if (Input.GetKeyUp(KeyCode.E))
        {
            holding = false;
            holdTimer = 0f;

            if (isPeeking || isTransitioning)
            {
                ForceClose();
                return;
            }

            ToggleDoor();
        }
    }

    private void ToggleDoor()
    {
        if (isTransitioning) return;

        isTransitioning = true;

        isOpen = !isOpen;

        float dir = sideSign;

        float y = baseY + (isOpen ? openAngle * dir : 0f);

        targetRotation = Quaternion.Euler(
            0f,
            y,
            0f
        );
    }

    private void StartPeek()
    {
        if (isTransitioning) return;

        isTransitioning = true;
        isPeeking = true;

        if (currentPlayer != null)
            currentPlayer.SetFrozen(true);

        float dir = sideSign;

        float y = baseY + (openAngle * peekAngleMultiplier * dir);

        targetRotation = Quaternion.Euler(
            0f,
            y,
            0f
        );
    }

    private void HandleCameraLean()
    {
        if (currentCamera == null)
            return;

        float targetLean = isPeeking ? cameraLeanAngle : 0f;

        currentCamera.SetDoorLean(
            Mathf.Lerp(currentCamera.GetDoorLean(), targetLean, Time.deltaTime * cameraLeanSpeed)
        );
    }

    public void SetCurrentPlayer(PlayerCubeController player)
    {
        currentPlayer = player;
        currentCamera = FindObjectOfType<CameraMovement>();
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

    private void ForceClose()
    {
        isPeeking = false;
        isOpen = false;
        holding = false;
        holdTimer = 0f;

        if (currentPlayer != null)
            currentPlayer.SetFrozen(false);

        targetRotation = Quaternion.Euler(0f, baseY, 0f);

        if (currentCamera != null)
            currentCamera.SetDoorLean(0f);

        isTransitioning = true;
    }

    // 🔥 called from camera
    public void SetFromCollider(Collider hitCollider)
    {
        if (hitCollider == frontCollider)
            sideSign = 1;
        else if (hitCollider == backCollider)
            sideSign = -1;
    }
}