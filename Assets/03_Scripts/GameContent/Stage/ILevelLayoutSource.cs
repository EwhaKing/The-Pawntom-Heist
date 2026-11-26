using Pawntom.Stage;

/// <summary>
/// 씬에서 레벨 데이터로 구워지는 배치 원본. 굽기 도구(<c>Pawntom/Level/Bake Layout From Scene</c>)가
/// 열린 씬의 모든 구현체를 찾아 <see cref="AppendTo"/> 를 부른다.
/// <para>
/// 마커(도킹·아이템·열쇠·브루투스·문 보정)가 이것을 구현한다. 이후 만들어질 환풍구·분전반·구역 컴포넌트도
/// 이것을 구현하면 굽기 도구를 고치지 않고 레벨 데이터에 들어간다.
/// </para>
/// <para>
/// ControllableWall·PatrolRoute·EnemySpawnPoint·VaultChest 는 이것을 구현하지 않아도 굽기 도구가 직접 읽는다.
/// 같은 GameObject 에 구현체가 함께 붙어 있으면 직접 읽기는 건너뛰고 구현체의 기록만 쓴다(보정용).
/// </para>
/// 런타임 로직은 두지 않는다 — 편집 시점에만 쓰인다.
/// </summary>
public interface ILevelLayoutSource
{
    /// <summary>자기 배치 항목을 <paramref name="layout"/> 의 해당 목록에 덧붙인다.</summary>
    void AppendTo(StageLayout layout);
}
