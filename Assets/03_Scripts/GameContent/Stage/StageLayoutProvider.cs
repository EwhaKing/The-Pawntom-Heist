using Pawntom.Stage;
using UnityEngine;

/// <summary>
/// 씬의 레벨 데이터 조회 진입점. 씬에 1개 두고 <see cref="LevelLayoutData"/> 를 꽂는다.
/// <para>
/// 네트워크 코드는 없다. 시드 공유·스폰은 상태 권한 쪽 코드가 <see cref="Layout"/> 과
/// <see cref="StageRules.CreatePlan"/> 으로 한다(같은 에셋 + 같은 시드 = 모든 피어에서 같은 배치).
/// </para>
/// <para>
/// <see cref="Current"/> 는 이 컴포넌트가 켜져 있는 동안만 유효하다. 씬이 언로드되면 OnDisable 에서 해제되고,
/// 도메인 리로드 없이 플레이를 다시 시작해도 SubsystemRegistration 에서 비운다.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Pawntom/Level/Stage Layout Provider")]
public sealed class StageLayoutProvider : MonoBehaviour
{
    [Tooltip("이 씬(단계·층)의 배치 데이터. 에셋 이름 규칙 LevelLayoutData_<식별자>")]
    [SerializeField] private LevelLayoutData _layoutData;

    private StageLayout _layout;

    /// <summary>현재 씬의 제공자. 없으면 null.</summary>
    public static StageLayoutProvider Current { get; private set; }

    /// <summary>꽂힌 레벨 데이터 에셋. 비어 있을 수 있다.</summary>
    public LevelLayoutData Data
    {
        get { return _layoutData; }
    }

    /// <summary>
    /// 순수 C# 모델. 처음 읽을 때 한 번 변환해 캐시한다. 데이터가 비어 있으면 null.
    /// 반환값은 공유 캐시다 — 고치지 않는다.
    /// </summary>
    public StageLayout Layout
    {
        get
        {
            if (_layout == null && _layoutData != null)
            {
                _layout = _layoutData.ToCore();
            }

            return _layout;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Current = null;
    }

    private void OnEnable()
    {
        if (Current != null && Current != this)
        {
            Debug.LogWarning("[StageLayoutProvider] 씬에 제공자가 둘 이상이다. 나중에 켜진 '" + name + "' 를 쓴다(이전: '" + Current.name + "').", this);
        }

        Current = this;
    }

    private void OnDisable()
    {
        if (Current == this)
        {
            Current = null;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // 인스펙터에서 에셋을 바꾸면 캐시를 버린다.
        _layout = null;
    }
#endif
}
