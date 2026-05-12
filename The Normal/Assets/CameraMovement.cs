using UnityEngine;
using System.Collections;

public class CameraMovement : MonoBehaviour
{
    public Vector3 firstPersonOffset = new Vector3(0f, 1.6f, 0f);
    public float smoothSpeed = 10f;

    [Header("Spawn Delay")]
    public float delayBeforeMove = 0.5f; // ⬅️ WACHTTIJD

    public Transform target;
    private bool ready = false;

    private Vector3 defaultPosition;
    private Quaternion defaultRotation;
    private bool inMenu = true;

    private void Start()
    {
        defaultPosition = transform.position;
        defaultRotation = transform.rotation;
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        inMenu = false;

        ready = false;

        StopAllCoroutines();
        StartCoroutine(DelayedSnapToPlayer());
    }

    public void ResetCameraToMenu()
    {
        target = null;
        ready = false;
        inMenu = true;

        StopAllCoroutines();
        StartCoroutine(ReturnToMenu());
    }

    // 🔥 NEW: delay + move combo
    private IEnumerator DelayedSnapToPlayer()
    {
        // ⏳ eerst wachten
        yield return new WaitForSeconds(delayBeforeMove);

        if (target == null) yield break;

        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;

        Vector3 endPos = target.position + firstPersonOffset;
        Quaternion endRot = target.rotation;

        float t = 0f;
        float duration = 1f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float progress = t / duration;

            transform.position = Vector3.Lerp(startPos, endPos, progress);
            transform.rotation = Quaternion.Slerp(startRot, endRot, progress);

            yield return null;
        }

        ready = true;
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

            transform.position = Vector3.Lerp(startPos, defaultPosition, p);
            transform.rotation = Quaternion.Slerp(startRot, defaultRotation, p);

            yield return null;
        }
    }

    private void LateUpdate()
    {
        if (inMenu || !ready || target == null) return;

        transform.position = target.position + firstPersonOffset;
        transform.rotation = target.rotation;
    }
}