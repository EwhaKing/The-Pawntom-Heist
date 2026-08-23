using UnityEngine;

/// <summary>
/// MainbaseManager
///
/// 담당:
/// - Mainbase 안에 황금 캣닢이 있는지 관리
/// - 출발 버튼을 눌렀을 때 출발 가능 여부 판단
///
/// 사용 위치:
/// - Mainbase 오브젝트에 붙임
/// </summary>
public class MainbaseManager : MonoBehaviour
{
    public static MainbaseManager Instance { get; private set; }

    [Header("Required Item")]
    [SerializeField] private int ExitItemId = 1;

    private int ExitCountInBase;

    public bool HasExit => ExitCountInBase > 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    /// <summary>
    /// Mainbase 안에 황금 캣닢이 들어왔을 때 호출
    /// </summary>
    public void RegisterCatnip()
    {
        ExitCountInBase++;
        Debug.Log($"[MainbaseManager] 황금 캣닢 감지됨. Count={ExitCountInBase}");
    }

    /// <summary>
    /// Mainbase 밖으로 황금 캣닢이 나갔을 때 호출
    /// </summary>
    public void UnregisterCatnip()
    {
        ExitCountInBase--;

        if (ExitCountInBase < 0)
        {
            ExitCountInBase = 0;
        }

        Debug.Log($"[MainbaseManager] 황금 캣닢 제거됨. Count={ExitCountInBase}");
    }

    /// <summary>
    /// 이 아이템이 황금 캣닢인지 확인
    /// </summary>
    public bool IsGoldenCatnip(int itemId)
    {
        return itemId == ExitItemId;
    }

    /// <summary>
    /// 출발 버튼을 눌렀을 때 호출
    /// </summary>
    public void TryDepart()
    {
        if (!HasExit)
        {
            Debug.Log("[MainbaseManager] 황금 캣닢이 Mainbase 안에 없습니다. 출발 불가");
            return;
        }

        Debug.Log("[MainbaseManager] 황금 캣닢 확인 완료. 출발 성공!");

        // 나중에 여기서 결과 화면, 씬 전환, 정산 처리 연결
        // GameManager.Instance.EscapeSuccess();
    }
}
