using UnityEngine;
using UnityEngine.UI;

public class ElevatorMenu : MonoBehaviour
{
    public static ElevatorMenu Instance;

    [Header("Leave Button")]
    [SerializeField] private Button leaveButton;

    private void Awake()
    {
        Instance = this;

        if (leaveButton != null)
        {
            leaveButton.gameObject.SetActive(false);
            leaveButton.onClick.AddListener(OnClickLeave);
        }
    }

    public void ShowLeaveButton(bool value)
    {
        if (leaveButton != null)
        {
            leaveButton.gameObject.SetActive(value);
        }
    }

    // 🔥 HARD RESET
    public void ForceResetUI()
    {
        ShowLeaveButton(false);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
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