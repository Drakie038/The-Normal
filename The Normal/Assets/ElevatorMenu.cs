using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class ElevatorMenu : MonoBehaviour
{
    public static ElevatorMenu Instance;

    [Header("Leave Button")]
    [SerializeField] private Button leaveButton;

    [Header("Timer UI")]
    [SerializeField] private TMP_Text timerText;

    private Coroutine timerRoutine;
    private float currentTimer;

    private void Awake()
    {
        Instance = this;

        if (leaveButton != null)
        {
            leaveButton.gameObject.SetActive(false);
            leaveButton.onClick.AddListener(OnClickLeave);
        }

        if (timerText != null)
            timerText.gameObject.SetActive(false);
    }

    public void ShowLeaveButton(bool value)
    {
        if (leaveButton != null)
            leaveButton.gameObject.SetActive(value);
    }

    public void StartTimer(float seconds)
    {
        StopTimer();

        currentTimer = seconds;

        if (timerText != null)
            timerText.gameObject.SetActive(true);

        timerRoutine = StartCoroutine(TimerCountdown());
    }

    public void StopTimer()
    {
        if (timerRoutine != null)
            StopCoroutine(timerRoutine);

        timerRoutine = null;

        if (timerText != null)
            timerText.gameObject.SetActive(false);
    }

    private IEnumerator TimerCountdown()
    {
        while (currentTimer > 0f)
        {
            currentTimer -= Time.deltaTime;

            int secondsLeft = Mathf.CeilToInt(currentTimer);

            if (timerText != null)
                timerText.text = secondsLeft.ToString();

            // 🔥 FIX: leave button verdwijnt bij <= 1 seconde
            if (secondsLeft <= 1)
            {
                ShowLeaveButton(false);
            }

            yield return null;
        }

        if (timerText != null)
            timerText.text = "0";

        StopTimer();

        ElevatorPlayers.Instance?.TriggerElevatorStartServerRpc();
    }

    public void ForceResetUI()
    {
        ShowLeaveButton(false);
        StopTimer();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ForceHideLeaveButton()
    {
        ShowLeaveButton(false);
    }

    private void OnClickLeave()
    {
        PlayerCubeController[] players =
            FindObjectsOfType<PlayerCubeController>();

        foreach (var player in players)
        {
            if (player.IsOwner)
            {
                player.LeaveElevator();
                break;
            }
        }
    }
}