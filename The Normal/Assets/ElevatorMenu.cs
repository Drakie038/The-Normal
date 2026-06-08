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

    [Header("Start Elevator Button")]
    [SerializeField] private Button startElevatorButton;

    private void Awake()
    {
        Instance = this;

        if (leaveButton != null)
        {
            leaveButton.gameObject.SetActive(false);
            leaveButton.onClick.AddListener(OnClickLeave);
        }

        if (startElevatorButton != null)
        {
            startElevatorButton.gameObject.SetActive(false);
            startElevatorButton.onClick.AddListener(OnClickStartElevator);
        }

        if (timerText != null)
            timerText.gameObject.SetActive(false);
    }

    public void UpdateStartButton(bool isInElevator)
    {
        if (startElevatorButton == null) return;

        startElevatorButton.gameObject.SetActive(isInElevator);
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

    public void CancelCooldownInstant()
    {
        StopTimer();

        ShowLeaveButton(false);

        currentTimer = 0f;

        if (timerText != null)
        {
            timerText.text = "";
            timerText.gameObject.SetActive(false);
        }

        if (startElevatorButton != null)
        {
            startElevatorButton.gameObject.SetActive(false);
        }
    }

    public void StopTimer()
    {
        currentTimer = 0f;

        if (timerRoutine != null)
        {
            StopCoroutine(timerRoutine);
            timerRoutine = null;
        }

        if (timerText != null)
        {
            timerText.text = "";
            timerText.gameObject.SetActive(false);
        }
    }

    private IEnumerator TimerCountdown()
    {
        bool doorsClosedTriggered = false;

        while (currentTimer > 0f)
        {
            currentTimer -= Time.deltaTime;

            int secondsLeft = Mathf.CeilToInt(currentTimer);

            if (timerText != null)
                timerText.text = secondsLeft.ToString();

            // 🚪 3 seconden voor einde: leave button weg + deuren dicht
            if (!doorsClosedTriggered && currentTimer <= 3f)
            {
                doorsClosedTriggered = true;

                ShowLeaveButton(false);

                // 🚪 deuren sluiten starten
                FindObjectOfType<OpenDoorEntrance>()?.StartDoorSequence();
            }

            yield return null;
        }

        if (timerText != null)
            timerText.text = "0";

        StopTimer();

        // 🚀 elevator start bij 0
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
        CancelCooldownInstant();

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

    private void OnClickStartElevator()
    {
        if (startElevatorButton != null)
            startElevatorButton.gameObject.SetActive(false);

        ElevatorPlayers.Instance?.ForceStartElevatorServerRpc();
    }

public void RefreshButtonsAfterCinematic()
{
    PlayerCubeController player =
        FindObjectOfType<PlayerCubeController>();

    if (player == null || !player.IsOwner)
        return;

    bool inElevator = player.inElevator.Value;

        UpdateStartButton(inElevator);
}

    public void ShowElevatorButtonsAfterCinematic(
    bool showStartButton)
    {
        ShowLeaveButton(true);

        if (startElevatorButton != null)
            startElevatorButton.gameObject.SetActive(showStartButton);
    }
}