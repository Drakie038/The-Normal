using UnityEngine;
using System.Collections;
using Unity.Netcode;

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

    [Header("Peek")]
    public float maxPeekYaw = 40f;

    private float peekYawOffset;
    private float peekStartYaw;

    [Header("Lean")]
    public float leanSmoothSpeed = 4f;

    private float leanVelocity;

    [Header("Push Mode Camera Sway")]
    public float pushYawSwayAmount = 2f;   // max graden links/rechts
    public float pushYawSwaySpeed = 3f;    // hoe snel hij beweegt

    private float pushSwayTime;

    private SuitCase heldSuitCase;

    private bool InPushMode()
    {
        return player != null && player.inPushMode.Value;
    }

    private bool IsPeeking()
    {
        return currentDoor != null && currentDoor.IsPeeking();
    }

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

    private LuggageCart currentLuggage;

    private SuitCase currentSuitCase;
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

        if (Input.GetKeyDown(KeyCode.E))
        {
            if (heldSuitCase != null)
            {
                heldSuitCase.Drop();
                heldSuitCase = null;
            }
        }

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

        // ================= PUSH MODE =================
        if (InPushMode())
        {
            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, -maxLookAngle, maxLookAngle);

            Transform pushTarget = player != null ? player.transform : null;

            if (pushTarget != null)
            {
                Vector3 dir = pushTarget.forward;
                dir.y = 0f;

                if (dir.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(dir);
                    float fixedYaw = targetRot.eulerAngles.y;

                    // 🔥 HIER komt de condition
                    if (Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.1f)
                    {
                        pushSwayTime += Time.deltaTime * pushYawSwaySpeed;
                    }

                    float sway = Mathf.Sin(pushSwayTime) * pushYawSwayAmount;

                    transform.rotation = Quaternion.Euler(
                        xRotation,
                        fixedYaw + sway,
                        0f
                    );
                }
            }

            transform.position = target.position + firstPersonOffset;
            return;
        }

        // ================= NORMAL MODE =================

        if (IsPeeking())
        {
            peekYawOffset += mouseX;
            peekYawOffset = Mathf.Clamp(peekYawOffset, -maxPeekYaw, maxPeekYaw);
        }
        else
        {
            player.SendLookInputServerRpc(mouseX);
        }

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -maxLookAngle, maxLookAngle);

        float targetLean = 0f;

        if (currentDoor != null)
            targetLean = currentDoor.GetLean();

        doorLean = Mathf.SmoothDamp(
            doorLean,
            targetLean,
            ref leanVelocity,
            0.25f
        );

        float yaw = target.eulerAngles.y;

        if (IsPeeking())
            yaw += peekYawOffset;

        transform.rotation = Quaternion.Euler(
            xRotation,
            yaw,
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
            // ❌ BLOCK EVERYTHING THAT IS NOT INTERACT
            if (!hit.collider.CompareTag("Interact"))
            {
                // belangrijk: reset current door/lever/luggage als je wil
                ClearInteractions();
                return;
            }

            // ================= DOOR =================
            DoorHallway door = hit.collider.GetComponentInParent<DoorHallway>();

            if (door != null)
            {
                if (currentDoor != null &&
                    currentDoor.IsPeeking() &&
                    door != currentDoor)
                {
                    return;
                }

                if (currentDoor != door)
                {
                    if (currentDoor != null)
                    {
                        currentDoor.SetHighlight(false);
                    }

                    currentDoor = door;
                    currentDoor.SetHighlight(true);
                    currentDoor.SetCurrentPlayer(player);
                }

                currentDoor.SetFromCollider(hit.collider);
                return;
            }

            // ================= LEVER =================
            Lever lever = hit.collider.GetComponentInParent<Lever>();

            if (lever != null)
            {
                lever.SetHighlight(true);

                if (Input.GetKeyDown(KeyCode.E))
                {
                    lever.TryActivate();
                }

                return;
            }

            //SuitCase
            SuitCase suitCase = hit.collider.GetComponent<SuitCase>();

            if (suitCase != null)
            {
                if (currentSuitCase != suitCase)
                {
                    if (currentSuitCase != null)
                        currentSuitCase.SetHighlight(false);

                    currentSuitCase = suitCase;
                }

                currentSuitCase.SetHighlight(true);

                // ❌ voeg cooldown check toe hier
                if (Input.GetKeyDown(KeyCode.E))
                {
                    if (Time.time >= suitCase.GetPickupCooldown())
                    {
                        suitCase.PickUp(transform);
                        heldSuitCase = suitCase;
                    }
                }

                return;
            }

            // ================= LUGGAGE =================
            LuggageCart luggage = hit.collider.GetComponentInParent<LuggageCart>();

            if (luggage != null)
            {
                if (InPushMode())
                {
                    luggage.SetHighlight(false);

                    if (Input.GetKeyDown(KeyCode.E))
                    {
                        player.StopPush();
                    }

                    return;
                }

                luggage.SetHighlight(true);
                currentLuggage = luggage;

                bool pressedE = Input.GetKeyDown(KeyCode.E);

                if (pressedE)
                {
                    TogglePush(luggage, hit.collider);
                }

                return;
            }

            if (luggage != null)
            {
                luggage.SetHighlight(true);
                luggage.SetFromCollider(hit.collider);
                return;
            }
        }

        ClearInteractions();
    }

    private void TogglePush(LuggageCart luggage, Collider hit)
    {
        PlayerCubeController p = player;

        if (p == null)
            return;

        if (p.inPushMode.Value)
        {
            p.StopPush();
            p.inPushMode.Value = false;
            return;
        }

        Transform target = null;

        if (hit == luggage.frontCollider)
        {
            target = luggage.pushFor;
        }
        else if (hit == luggage.backCollider)
        {
            target = luggage.pushBack;
        }

        if (target == null)
            return;

        // EXACT hetzelfde idee als DoorHallway:
        // collider -> luggage -> parent networkobject

        NetworkObject rootNetworkObject =
            luggage.GetComponentInParent<NetworkObject>();

        if (rootNetworkObject == null)
        {
            Debug.LogError("No parent NetworkObject found");
            return;
        }

        p.RequestStartPushServerRpc(
            rootNetworkObject,
            hit == luggage.frontCollider
        );
    }

    private IEnumerator EnterPushMode(PlayerCubeController p, Transform target, Vector3 lookDir)
    {
        inputLocked = true;

        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;

        float t = 0f;
        float duration = 0.4f;

        while (t < duration)
        {
            t += Time.deltaTime;

            float n = Mathf.Clamp01(t / duration);

            transform.position = Vector3.Lerp(
                startPos,
                target.position + firstPersonOffset,
                n
            );

            Quaternion targetRot = Quaternion.LookRotation(lookDir);

            transform.rotation = Quaternion.Slerp(
                startRot,
                targetRot,
                n
            );

            yield return null;
        }

        transform.position = target.position + firstPersonOffset;
        transform.rotation = Quaternion.LookRotation(lookDir);

        inputLocked = false;

        p.SetPushMode(true, target);
    }

    private void ClearInteractions()
    {
        if (currentDoor != null)
        {
            currentDoor.SetHighlight(false);
            currentDoor.SetCurrentPlayer(null);
            currentDoor = null;
        }

        Lever oldLever = FindObjectOfType<Lever>();
        if (oldLever != null)
        {
            oldLever.SetHighlight(false);
        }

        LuggageCart oldLuggage = FindObjectOfType<LuggageCart>();
        if (oldLuggage != null)
        {
            oldLuggage.SetHighlight(false);
        }

        if (currentSuitCase != null)
        {
            currentSuitCase.SetHighlight(false);
            currentSuitCase = null;
        }
    }

    public void BeginPeek()
    {
        peekStartYaw = target.eulerAngles.y;
        peekYawOffset = 0f;
    }

    public void EndPeek()
    {
        peekYawOffset = 0f;
    }
}