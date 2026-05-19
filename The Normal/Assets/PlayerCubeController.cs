using UnityEngine;
using Unity.Netcode;
using System.Collections;
using Unity.Collections;
using TMPro;

[RequireComponent(typeof(CharacterController))]
public class PlayerCubeController : NetworkBehaviour
{
    public float moveSpeed = 5f;
    public float gravity = -9.81f;

    private CharacterController controller;
    private Vector3 velocity;

    private Vector2 moveInput;

    private bool canMove = false;
    private float spawnLockTime = 1.5f;

    // 🔥 NETWORK NAME
    public NetworkVariable<FixedString32Bytes> PlayerName =
        new NetworkVariable<FixedString32Bytes>();

    // ✅ NEW: TEXT FIELD (drag in inspector)
    [Header("Name Tag UI")]
    [SerializeField] private TMP_Text nameText;

    // cache camera (for billboard)
    private Transform cam;

    [ServerRpc]
    public void SendLookInputServerRpc(float mouseX)
    {
        transform.Rotate(Vector3.up * mouseX);
    }

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            CameraMovement camMove = FindObjectOfType<CameraMovement>();
            Transform t = transform.Find("CameraTarget");

            if (camMove != null)
                camMove.SetTarget(t != null ? t : transform);

            MultiplayerMenu menu = FindObjectOfType<MultiplayerMenu>();
            if (menu != null)
            {
                SetNameServerRpc(menu.GetPlayerName());
            }
        }

        // 🔥 subscribe to name changes (ALL clients)
        PlayerName.OnValueChanged += OnNameChanged;

        // initial set
        UpdateNameVisual(PlayerName.Value.ToString());

        StartCoroutine(EnableMovementAfterDelay());
    }

    private void OnDestroy()
    {
        PlayerName.OnValueChanged -= OnNameChanged;
    }

    private void OnNameChanged(FixedString32Bytes oldName, FixedString32Bytes newName)
    {
        UpdateNameVisual(newName.ToString());
    }

    private void UpdateNameVisual(string name)
    {
        if (nameText == null)
            return;

        nameText.text = string.IsNullOrEmpty(name) ? "Player" : name;
    }

    private IEnumerator EnableMovementAfterDelay()
    {
        canMove = false;
        yield return new WaitForSeconds(spawnLockTime);
        canMove = true;
    }

    private void Update()
    {
        if (!canMove || GlobalChatUI.IsTyping)
            return;

        // movement alleen voor owner (blijft zoals jij had)
        if (IsOwner)
        {
            moveInput = new Vector2(
                Input.GetAxisRaw("Horizontal"),
                Input.GetAxisRaw("Vertical")
            );
        }

        // 🔥 BILLBOARD VOOR IEDEREEN
        if (nameText != null)
        {
            Camera cam = Camera.main;

            if (cam != null)
            {
                Vector3 dir = nameText.transform.position - cam.transform.position;
                dir.y = 0f; // optioneel: alleen horizontaal draaien

                nameText.transform.rotation = Quaternion.LookRotation(dir);
            }
        }
    }

    private void FixedUpdate()
    {
        if (!IsOwner || !canMove || GlobalChatUI.IsTyping)
            return;

        MoveServerRpc(moveInput);
    }

    [ServerRpc]
    private void MoveServerRpc(Vector2 input)
    {
        Vector3 move =
            transform.right * input.x +
            transform.forward * input.y;

        controller.Move(move * moveSpeed * Time.fixedDeltaTime);

        if (controller.isGrounded && velocity.y < 0)
            velocity.y = -2f;

        velocity.y += gravity * Time.fixedDeltaTime;
        controller.Move(velocity * Time.fixedDeltaTime);
    }

    [ServerRpc]
    private void SetNameServerRpc(string name)
    {
        PlayerName.Value = name;
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        if (IsOwner)
        {
            CameraMovement cam = FindObjectOfType<CameraMovement>();
            if (cam != null)
                cam.ResetCameraToMenu();

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        Destroy(gameObject);
    }
}