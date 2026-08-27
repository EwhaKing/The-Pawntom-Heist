using System;
using UnityEngine;

namespace Pawntom.Enemy.Core
{
    /// <summary>
    /// 이동 관련 수치. 상태와 무관하게 "몸이 어떻게 움직이는가" 를 정한다.
    /// </summary>
    [Serializable]
    public class K9MovementSettings
    {
        // 속도의 정본은 아래 세 필드다. 씬의 Nav Mesh Agent 에 보이는 Speed 는 파생값이라,
        // 이동 명령이 나갈 때마다 아래 값으로 덮어써진다. 툴팁에 같은 내용을 적어 둔다.
        [Tooltip("순찰 중 이동 속도. 속도의 정본은 여기다 — 이 값이 Nav Mesh Agent 의 Speed 를 덮어쓴다. 속도는 여기서 조정한다")]
        [SerializeField] private float _patrolSpeed = 2.0f;

        [Tooltip("추격 중 이동 속도. 순찰보다 빨라야 한다. 속도의 정본은 여기다 — 이 값이 Nav Mesh Agent 의 Speed 를 덮어쓴다. 속도는 여기서 조정한다")]
        [SerializeField] private float _chaseSpeed = 4.5f;

        [Tooltip("조사 중 이동 속도. 순찰·추격과 따로 조절한다")]
        [SerializeField] private float _investigateSpeed = 1.5f;

        [Tooltip("이동 가속도(m/s²). 낮으면 방향을 튼 뒤 다시 속도를 내는 데 오래 걸린다. 추격 속도 ÷ 이 값 = 최고 속도까지 걸리는 초")]
        [SerializeField] private float _acceleration = 30f;

        [Tooltip("이동 중 몸이 도는 속도(도/초). 이동 명령과 함께 나간다. 경계(Alert) 그룹의 Turn Speed Degrees — 제자리 조준 회전 — 와는 다른 값이다")]
        [SerializeField] private float _turnSpeedDegrees = 540f;

        public float PatrolSpeed { get { return _patrolSpeed; } }

        public float ChaseSpeed { get { return _chaseSpeed; } }

        public float InvestigateSpeed { get { return _investigateSpeed; } }

        public float Acceleration { get { return _acceleration; } }

        public float TurnSpeedDegrees { get { return _turnSpeedDegrees; } }
    }

    /// <summary>
    /// 감지 수치. 시야·접촉 판정과 시야 기억을 함께 둔다.
    /// </summary>
    [Serializable]
    public class K9PerceptionSettings
    {
        [Tooltip("시야 거리(m)")]
        [SerializeField] private float _sightRange = 12f;

        [Tooltip("시야각 전체 폭(도). 정면 기준 좌우 절반씩 나뉜다")]
        [SerializeField] private float _sightAngle = 110f;

        [Tooltip("접촉·초근접으로 판정하는 거리(m)")]
        [SerializeField] private float _contactDistance = 1.5f;

        [Tooltip("시야에서 사라진 뒤에도 '본 것'으로 취급하는 시간(초). 0이면 기억하지 않고 이번 틱의 시야만 본다")]
        [SerializeField] private float _sightMemorySeconds = 1.0f;

        public float SightRange { get { return _sightRange; } }

        public float SightAngle { get { return _sightAngle; } }

        public float ContactDistance { get { return _contactDistance; } }

        public float SightMemorySeconds { get { return _sightMemorySeconds; } }
    }

    /// <summary>
    /// Patrol 상태 수치.
    /// </summary>
    [Serializable]
    public class K9PatrolSettings
    {
        [Tooltip("웨이포인트 도달로 인정하는 거리(m)")]
        [SerializeField] private float _arriveDistance = 0.5f;

        [Tooltip("웨이포인트 도달 후 다음으로 넘어가기까지의 대기 시간(초)")]
        [SerializeField] private float _waitSeconds = 1.0f;

        public float ArriveDistance { get { return _arriveDistance; } }

        public float WaitSeconds { get { return _waitSeconds; } }
    }

    /// <summary>
    /// Alert 상태 수치. 하울링과 소집도 경계 중에 일어나므로 여기 둔다.
    /// </summary>
    [Serializable]
    public class K9AlertSettings
    {
        [Tooltip("하울링 지속 시간(초). 이 시간이 지나도 시야를 못 잡으면 조사로 전환한다")]
        [SerializeField] private float _howlDurationSeconds = 1.5f;

        [Tooltip("하울링 소집 반경(m). 기획서 명시값 30")]
        [SerializeField] private float _howlRadius = 30f;

        [Tooltip("하울링 후 이 시간 안에 시야를 다시 잡으면 하울링을 반복하지 않고 곧바로 추격한다(초)")]
        [SerializeField] private float _howlCooldownSeconds = 8f;

        [Tooltip("경계 중 목표 쪽으로 제자리에서 몸을 돌리는 속도(도/초). 0 이하면 돌지 않는다. 이동(Movement) 그룹의 Turn Speed Degrees — 이동 중 몸 회전 — 와는 다른 값이다")]
        [SerializeField] private float _turnSpeedDegrees = 360f;

        [Tooltip("경계 진입 후 제자리를 지키는 시간(초). 이 시간이 지나면 시야가 있는 즉시 추격한다. 0이면 하울링을 시작하자마자 달려든다")]
        [SerializeField] private float _holdSeconds = 0f;

        [Tooltip("소집 좌표 주변으로 흩어지는 반경(m). 0이면 분산하지 않고 소집 좌표로 그대로 간다")]
        [SerializeField] private float _summonSpreadRadius = 3f;

        public float HowlDurationSeconds { get { return _howlDurationSeconds; } }

        public float HowlRadius { get { return _howlRadius; } }

        public float HowlCooldownSeconds { get { return _howlCooldownSeconds; } }

        public float TurnSpeedDegrees { get { return _turnSpeedDegrees; } }

        public float HoldSeconds { get { return _holdSeconds; } }

        public float SummonSpreadRadius { get { return _summonSpreadRadius; } }
    }

    /// <summary>
    /// Investigate 상태 수치. 포기 시간과 배회 규칙.
    /// </summary>
    [Serializable]
    public class K9InvestigateSettings
    {
        [Tooltip("조사 중 신규 감지가 없을 때 순찰로 복귀하기까지의 시간(초). 기획서 명시값 20")]
        [SerializeField] private float _giveUpSeconds = 20f;

        [Tooltip("조사 기준점에서 배회할 수 있는 반경(m)")]
        [SerializeField] private float _wanderRadius = 3f;

        [Tooltip("배회 지점에 도달한 뒤 다음 지점을 고르기까지의 대기 시간(초)")]
        [SerializeField] private float _wanderIntervalSeconds = 2f;

        [Tooltip("고른 지점이 갈 수 없는 곳일 때 근처에서 갈 수 있는 곳을 찾는 허용 거리(m)")]
        [SerializeField] private float _wanderSampleDistance = 1.5f;

        public float GiveUpSeconds { get { return _giveUpSeconds; } }

        public float WanderRadius { get { return _wanderRadius; } }

        public float WanderIntervalSeconds { get { return _wanderIntervalSeconds; } }

        public float WanderSampleDistance { get { return _wanderSampleDistance; } }
    }

    /// <summary>
    /// Chase 상태 수치. 지금은 하나뿐이지만 추격 수치가 늘어날 자리다.
    /// </summary>
    [Serializable]
    public class K9ChaseSettings
    {
        [Tooltip("추격 중 시야를 잃고 조사로 복귀하기까지의 시간(초). 기획서 명시값 5")]
        [SerializeField] private float _loseSightSeconds = 5f;

        public float LoseSightSeconds { get { return _loseSightSeconds; } }
    }

    /// <summary>
    /// K-9 한 마리의 수치 설정.
    /// <para>
    /// 별도 애셋 파일 없이 인스펙터에서 개체마다 다른 값을 줄 수 있도록
    /// <see cref="SerializableAttribute"/> 를 붙인 일반 클래스로 둔다.
    /// </para>
    /// <para>
    /// 수치는 상태 머신의 상태 이름과 같은 이름의 그룹으로 나눠 둔다 —
    /// "Chase 가 느리다" 면 <see cref="Chase"/> 를 연다.
    /// 상태와 무관한 공통 수치는 <see cref="Movement"/> 와 <see cref="Perception"/> 에 있다.
    /// </para>
    /// <para>
    /// 기본값 출처 — 20초 / 30m / 5초는 GAME_DESIGN.md 3.3 명시값,
    /// 나머지는 TASK-002 2.2 에서 정한 시작값이며 L3(감각) 에서 사용자가 조정한다.
    /// </para>
    /// </summary>
    [Serializable]
    public class K9Settings
    {
        [SerializeField] private K9MovementSettings _movement = new K9MovementSettings();
        [SerializeField] private K9PerceptionSettings _perception = new K9PerceptionSettings();
        [SerializeField] private K9PatrolSettings _patrol = new K9PatrolSettings();
        [SerializeField] private K9AlertSettings _alert = new K9AlertSettings();
        [SerializeField] private K9InvestigateSettings _investigate = new K9InvestigateSettings();
        [SerializeField] private K9ChaseSettings _chase = new K9ChaseSettings();

        public K9MovementSettings Movement { get { return _movement; } }

        public K9PerceptionSettings Perception { get { return _perception; } }

        public K9PatrolSettings Patrol { get { return _patrol; } }

        public K9AlertSettings Alert { get { return _alert; } }

        public K9InvestigateSettings Investigate { get { return _investigate; } }

        public K9ChaseSettings Chase { get { return _chase; } }
    }
}
