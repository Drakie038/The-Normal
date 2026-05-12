using UnityEngine;
using System.Collections;

public class CameraMovement : MonoBehaviour
{
    [Header("Follow")]
    public Vector3 firstPersonOffset = new Vector3(0f, 1.6f, 0f);

    [Header("Mouse Look")]
    public float mouseSensitivity = 200f;
    public float maxLookAngle = 85f;

    [Header("Timings")]
    [SerializeField] private float spawnCameraDelay = 1.5f;

    private Transform target;
    private PlayerCubeController player;

    private enum CameraState { Menu, Transition, FPS }
    private CameraState state = CameraState.Menu;

    private float xRotation = 0f;

    private Vector3 menuPos;
    private Quaternion menuRot;

    private bool inputLocked = true;

    private void Start()
    {
        menuPos = transform.position;
        menuRot = transform.rotation;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        player = newTarget.GetComponentInParent<PlayerCubeController>();

        StopAllCoroutines();
        StartCoroutine(SmoothEnter());
        StartCoroutine(UnlockInputAfterDelay());
    }

    private IEnumerator UnlockInputAfterDelay()
    {
        inputLocked = true;
        yield return new WaitForSeconds(spawnCameraDelay);
        inputLocked = false;
    }

    public void ResetCameraToMenu()
    {
        target = null;
        player = null;
        state = CameraState.Menu;

        StopAllCoroutines();
        StartCoroutine(ReturnToMenu());

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        inputLocked = true;
    }

    private IEnumerator SmoothEnter()
    {
        state = CameraState.Transition;

        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;

        float t = 0f;
        float duration = 1f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float p = t / duration;

            if (target == null) yield break;

            Vector3 endPos = target.position + firstPersonOffset;

            transform.position = Vector3.Lerp(startPos, endPos, p);
            transform.rotation = Quaternion.Slerp(startRot, target.rotation, p);

            yield return null;
        }

        state = CameraState.FPS;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private IEnumerator ReturnToMenu()
    {
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;

        float t = 0f;
        float duration = 1f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float p = t / duration;

            transform.position = Vector3.Lerp(startPos, menuPos, p);
            transform.rotation = Quaternion.Slerp(startRot, menuRot, p);

            yield return null;
        }
    }

    private void LateUpdate()
    {
        if (state != CameraState.FPS || target == null || player == null)
            return;

        if (inputLocked)
            return;

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        player.SendLookInputServerRpc(mouseX);

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -maxLookAngle, maxLookAngle);

        transform.rotation = Quaternion.Euler(xRotation, target.eulerAngles.y, 0f);
        transform.position = target.position + firstPersonOffset;
    }
}