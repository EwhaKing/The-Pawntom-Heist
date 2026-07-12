using UnityEngine;

namespace Hacking
{
    /// <summary>
    /// HackingManager의 hackingGamePool 리스트 원소 하나.
    /// "이 등급에는 이 미니게임 프리팹이 나올 수 있다"를 표현합니다.
    /// 인스펙터에서 List로 직접 채워넣는 방식(방법 1)을 기준으로 작성했습니다.
    /// 나중에 스테이지가 늘어나면 이 구조를 그대로 ScriptableObject 안으로 옮기면 됩니다.
    /// </summary>
    [System.Serializable]
    public class HackingGameData
    {
        public SecurityLevel securityLevel;
        public HackingGameType gameType;

        [Tooltip("HackingGameBase를 상속한 컴포넌트가 붙어있는 프리팹")]
        public GameObject gamePrefab;
    }
}