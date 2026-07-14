using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkCharacterController))]
public class PlayerController : NetworkBehaviour
{
    [Header("Camera")]
    [SerializeField] private Camera playerCamera;

    private NetworkCharacterController _controller;

    private void Awake()
    {
        _controller = GetComponent<NetworkCharacterController>();
    }

    public override void Spawned()
    {
        bool isLocalPlayer = Object.HasInputAuthority;

        if (playerCamera != null)
        {
            playerCamera.enabled = isLocalPlayer;

            AudioListener listener =
                playerCamera.GetComponent<AudioListener>();

            if (listener != null)
            {
                listener.enabled = isLocalPlayer;
            }
        }

        if (isLocalPlayer)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!GetInput(out NetworkInputData input))
        {
            return;
        }

        Vector2 moveInput =
            Vector2.ClampMagnitude(input.Move, 1f);

        Vector3 moveDirection =
            transform.forward * moveInput.y +
            transform.right * moveInput.x;

        moveDirection.y = 0f;

        _controller.Move(moveDirection);
    }
}