using UnityEngine;
using UnityEngine.InputSystem;

public class SimpleFPSController : MonoBehaviour
{
    [Header("Inventory UI (순정 사각형 UI)")]
    public GameObject myInventoryUI; // ◀ 여기에 새로 만든 사각형 패널을 드래그해서 넣으세요!

    [Header("Movement")]
    public float moveSpeed = 5f;
    public float jumpForce = 5f;
    public float gravity = 9.81f;

    [Header("Look Sensitive")]
    public float mouseSensitivity = 0.1f;

    [Header("Head Bobbing (Walk Motion)")]
    public bool useHeadBob = true;
    public float bobFrequency = 10f;
    public float bobAmount = 0.04f;
    
    [Header("Hand Bobbing (Weapon Motion)")]
    public bool useHandBob = true;
    public float handBobAmountX = 0.02f;
    public float handBobAmountY = 0.01f;
    
    [Header("Assign")]
    public Transform playerHandObject;

    private CharacterController controller;
    private Camera mainCamera;
    private Transform cameraTransform;
    private Vector3 moveDirection;
    private float xRotation = 0f;
    
    private float defaultCamYPos = 0f;
    private Vector3 defaultHandPos;
    private float timer = 0f;

    // 변수 중복 선언 제거 및 하나로 통합
    private bool isInventoryOpen = false;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        mainCamera = GetComponentInChildren<Camera>();
        cameraTransform = mainCamera.transform;

        defaultCamYPos = cameraTransform.localPosition.y;
        if (playerHandObject != null) defaultHandPos = playerHandObject.localPosition;

        // 게임 시작 시 내가 만든 순정 UI는 꺼두고 마우스 커서는 잠급니다.
        if (myInventoryUI != null) myInventoryUI.SetActive(false);
        LockCursor();
    }

    void Update()
    {
        // ==========================================
        // 키보드 [ I ] 키를 누르면 인벤토리를 열고 닫습니다.
        // ==========================================
        if (Keyboard.current.iKey.wasPressedThisFrame)
        {
            ToggleInventory();
        }

        // ★인벤토리가 열려있을 때는 아래의 움직임/시선 처리 계산을 통째로 건너뜁니다! (화면 안 돌아감)
        if (isInventoryOpen) return;

        // 1. 신형 마우스 입력 (시선 처리)
        Vector2 mouseDelta = Mouse.current.delta.ReadValue() * mouseSensitivity;
        transform.Rotate(Vector3.up * mouseDelta.x);

        xRotation -= mouseDelta.y;
        xRotation = Mathf.Clamp(xRotation, -45f, 45f);
        cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        // 2. 신형 키보드 입력
        float moveX = 0f; float moveZ = 0f;
        if (Keyboard.current.wKey.isPressed) moveZ = 1f;
        if (Keyboard.current.sKey.isPressed) moveZ = -1f;
        if (Keyboard.current.dKey.isPressed) moveX = 1f;
        if (Keyboard.current.aKey.isPressed) moveX = -1f;

        Vector3 move = (transform.right * moveX) + (transform.forward * moveZ);

        // 3. 땅에 있을 때 & 점프
        if (controller.isGrounded)
        {
            moveDirection = move * moveSpeed;
            if (Keyboard.current.spaceKey.wasPressedThisFrame) moveDirection.y = jumpForce;
        }
        else
        {
            moveDirection.x = move.x * moveSpeed;
            moveDirection.z = move.z * moveSpeed;
        }

        moveDirection.y -= gravity * Time.deltaTime;
        controller.Move(moveDirection * Time.deltaTime);

        // 4. 흔들림 처리
        HandleBobbing(moveX, moveZ);
    }

    // 인벤토리를 켜고 끄는 핵심 함수 (순정 UI로 재조립)
    void ToggleInventory()
    {
        if (myInventoryUI == null) return;

        isInventoryOpen = !isInventoryOpen; // 상태 뒤집기
        myInventoryUI.SetActive(isInventoryOpen); // 우리가 만든 사각형 UI 껐다 켜기

        if (isInventoryOpen)
        {
            UnlockCursor(); // 인벤토리 열리면 마우스 자유롭게 풀기
        }
        else
        {
            LockCursor(); // 인벤토리 닫히면 마우스 다시 중앙에 가두기
        }
    }

    void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void HandleBobbing(float moveX, float moveZ)
    {
        if (controller.isGrounded && (Mathf.Abs(moveX) > 0.1f || Mathf.Abs(moveZ) > 0.1f))
        {
            timer += Time.deltaTime * bobFrequency;
            if (useHeadBob)
            {
                float newCamY = defaultCamYPos + Mathf.Sin(timer) * bobAmount;
                cameraTransform.localPosition = new Vector3(cameraTransform.localPosition.x, newCamY, cameraTransform.localPosition.z);
            }
            if (useHandBob && playerHandObject != null)
            {
                float newHandX = defaultHandPos.x + Mathf.Cos(timer) * handBobAmountX;
                float newHandY = defaultHandPos.y + Mathf.Sin(timer * 2f) * handBobAmountY;
                playerHandObject.localPosition = new Vector3(newHandX, newHandY, playerHandObject.localPosition.z);
            }
        }
        else
        {
            timer = 0f;
            float targetCamY = Mathf.Lerp(cameraTransform.localPosition.y, defaultCamYPos, Time.deltaTime * 10f);
            cameraTransform.localPosition = new Vector3(cameraTransform.localPosition.x, targetCamY, cameraTransform.localPosition.z);
            if (playerHandObject != null) playerHandObject.localPosition = Vector3.Lerp(playerHandObject.localPosition, defaultHandPos, Time.deltaTime * 10f);
        }
    }
}