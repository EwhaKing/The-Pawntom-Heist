namespace Pawntom.Tutorial.Core
{
    /// <summary>
    /// 튜토리얼 진행을 움직이는 게임 쪽 사건. 대본의 "수신 시점" 열과 키 안내 문구의 "같은 행동"을 표현한다.
    /// <para>
    /// 대부분은 <b>상태형</b>이다 — 조건이 참인 동안 수집기가 계속 보낸다(예: 본부 안에 있음, 스태미나 절반 이하).
    /// 그래서 신호가 비트보다 먼저 와서 버려져도, 비트가 열리면 다음 수집 주기에 다시 들어온다.
    /// 한 번만 일어나는 <b>사건형</b>(해킹 실패, 가방 가득 참 등)은 주석에 표시했다.
    /// </para>
    /// </summary>
    public enum TutorialSignal
    {
        None = 0,

        // ── 흐름 ──
        /// <summary>오프닝 눈 뜨기 연출이 끝났다. 사건형.</summary>
        EyesOpened,

        // ── 2단계 기본 조작 ──
        /// <summary>조작 가능 전환 뒤 일정 거리 이동 + 일정 각도 회전.</summary>
        MovedAndLooked,
        /// <summary>내 털공 흔적이 하나 이상 있다.</summary>
        FirstFootprint,
        /// <summary>기준 방향(마지막 메인 비트 시작 시점)에서 크게 돌아섰다.</summary>
        LookedBack,
        SprintStarted,
        StaminaBelowHalf,
        /// <summary>점프 입력. 사건형.</summary>
        Jumped,
        ObstacleAhead,
        CorridorAEndReached,

        // ── 3단계 첫 마주침 ──
        NearSecurityNode,
        /// <summary>분전반 캐스팅을 시작했다. 사건형.</summary>
        SecurityNodeCastStarted,
        /// <summary>캐스팅 도중 K-9 접근 연출 지점. 사건형.</summary>
        SecurityNodeThreatCue,
        SecurityNodeConnected,
        EnemyChasing,
        StaminaBelow30,
        /// <summary>본부 안에 있고 추격 중인 적이 없다.</summary>
        SafeInMainbase,

        // ── 4단계 오퍼레이터 전환 ──
        NearTerminal,
        TerminalOpened,
        /// <summary>CCTV(위치) 탭을 일정 시간 확인했다.</summary>
        CctvChecked,
        /// <summary>격벽(일반 등급) 해킹 시작. 사건형.</summary>
        DoorHackStarted,
        /// <summary>사건형.</summary>
        DoorHackSucceeded,
        /// <summary>사건형.</summary>
        DoorHackFailed,
        /// <summary>단말기를 닫았다. 사건형.</summary>
        TerminalClosed,

        // ── 5단계 파밍 ──
        BlueDoorAhead,
        /// <summary>격벽이 닫힘→열림으로 바뀌었다. 사건형.</summary>
        DoorOpened,
        LootRoomEntered,
        NearLoot,
        /// <summary>전리품을 하나 이상 주웠다.</summary>
        ItemLooted,
        InventoryThreeFilled,
        /// <summary>가방이 가득 차 루팅이 거절됐다. 사건형.</summary>
        InventoryFullRejected,
        /// <summary>슬롯이 비었다(버리기). 사건형.</summary>
        ItemDropped,
        LootRoomExiting,

        // ── 6단계 금고 복도 ──
        VaultCorridorEntered,
        VaultRoomEntered,

        // ── 7단계 캣닢 ──
        VaultFront,
        /// <summary>사건형.</summary>
        VaultHackStarted,
        /// <summary>사건형.</summary>
        VaultHackSucceeded,
        /// <summary>사건형.</summary>
        VaultHackFailed,
        CatnipAcquired,
        PlayerInMainbase,

        // ── 8단계 클로징 ──
        SettlementShown,
        /// <summary>정산 결과 집계 연출이 끝났다.</summary>
        SettlementRevealed,

        // ── 상황별 반복 대사 ── (모두 사건형: 조건을 넘는 순간 한 번)
        /// <summary>10초간 이동 입력 없음(대사·UI·해킹 중에는 재지 않는다).</summary>
        IdleNoMove,
        /// <summary>단말기를 연 채 15초간 조작 없음.</summary>
        IdleAtTerminal,
        /// <summary>금고 앞에서 10초간 해킹을 시작하지 않음.</summary>
        IdleAtVault,
        /// <summary>적이 Chase 에서 Investigate 로 돌아갔다(본부 안이면 보내지 않는다 — 3-6 이 맡는다).</summary>
        ChaseEnded,
        /// <summary>체력이 30% 이하로 떨어졌다.</summary>
        HealthLow,
        /// <summary>사망 판정.</summary>
        PlayerDied,
        /// <summary>사망 후 다시 움직일 수 있게 됐다(부활 또는 재스폰).</summary>
        PlayerRespawned
    }
}
