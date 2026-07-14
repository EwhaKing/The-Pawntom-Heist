using UnityEngine;

public class HandBob : MonoBehaviour
{
    public CharacterController controller;

    [Header("Position Bob")]
    public float bobSpeed = 6f;
    public float bobAmount = 0.08f;

    [Header("Rotation Bob")]
    public float tiltAmount = 15f;   // 좌우 기울기
    public float rollAmount = 5f;   // 앞뒤 회전 느낌

    [Header("Smoothing")]
    public float smooth = 5f;

    private Vector3 startPos;
    private Quaternion startRot;
    private float timer;

    void Start()
    {
        startPos = transform.localPosition;
        startRot = transform.localRotation;
    }

    void Update()
    {
        // 평면상의 현재 이동 속도를 구합니다.
        float speed = new Vector3(controller.velocity.x, 0, controller.velocity.z).magnitude;

        if (speed > 0.1f && controller.isGrounded)
        {
            // 🔥 [수정] 속도에 지나치게 영향을 받지 않도록 (speed/5f) 제거
            // 오직 bobSpeed에 의해서만 일정한 리듬으로 타이머가 돌아갑니다.
            timer += Time.deltaTime * bobSpeed;

            // 팔이 8자 모양이나 U자 모양을 그리도록 계산
            float bobX = Mathf.Sin(timer) * bobAmount;
            
            // Y축(위아래)은 Sin(timer * 2)를 쓰면 1걸음에 위아래로 2번 왕복하여 훨씬 자연스럽습니다.
            float bobY = Mathf.Sin(timer * 2f) * bobAmount * 0.5f; 

            // 👉 위치 흔들림
            Vector3 targetPos = startPos + new Vector3(bobX, bobY, 0);
            transform.localPosition = Vector3.Lerp(
                transform.localPosition,
                targetPos,
                Time.deltaTime * smooth
            );

            // 👉 회전 흔들림 (핵심)
            float tilt = Mathf.Sin(timer) * tiltAmount;
            float roll = Mathf.Cos(timer * 2f) * rollAmount; // 회전도 Y축과 리듬을 맞춤

            Quaternion targetRot = startRot * Quaternion.Euler(roll, 0, tilt);

            transform.localRotation = Quaternion.Slerp(
                transform.localRotation,
                targetRot,
                Time.deltaTime * smooth
            );
        }
        else
        {
            // 멈췄을 때 타이머 초기화 (원래 자리로 부드럽게 복귀)
            timer = 0;

            transform.localPosition = Vector3.Lerp(
                transform.localPosition,
                startPos,
                Time.deltaTime * smooth
            );

            transform.localRotation = Quaternion.Slerp(
                transform.localRotation,
                startRot,
                Time.deltaTime * smooth
            );
        }
    }
}