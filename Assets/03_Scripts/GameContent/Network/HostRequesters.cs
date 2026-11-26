using Fusion;
using Pawntom.Network;
using UnityEngine;

/// <summary>
/// 상태 권한(호스트) 쪽 요청 재검증에 쓰는 씬 조회. 판정 규칙 자체는 순수 C#(<see cref="HostRequestRule"/> 등)에 있다.
/// <para>
/// 클라이언트가 보낸 값은 믿지 않는다. 요청자는 RPC 의 <c>RpcInfo.Source</c>(호스트 자신의 호출이면 <c>Runner.LocalPlayer</c>)로만 정하고,
/// 그 PlayerRef 의 플레이어 오브젝트 위치·생존을 호스트가 직접 본다.
/// </para>
/// <para>
/// 본부 단말기 "사용 중" 여부는 네트워크 상태가 아니다(로컬 UI). 대신 요청자가 단말기 앞이나 본부 구역 안에 있는지로 확인한다.
/// 씬에 단말기·본부 구역이 하나도 없으면 확인할 수 없으므로 위치 조건을 통과시킨다.
/// </para>
/// </summary>
public static class HostRequesters
{
    /// <summary>호스트가 보는 위치는 조금 늦다. 표준 상호작용 거리에 더해 허용하는 여유(m).</summary>
    public const float FieldRangeTolerance = 1.5f;

    /// <summary>단말기 앞 판정 여유(m). 단말기 표준 거리에 더한다.</summary>
    public const float TerminalRangeTolerance = 1.0f;

    /// <summary>같은 요청자의 연속 조작 사이 최소 간격(초).</summary>
    public const float DefaultCooldownSeconds = 0.2f;

    /// <summary>쿨다운 키. PlayerRef 의 인코딩 값.</summary>
    public static int Key(PlayerRef player)
    {
        return player.RawEncoded;
    }

    public static int SecondsToTicks(NetworkRunner runner, float seconds)
    {
        if (runner == null || seconds <= 0f)
        {
            return 0;
        }

        return Mathf.Max(1, Mathf.CeilToInt(seconds * runner.TickRate));
    }

    /// <summary>요청자 PlayerRef 가 입력 권한을 가진 플레이어 오브젝트를 찾는다.</summary>
    public static bool TryResolvePlayer(PlayerRef source, out PlayerData player)
    {
        player = null;

        if (source == PlayerRef.None)
        {
            return false;
        }

        for (int i = 0; i < PlayerData.ActivePlayers.Count; i++)
        {
            PlayerData candidate = PlayerData.ActivePlayers[i];

            if (candidate != null && candidate.Object != null && candidate.Object.IsValid &&
                candidate.Object.InputAuthority == source)
            {
                player = candidate;
                return true;
            }
        }

        return false;
    }

    /// <summary>요청자가 본부 단말기 앞 또는 본부 구역 안에 있는가.</summary>
    public static bool IsAtTerminal(Vector3 position)
    {
        // 요청 때만 부른다(매 프레임 아님). 씬마다 개수가 1~2개라 캐시를 두지 않는다.
        ControlMachine[] machines = Object.FindObjectsByType<ControlMachine>(FindObjectsSortMode.None);
        MainbaseZone[] zones = Object.FindObjectsByType<MainbaseZone>(FindObjectsSortMode.None);

        if (machines.Length == 0 && zones.Length == 0)
        {
            return true;
        }

        for (int i = 0; i < machines.Length; i++)
        {
            if (InteractRange.IsInRange(machines[i], position, InteractRange.StandardMargin + TerminalRangeTolerance))
            {
                return true;
            }
        }

        for (int i = 0; i < zones.Length; i++)
        {
            if (zones[i].ContainsPoint(position))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>본부 원격 조작 요청자의 사실(유효·생존·단말기 앞·쿨다운).</summary>
    public static RequesterFacts ForTerminal(PlayerRef source, RequestCooldown cooldown, int tick)
    {
        bool valid = TryResolvePlayer(source, out PlayerData player);

        return new RequesterFacts
        {
            Valid = valid,
            Dead = valid && player.IsDead,
            InPlace = valid && IsAtTerminal(player.transform.position),
            CoolingDown = cooldown != null && cooldown.IsCoolingDown(Key(source), tick),
        };
    }

    /// <summary>현장 조작 요청자의 사실(유효·생존·대상 근처·쿨다운).</summary>
    public static RequesterFacts ForField(PlayerRef source, Component target, RequestCooldown cooldown, int tick)
    {
        bool valid = TryResolvePlayer(source, out PlayerData player);

        return new RequesterFacts
        {
            Valid = valid,
            Dead = valid && player.IsDead,
            InPlace = valid && InteractRange.IsInRange(target, player.transform.position,
                InteractRange.StandardMargin + FieldRangeTolerance),
            CoolingDown = cooldown != null && cooldown.IsCoolingDown(Key(source), tick),
        };
    }
}
