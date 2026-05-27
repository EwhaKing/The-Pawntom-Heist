using UnityEngine;

public class HandBob : MonoBehaviour
{
    public CharacterController controller;

    [Header("Position Bob")]
    public float bobSpeed = 10f;
    public float bobAmount = 0.05f;

    [Header("Rotation Bob")]
    public float tiltAmount = 10f;   // 좌우 기울기
    public float rollAmount = 3f;   // 앞뒤 회전 느낌

    [Header("Smoothing")]
    public float smooth = 10f;

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
        float speed = new Vector3(controller.velocity.x, 0, controller.velocity.z).magnitude;

        if (speed > 0.1f && controller.isGrounded)
        {
            timer += Time.deltaTime * bobSpeed * (speed / 5f);

            float bobX = Mathf.Sin(timer) * bobAmount;
            float bobY = Mathf.Abs(Mathf.Cos(timer)) * bobAmount;

            // 👉 위치 흔들림
            Vector3 targetPos = startPos + new Vector3(bobX, bobY, 0);
            transform.localPosition = Vector3.Lerp(
                transform.localPosition,
                targetPos,
                Time.deltaTime * smooth
            );

            // 👉 회전 흔들림 (핵심)
            float tilt = Mathf.Sin(timer) * tiltAmount;
            float roll = Mathf.Cos(timer) * rollAmount;

            Quaternion targetRot = startRot * Quaternion.Euler(roll, 0, tilt);

            transform.localRotation = Quaternion.Slerp(
                transform.localRotation,
                targetRot,
                Time.deltaTime * smooth
            );
        }
        else
        {
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