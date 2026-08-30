using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Hacking
{
    /// <summary>
    /// DigitalSyncGame
    ///
    /// 담당:
    /// - 데바데 발전기 스킬체크처럼 회전하는 바늘을 성공 구간에 맞추는 미니게임
    /// - 바늘이 성공 구간 안에 있을 때 Space 또는 Sync 버튼을 누르면 성공
    /// - 성공 구간 밖에서 누르면 실패
    ///
    /// 사용 위치:
    /// - DigitalSyncGamePrefab 루트 오브젝트에 붙임
    /// - HackingManager의 Hacking Game Pool에 등록해서 사용
    /// </summary>
    public class DigitalSyncGame : HackingGameBase
    {
        public override string DisplayText => "색칠된 구간에 맞춰 SPACE를 누르세요";

        [Header("Needle")]
        [SerializeField] private RectTransform needlePivot;

        [Header("Success Zone")]
        [SerializeField] private RectTransform successZonePivot;
        [SerializeField] private Image successZoneImage;

        [Header("Button")]
        [SerializeField] private Button hitButton;

        [Header("Text")]
        [SerializeField] private TextMeshProUGUI resultText;

        [Header("Difficulty")]
        [SerializeField] private float normalNeedleSpeed = 180f;
        [SerializeField] private float vaultNeedleSpeed = 260f;

        [SerializeField] private float normalSuccessAngleRange = 35f;
        [SerializeField] private float vaultSuccessAngleRange = 22f;

        [Header("Rule")]
        [SerializeField] private int requiredSuccessCount = 1;
        [SerializeField] private int maxMissCount = 1;

        private float currentNeedleAngle;
        private float successCenterAngle;
        private float successAngleRange;
        private float needleSpeed;

        private int successCount;
        private int missCount;

        private bool hasFinished;

        /// <summary>
        /// 미니게임 초기화.
        /// HackingManager가 프리팹을 생성한 뒤 호출합니다.
        /// </summary>
        public override void InitGame(SecurityLevel level)
        {
            base.InitGame(level);

            hasFinished = false;
            successCount = 0;
            missCount = 0;
            currentNeedleAngle = 0f;

            SetDifficulty(level);
            SetRandomSuccessZone();

            if (hitButton != null)
            {
                hitButton.onClick.RemoveAllListeners();
                hitButton.onClick.AddListener(TrySync);
            }

            if (resultText != null)
            {
                resultText.text = "색칠된 구간에 맞춰 누르세요";
            }

            Debug.Log($"[DigitalSyncGame] 시작. Speed={needleSpeed}, Range={successAngleRange}");
        }

        /// <summary>
        /// HackingGameBase에서 매 프레임 호출하는 입력 처리 함수입니다.
        /// DigitalSyncGame에서는 바늘 회전과 Space 입력 판정을 처리합니다.
        /// </summary>
        protected override void HandleInput()
        {
            if (!IsActive || hasFinished)
            {
                return;
            }

            RotateNeedle();

            if (Input.GetKeyDown(KeyCode.Space))
            {
                TrySync();
            }
        }

        /// <summary>
        /// 보안 등급에 따라 난이도를 설정합니다.
        /// </summary>
        private void SetDifficulty(SecurityLevel level)
        {
            if (level == SecurityLevel.VaultFinal)
            {
                needleSpeed = vaultNeedleSpeed;
                successAngleRange = vaultSuccessAngleRange;
            }
            else
            {
                needleSpeed = normalNeedleSpeed;
                successAngleRange = normalSuccessAngleRange;
            }
        }

        /// <summary>
        /// 성공 구간을 랜덤 각도로 배치합니다.
        /// </summary>
        private void SetRandomSuccessZone()
        {
            successCenterAngle = Random.Range(0f, 360f);

            if (successZonePivot != null)
            {
                successZonePivot.localRotation = Quaternion.Euler(0f, 0f, -successCenterAngle);
            }

            if (successZoneImage != null)
            {
                successZoneImage.color = new Color(0.2f, 1f, 0.25f, 0.9f);
            }
        }

        /// <summary>
        /// 바늘을 회전시킵니다.
        /// </summary>
        private void RotateNeedle()
        {
            currentNeedleAngle += needleSpeed * Time.deltaTime;

            if (currentNeedleAngle >= 360f)
            {
                currentNeedleAngle -= 360f;
            }

            if (needlePivot != null)
            {
                needlePivot.localRotation = Quaternion.Euler(0f, 0f, -currentNeedleAngle);
            }
        }

        /// <summary>
        /// 현재 바늘이 성공 구간 안에 있는지 검사합니다.
        /// </summary>
        private bool IsNeedleInsideSuccessZone()
        {
            float difference = Mathf.Abs(Mathf.DeltaAngle(currentNeedleAngle, successCenterAngle));

            return difference <= successAngleRange * 0.5f;
        }

        /// <summary>
        /// 플레이어가 타이밍 입력을 시도합니다.
        /// 성공 구간 안이면 성공, 밖이면 실패 처리합니다.
        /// </summary>
        public void TrySync()
        {
            if (!IsActive || hasFinished)
            {
                return;
            }

            bool isSuccessTiming = IsNeedleInsideSuccessZone();

            if (isSuccessTiming)
            {
                successCount++;

                Debug.Log($"[DigitalSyncGame] 싱크 성공 {successCount}/{requiredSuccessCount}");

                if (resultText != null)
                {
                    resultText.text = "SYNC 성공!";
                }

                if (successCount >= requiredSuccessCount)
                {
                    CompleteDigitalSyncGame(true);
                    return;
                }

                SetRandomSuccessZone();
            }
            else
            {
                missCount++;

                Debug.Log($"[DigitalSyncGame] 싱크 실패 {missCount}/{maxMissCount}");

                if (resultText != null)
                {
                    resultText.text = "타이밍 실패!";
                }

                if (missCount >= maxMissCount)
                {
                    CompleteDigitalSyncGame(false);
                }
            }
        }

        /// <summary>
        /// HackingPopupView에서 가상 입력을 보낼 때를 위한 함수입니다.
        /// </summary>
        public override void ReceiveVirtualInput(KeyCode key)
        {
            if (key == KeyCode.Space)
            {
                TrySync();
            }
        }

        /// <summary>
        /// DigitalSyncGame 전용 종료 처리.
        /// 실제 해킹 결과 전달은 HackingGameBase의 FinishGame을 호출합니다.
        /// </summary>
        private void CompleteDigitalSyncGame(bool isSuccess)
        {
            if (hasFinished)
            {
                return;
            }

            hasFinished = true;

            Debug.Log($"[DigitalSyncGame] 결과: {(isSuccess ? "성공" : "실패")}");

            // 부모 클래스 HackingGameBase에 있는 종료 함수 호출
            FinishGame(isSuccess);
        }

        private void OnDestroy()
        {
            if (hitButton != null)
            {
                hitButton.onClick.RemoveListener(TrySync);
            }
        }
    }
}