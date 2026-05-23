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

    private Transform target;
    private PlayerCubeController player;

    private enum State { Menu, Cinematic, FPS }
    private State state;

    private float xRotation;
    private bool inputLocked;

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
        if (player == null || !player.IsOwner)
            yield break;

        inputLocked = true;
        state = State.Cinematic;

        yield return new WaitForSeconds(waitBeforeMove);

        // 🔥 freeze player movement
        player.SetFrozen(true);

        // 🔥 snapshot target (NO jitter)
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

        // 🔥 FINAL SNAP POSITION (important)
        transform.position = frozenPos + firstPersonOffset;
        transform.rotation = Quaternion.Euler(0f, frozenRot.eulerAngles.y, 0f);

        // 🔥 CRITICAL FIX: prevent downward snap
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
        if (player == null || !player.IsOwner)
            return;

        if (state != State.FPS || inputLocked || target == null)
            return;

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        player.SendLookInputServerRpc(mouseX);

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -maxLookAngle, maxLookAngle);

        float yaw = target.eulerAngles.y;

        transform.rotation = Quaternion.Euler(xRotation, yaw, 0f);
        transform.position = target.position + firstPersonOffset;
    }

    public void ResetCameraToMenu()
    {
        StopAllCoroutines();

        target = null;
        player = null;

        state = State.Menu;
        inputLocked = true;

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