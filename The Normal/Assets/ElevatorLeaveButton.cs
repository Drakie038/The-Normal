using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class ElevatorLeaveButton : MonoBehaviour
{
    [Header("References")]
    public CameraMovement cameraMovement;
    public PlayerCubeController player;
    public Button leaveButton;

    [Header("Settings")]
    public float exitDuration = 2.5f;

    private bool isLeaving;

    private void Start()
    {
        if (leaveButton != null)
        {
            leaveButton.gameObject.SetActive(false);
            leaveButton.onClick.AddListener(OnLeavePressed);
        }
    }

    public void ShowButton(bool state)
    {
        if (leaveButton != null)
            leaveButton.gameObject.SetActive(state);
    }

    public void OnLeavePressed()
    {
        if (isLeaving) return;

        StartCoroutine(LeaveRoutine());
    }

    private IEnumerator LeaveRoutine()
    {
        isLeaving = true;

        if (player == null || cameraMovement == null)
        {
            isLeaving = false;
            yield break;
        }

        player.SetFrozen(true);

        Transform pivot =
            player.cameraPivot != null ? player.cameraPivot : player.transform;

        yield return cameraMovement.ExitElevatorCinematic(pivot, exitDuration);

        // 🔥 SERVER AUTHORITATIVE EXIT
        Vector3 forward = Vector3.forward;
        if (Camera.main != null)
            forward = Camera.main.transform.forward;

        forward.y = 0f;

        Vector3 exitPos =
            player.transform.position + forward.normalized * 6f;

        player.RequestExitElevatorServerRpc(exitPos);

        player.SetFrozen(false);
        player.EnableMovement();

        if (leaveButton != null)
            leaveButton.gameObject.SetActive(false);

        isLeaving = false;
    }
}