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
    public AnimationCurve curve =
        AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Elevator Lock Rotation")]
    public Vector3 elevatorCameraRotation;

    private Transform target;
    private PlayerCubeController player;

    private enum State { Menu, Cinematic, FPS }
    private State state;

    private float xRotation;

    public bool inputLocked;
    public bool elevatorLocked;

    private Vector3 menuPos;
    private Quaternion menuRot;

    private Vector3 frozenPos;
    private Quaternion frozenRot;

    private void Start()
    {
        menuPos = transform.position;
        menuRot = transform.rotation;
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

        frozenPos = target.position;
        frozenRot = target.rotation;

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

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

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

        float yaw = target.eulerAngles.y;

        // =====================================================
        // 🚨 FULL ELEVATOR LOCK (FIXED)
        // =====================================================
        if (inputLocked || elevatorLocked || player.inElevator)
        {
            // ❌ HARD STOP: no vertical look ever
            xRotation = 0f;

            // smooth follow only (no input influence)
            transform.position = Vector3.Lerp(
                transform.position,
                target.position + firstPersonOffset,
                12f * Time.deltaTime
            );

            // FULL LOCK ROTATION (no pitch, no input)
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.Euler(
                    elevatorCameraRotation.x,
                    yaw,
                    elevatorCameraRotation.z
                ),
                12f * Time.deltaTime
            );

            return;
        }

        // =====================================================
        // 🎮 NORMAL FPS CAMERA
        // =====================================================

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        player.SendLookInputServerRpc(mouseX);

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -maxLookAngle, maxLookAngle);

        transform.rotation = Quaternion.Euler(xRotation, yaw, 0f);
        transform.position = target.position + firstPersonOffset;
    }

    public IEnumerator ElevatorLookAt(Transform lookTarget, float duration)
    {
        elevatorLocked = true;
        inputLocked = true;

        player.SetFrozen(true);
        player.SetInElevator(true);

        yield return null;
    }

    public IEnumerator ExitElevatorCinematic(Transform targetPivot, float duration)
    {
        inputLocked = true;

        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;

        Vector3 endPos = targetPivot.position + firstPersonOffset;
        Quaternion endRot = Quaternion.identity;

        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;

            float c = curve.Evaluate(Mathf.Clamp01(t / duration));

            transform.position = Vector3.Lerp(startPos, endPos, c);
            transform.rotation = Quaternion.Slerp(startRot, endRot, c);

            yield return null;
        }

        inputLocked = false;
        elevatorLocked = false;

        state = State.FPS;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void ResetCameraToMenu()
    {
        StopAllCoroutines();

        target = null;
        player = null;

        inputLocked = true;
        elevatorLocked = false;

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
    }
}