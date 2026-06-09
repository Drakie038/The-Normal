using UnityEngine;
using System.Collections;

public class CameraMovement : MonoBehaviour
{
    [Header("Follow")]
    public Vector3 firstPersonOffset = new Vector3(0f, 1.6f, 0f);

    [Header("Mouse")]
    public float mouseSensitivity = 200f;
    public float maxLookAngle = 85f;

    [Header("Cinematic")]
    public float cinematicDuration = 3f;
    public float waitBeforeMove = 1f;
    public AnimationCurve curve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Elevator Lock Rotation")]
    public Vector3 elevatorCameraRotation;

    private Transform target;
    private PlayerCubeController player;

    [Header("Door Detection")]
    public float doorDetectDistance = 3f;

    private DoorHallway currentDoor;

    private enum State { Menu, Cinematic, FPS }
    private State state;

    private float xRotation;

    public bool inputLocked;
    public bool elevatorLocked;
    public bool settingsLocked;

    private Vector3 menuPos;
    private Quaternion menuRot;

    public MultiplayerMenu menu;

    private Coroutine elevatorTransitionRoutine;
    public bool inElevatorTransition;
    
    private float doorLean;
    public void SetDoorLean(float value)
    {
        doorLeanTarget = value;
    }
    public float GetDoorLean() => doorLean;

    private float doorLeanTarget;

    public void SetDoorLeanTarget(float value)
    {
        doorLeanTarget = value;
    }

    private void Start()
    {
        menuPos = transform.position;
        menuRot = transform.rotation;
    }

    public void PlayElevatorEnterCinematic(Transform playerTarget)
    {
        if (elevatorTransitionRoutine != null)
            StopCoroutine(elevatorTransitionRoutine);

        elevatorTransitionRoutine = StartCoroutine(ElevatorEnterRoutine(playerTarget));
    }

    private IEnumerator ElevatorEnterRoutine(Transform playerTarget)
    {
        inElevatorTransition = true;
        inputLocked = true;

        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;

        float t = 0f;
        float duration = 0.6f;

        while (t < duration)
        {
            t += Time.deltaTime;

            float n = Mathf.Clamp01(t / duration);
            float c = curve.Evaluate(n);

            Vector3 targetPos = playerTarget.position + firstPersonOffset;

            Quaternion targetRot = Quaternion.Euler(
                elevatorCameraRotation.x,
                playerTarget.eulerAngles.y,
                elevatorCameraRotation.z
            );

            transform.position = Vector3.Lerp(
                startPos,
                targetPos,
                c
            );

            transform.rotation = Quaternion.Slerp(
                startRot,
                targetRot,
                c
            );

            yield return null;
        }

        transform.position = playerTarget.position + firstPersonOffset;

        transform.rotation = Quaternion.Euler(
            elevatorCameraRotation.x,
            playerTarget.eulerAngles.y,
            elevatorCameraRotation.z
        );

        inElevatorTransition = false;
        elevatorLocked = true;
    }

    public void SetTarget(Transform newTarget, PlayerCubeController newPlayer)
    {
        target = newTarget;
        player = newPlayer;

        StopAllCoroutines();
        StartCoroutine(Cinematic());
    }

    private IEnumerator Cinematic()
    {
        inputLocked = true;
        state = State.Cinematic;

        yield return new WaitForSeconds(waitBeforeMove);

        player.SetFrozen(true);

        Vector3 frozenPos = target.position;
        Quaternion frozenRot = target.rotation;

        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;

        float t = 0f;

        while (t < cinematicDuration)
        {
            t += Time.deltaTime;

            float n = Mathf.Clamp01(t / cinematicDuration);
            float c = curve.Evaluate(n);

            Vector3 endPos = frozenPos + firstPersonOffset;
            Quaternion endRot = Quaternion.Euler(0f, frozenRot.eulerAngles.y, 0f);

            transform.position = Vector3.Lerp(startPos, endPos, c);
            transform.rotation = Quaternion.Slerp(startRot, endRot, c);

            yield return null;
        }

        transform.position = frozenPos + firstPersonOffset;
        transform.rotation = Quaternion.Euler(0f, frozenRot.eulerAngles.y, 0f);

        xRotation = 0f;
        state = State.FPS;

        inputLocked = false;

        player.SetFrozen(false);
        player.EnableMovement();
    }

    private void LateUpdate()
    {
        if (player == null || !player.IsOwner || target == null)
            return;

        if (state != State.FPS)
            return;

        bool inElevator = player != null && player.inElevator.Value;

        bool lockCamera =
            inputLocked ||
            elevatorLocked ||
            settingsLocked ||
            inElevator;

        if (inElevatorTransition)
        {
            HandleCursor(inElevator);
            DetectDoor(); // ✅ ADD
            return;
        }

        if (lockCamera)
        {
            HandleLockedCamera(inElevator);
            HandleCursor(inElevator);
            DetectDoor(); // ✅ ADD
            return;
        }

        HandleFPSCamera(inElevator);
        HandleCursor(inElevator);

        DetectDoor(); // ✅ ADD (dit was weg)
    }

    private void HandleLockedCamera(bool inElevator)
    {
        xRotation = 0f;

        transform.position = target.position + firstPersonOffset;

        transform.rotation = Quaternion.Euler(
            inElevator ? elevatorCameraRotation.x : 0f,
            target.eulerAngles.y,
            inElevator ? elevatorCameraRotation.z : 0f
        );
    }

    private void HandleFPSCamera(bool inElevator)
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        player.SendLookInputServerRpc(mouseX);

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -maxLookAngle, maxLookAngle);

        doorLean = Mathf.Lerp(doorLean, doorLeanTarget, Time.deltaTime * 8f);

        transform.rotation = Quaternion.Euler(
            xRotation,
            target.eulerAngles.y,
            doorLean
        );
        transform.position = target.position + firstPersonOffset;
    }

    private void HandleCursor(bool inElevator)
    {
        if (settingsLocked)
        {
            SetCursor(true);
            return;
        }

        if (state == State.Menu)
        {
            SetCursor(true);
            return;
        }

        if (inElevator)
        {
            SetCursor(true);
            return;
        }

        SetCursor(false);
    }

    private void SetCursor(bool free)
    {
        Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = free;
    }

    public void ResetCameraToMenu()
    {
        StopAllCoroutines();

        target = null;
        player = null;

        inputLocked = true;
        elevatorLocked = false;
        settingsLocked = false;

        state = State.Menu;

        StartCoroutine(ReturnToMenu());
    }

    private IEnumerator ReturnToMenu()
    {
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;

        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime;

            transform.position = Vector3.Lerp(startPos, menuPos, t);
            transform.rotation = Quaternion.Slerp(startRot, menuRot, t);

            yield return null;
        }

        transform.position = menuPos;
        transform.rotation = menuRot;
    }

    private void DetectDoor()
    {
        Ray ray = new Ray(transform.position, transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, doorDetectDistance))
        {
            DoorHallway door = hit.collider.GetComponentInParent<DoorHallway>();

            if (door != null)
            {
                if (currentDoor != door)
                {
                    if (currentDoor != null)
                    {
                        currentDoor.SetHighlight(false);
                        currentDoor.SetCurrentPlayer(null);
                    }

                    currentDoor = door;
                    currentDoor.SetHighlight(true);
                    currentDoor.SetCurrentPlayer(player);
                }

                // 🔥 CRUCIAL: geef collider door
                currentDoor.SetFromCollider(hit.collider);

                return;
            }
        }

        if (currentDoor != null)
        {
            currentDoor.SetHighlight(false);
            currentDoor.SetCurrentPlayer(null);
            currentDoor = null;
        }
    }
}