using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using UnityEngine.EventSystems;

public class GlobalChatUI : NetworkBehaviour
{
    public static bool IsTyping;

    [Header("UI")]
    [SerializeField] private Button toggleChatButton;

    [Header("Chat UI")]
    [SerializeField] private GameObject chatScrollView;

    [Header("Chat Input")]
    [SerializeField] private TMP_InputField chatInput;

    [Header("Chat Messages")]
    [SerializeField] private Transform messageContainer;
    [SerializeField] private GameObject messagePrefab;

    [Header("Optional")]
    [SerializeField] private ScrollRect scrollRect;

    private bool chatOpen;

    private MultiplayerMenu multiplayerMenu;

    private void Start()
    {
        multiplayerMenu = FindObjectOfType<MultiplayerMenu>();

        IsTyping = false;

        if (chatScrollView != null)
            chatScrollView.SetActive(false);

        if (chatInput != null)
        {
            chatInput.gameObject.SetActive(false);
            chatInput.text = "";
        }

        if (toggleChatButton != null)
            toggleChatButton.gameObject.SetActive(false);

        if (toggleChatButton != null)
            toggleChatButton.onClick.AddListener(ToggleChat);

        CheckIfInServer();
    }

    private void Update()
    {
        CheckIfInServer();

        if (NetworkManager.Singleton == null)
            return;

        bool inServer =
            NetworkManager.Singleton.IsClient ||
            NetworkManager.Singleton.IsHost;

        if (!inServer)
            return;

        // `/` = toggle open/close
        if (Input.GetKeyDown(KeyCode.Slash) || Input.GetKeyDown(KeyCode.KeypadDivide))
        {
            ToggleChat();
        }

        // ESC = close
        if (chatOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            CloseChat();
        }

        // ENTER = send
        if (chatOpen && Input.GetKeyDown(KeyCode.Return))
        {
            SendChatMessage();
        }

        // linkermuisknop buiten input = chat sluiten
        if (chatOpen && Input.GetMouseButtonDown(0))
        {
            if (!RectTransformUtility.RectangleContainsScreenPoint(
                chatInput.GetComponent<RectTransform>(),
                Input.mousePosition))
            {
                CloseChat();
            }
        }

        // scroll omhoog
        if (chatOpen && Input.GetKey(KeyCode.UpArrow))
        {
            if (scrollRect != null)
            {
                scrollRect.verticalNormalizedPosition += 2f * Time.deltaTime;
            }
        }

        // scroll omlaag
        if (chatOpen && Input.GetKey(KeyCode.DownArrow))
        {
            if (scrollRect != null)
            {
                scrollRect.verticalNormalizedPosition -= 2f * Time.deltaTime;
            }
        }

        // keep focus only when chat open AND input active
        if (chatOpen && chatInput != null && !chatInput.isFocused)
        {
            chatInput.ActivateInputField();
        }
    }

    private void CheckIfInServer()
    {
        if (NetworkManager.Singleton == null)
            return;

        bool inServer =
            NetworkManager.Singleton.IsClient ||
            NetworkManager.Singleton.IsHost;

        if (toggleChatButton != null)
            toggleChatButton.gameObject.SetActive(inServer);

        if (!inServer)
            CloseChat();
    }

    // ---------------- CHAT OPEN/CLOSE ----------------

    private void ToggleChat()
    {
        if (chatOpen)
            CloseChat();
        else
            OpenChat();
    }

    private void OpenChat()
    {
        chatOpen = true;
        IsTyping = true;

        if (chatScrollView != null)
            chatScrollView.SetActive(true);

        if (chatInput != null)
        {
            chatInput.gameObject.SetActive(true);
            chatInput.text = "";

            chatInput.ActivateInputField();
            EventSystem.current.SetSelectedGameObject(chatInput.gameObject);
        }
    }

    private void CloseChat()
    {
        chatOpen = false;
        IsTyping = false;

        if (chatInput != null)
        {
            chatInput.text = "";
            chatInput.DeactivateInputField();
            chatInput.gameObject.SetActive(false);
        }

        if (chatScrollView != null)
            chatScrollView.SetActive(false);
    }

    // ---------------- SEND MESSAGE ----------------

    private void SendChatMessage()
    {
        string msg = chatInput.text.Trim();

        if (string.IsNullOrEmpty(msg))
            return;

        string playerName = "Player";

        if (multiplayerMenu != null)
            playerName = multiplayerMenu.GetPlayerName();

        SubmitMessageServerRpc(playerName, msg);

        chatInput.text = "";
        chatInput.ActivateInputField();
    }

    // ---------------- MULTIPLAYER SYNC ----------------

    [ServerRpc(RequireOwnership = false)]
    private void SubmitMessageServerRpc(string playerName, string message)
    {
        BroadcastMessageClientRpc(playerName, message);
    }

    [ClientRpc]
    private void BroadcastMessageClientRpc(string playerName, string message)
    {
        CreateMessage($"{playerName}: {message}");
    }

    // ---------------- UI MESSAGE ----------------

    private void CreateMessage(string message)
    {
        if (messagePrefab == null || messageContainer == null)
            return;

        GameObject msg = Instantiate(messagePrefab, messageContainer);

        TMP_Text text = msg.GetComponentInChildren<TMP_Text>();
        if (text != null)
            text.text = message;

        Canvas.ForceUpdateCanvases();

        if (scrollRect != null)
            scrollRect.verticalNormalizedPosition = 0f;
    }
}