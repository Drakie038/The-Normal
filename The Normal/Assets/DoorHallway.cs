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
    public float peekAngleMultiplier = 0.35f;
    public float openSpeed = 6f;

    [Header("Camera Lean")]
    public float cameraLeanAngle = 8f;
    public float cameraLeanSpeed = 6f;

    [Header("Highlight")]
    public Color highlightColor = Color.yellow;
    [Range(0f, 2f)] public float intensity = 0.3f;

    private Renderer rend;
    private Material mat;

    private bool isHighlighted;

    private bool isPeeking;
    private bool isOpen;

    private float holdTimer;
    private const float holdThreshold = 0.25f;
    private bool holding;

    private Quaternion closedRotation;
    private Quaternion openRotation;
    private Quaternion peekRotation;
    private Quaternion targetRotation;

    private PlayerCubeController currentPlayer;
    private CameraMovement currentCamera;

    private bool isTransitioning;
    private void Awake()
    {
        rend = GetComponentInChildren<Renderer>();

        if (rend != null)
            mat = rend.material;

        closedRotation = transform.rotation;

        float dir = (doorDirection == DoorDirection.Left) ? -1f : 1f;

        openRotation = closedRotation * Quaternion.Euler(0f, openAngle * dir, 0f);
        peekRotation = closedRotation * Quaternion.Euler(0f, openAngle * peekAngleMultiplier * dir, 0f);

        targetRotation = closedRotation;
    }

    private void Update()
    {
        HandleInput();

        if (isTransitioning)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                Time.deltaTime * openSpeed
            );

            if (Quaternion.Angle(transform.rotation, targetRotation) < 0.5f)
            {
                transform.rotation = targetRotation;
                isTransitioning = false;
            }

            HandleCameraLean();
            return;
        }

        if (!isPeeking && !holding)
        {
            // allow normal door motion to finish quickly
            isTransitioning = false;
        }

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            Time.deltaTime * openSpeed
        );

        HandleCameraLean();
    }

    private void HandleInput()
    {
        if (!isHighlighted)
            return;

        // START HOLD
        if (Input.GetKeyDown(KeyCode.E))
        {
            holding = true;
            holdTimer = 0f;
        }

        // HOLDING
        if (holding)
        {
            holdTimer += Time.deltaTime;

            // 🔥 PEek ACTIVE zodra threshold bereikt is (TIJDENS hold)
            if (holdTimer >= holdThreshold && !isPeeking)
            {
                StartPeek();
            }
        }

        if (Input.GetKeyUp(KeyCode.E))
        {
            holding = false;
            holdTimer = 0f;

            // 🔥 ALS E WORDT LOSGELATEN: ALTIJD DIRECT CLOSE OVERRIDE
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
        targetRotation = isOpen ? openRotation : closedRotation;
    }

    private void StartPeek()
    {
        if (isTransitioning) return;

        isTransitioning = true;

        isPeeking = true;

        if (currentPlayer != null)
            currentPlayer.SetFrozen(true);

        targetRotation = peekRotation;
    }

    private void StopPeek()
    {
        if (isTransitioning) return;

        isTransitioning = true;

        isPeeking = false;

        if (currentPlayer != null)
            currentPlayer.SetFrozen(false);

        // 🔥 altijd terug naar dicht
        targetRotation = closedRotation;

        if (currentCamera != null)
            currentCamera.SetDoorLean(0f);
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

        targetRotation = closedRotation;

        if (currentCamera != null)
            currentCamera.SetDoorLean(0f);

        isTransitioning = true;
    }
}