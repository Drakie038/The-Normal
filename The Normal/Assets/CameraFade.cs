using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class CameraFade : MonoBehaviour
{
    public static CameraFade Instance;

    [SerializeField] private Image blackImage;
    [SerializeField] private float fadeDuration = 2f;

    private Coroutine hideRoutine;

    private void Awake()
    {
        Instance = this;

        if (blackImage != null)
            blackImage.enabled = false;
    }

    public void ShowFade(float autoHideTime = 1f)
    {
        if (blackImage == null)
            return;

        blackImage.enabled = true;

        if (hideRoutine != null)
            StopCoroutine(hideRoutine);

        hideRoutine = StartCoroutine(HideAfterTime(autoHideTime));
    }

    private IEnumerator HideAfterTime(float t)
    {
        yield return new WaitForSeconds(t);

        if (blackImage != null)
            blackImage.enabled = false;
    }

    public void HideFadeInstant()
    {
        if (hideRoutine != null)
            StopCoroutine(hideRoutine);

        if (blackImage != null)
            blackImage.enabled = false;
    }
}