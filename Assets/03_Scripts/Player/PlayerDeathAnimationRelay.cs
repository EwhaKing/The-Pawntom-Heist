using UnityEngine;

/// <summary>쓰러짐 Animator가 있는 오브젝트에 붙이고 마지막 프레임 Animation Event에 같은 이름을 연결합니다.</summary>
public sealed class PlayerDeathAnimationRelay : MonoBehaviour
{
    public void NotifyDeathAnimationFinished()
    {
        PlayerData player = GetComponentInParent<PlayerData>();
        if (player != null) player.NotifyDeathAnimationFinished();
    }
}
