using UnityEngine;

/// <summary>
/// ControllableLight
///
/// 담당:
/// - 실제 맵의 전등 ON/OFF 상태 관리
/// - 해킹 전에는 조작 불가
/// - 해킹 성공 후에는 전등을 켜고 끌 수 있음
///
/// 사용 위치:
/// - 실제 맵의 RoomLight_01 같은 Empty Object에 붙임
///
/// 주의:
/// - 구역 발견 여부는 여기서 관리하지 않음
/// - 발견 여부는 DiscoverableArea가 담당함
/// </summary>
public class ControllableLight : MonoBehaviour
{
    [Header("State")]
    [SerializeField] private bool isUnlocked;
    [SerializeField] private bool isOn = true;

    [Header("Actual Lights")]
    [Tooltip("이 구역에서 실제로 켜고 끌 Light 컴포넌트들")]
    [SerializeField] private Light[] targetLights;

    [Header("Optional Visual")]
    [Tooltip("전등 모델 색을 바꾸고 싶을 때만 연결합니다. 색 변경이 싫으면 비워둬도 됩니다.")]
    [SerializeField] private Renderer[] lampRenderers;

    [Header("Optional Area Overlay")]
    [Tooltip("전등이 꺼졌을 때 실제 월드에 어두운 오브젝트를 켜고 싶으면 연결합니다.")]
    [SerializeField] private GameObject darknessObject;

    [Header("Visual Colors")]
    [SerializeField] private Color lampOnColor = Color.white;
    [SerializeField] private Color lampOffColor = Color.gray;

    public bool IsUnlocked => isUnlocked;
    public bool IsOn => isOn;

    private void Awake()
    {
        if (targetLights == null || targetLights.Length == 0)
        {
            targetLights = GetComponentsInChildren<Light>(true);
        }

        ApplyLightState();
    }

    /// <summary>
    /// 전등 조작을 해금
    /// </summary>
    public void Unlock()
    {
        if (isUnlocked)
        {
            return;
        }

        isUnlocked = true;

        Debug.Log($"[ControllableLight] 전등 해금 완료: {gameObject.name}");
    }

    /// <summary>
    /// 전등 ON
    /// </summary>
    public void TurnOn()
    {
        isOn = true;
        ApplyLightState();

        Debug.Log($"[ControllableLight] 전등 켜짐: {gameObject.name}");
    }

    /// <summary>
    /// 전등 OFF
    /// </summary>
    public void TurnOff()
    {
        isOn = false;
        ApplyLightState();

        Debug.Log($"[ControllableLight] 전등 꺼짐: {gameObject.name}");
    }

    /// <summary>
    /// 해금된 전등의 ON/OFF 상태를 전환
    /// </summary>
    public void ToggleLight()
    {
        if (!isUnlocked)
        {
            Debug.Log($"[ControllableLight] 아직 해금되지 않은 전등입니다: {gameObject.name}");
            return;
        }

        if (isOn)
        {
            TurnOff();
        }
        else
        {
            TurnOn();
        }
    }

    /// <summary>
    /// 현재 상태를 실제 Light와 선택적 시각 요소에 반영
    /// </summary>
    private void ApplyLightState()
    {
        if (targetLights != null)
        {
            for (int i = 0; i < targetLights.Length; i++)
            {
                if (targetLights[i] == null)
                {
                    continue;
                }

                targetLights[i].enabled = isOn;
            }
        }

        // 실제 전등 모델 색을 바꾸고 싶을 때만 사용
        if (lampRenderers != null)
        {
            for (int i = 0; i < lampRenderers.Length; i++)
            {
                if (lampRenderers[i] == null)
                {
                    continue;
                }

                lampRenderers[i].material.color = isOn ? lampOnColor : lampOffColor;
            }
        }

        // 전등이 꺼졌을 때 월드에 어두운 영역 표시용 오브젝트
        if (darknessObject != null)
        {
            darknessObject.SetActive(!isOn);
        }
    }

    [ContextMenu("TEST Toggle Light")]
    private void TestToggleLight()
    {
        isUnlocked = true;
        ToggleLight();
    }
}