using Fusion;
using UnityEngine;

/// <summary>
/// 플레이어 한 명의 네트워크 상태를 보관합니다.
///
/// 담당:
/// - 선택한 고양이 종류
/// - 현재 달리기 상태
/// - 사망 상태
/// </summary>
public class PlayerData : NetworkBehaviour
{
    [Networked] public CatType SelectedCatType { get; set; }

    [Networked] public NetworkBool IsSprinting { get; set; }

    [Networked] public NetworkBool IsDead { get; set; }

    public override void Spawned()
    {
        if (!Object.HasStateAuthority)
        {
            return;
        }

        IsSprinting = false;
        IsDead = false;
    }
}