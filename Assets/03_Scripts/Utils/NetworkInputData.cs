using Fusion;
using UnityEngine;

public enum InputButton
{
    Jump = 0,
    Sprint = 1, //달리기
    Interact = 2,
    // UseItem = 3,
    // DropItem = 4

    //Attack = 5 //TODO : 기획에 따라 추가하기
}

public struct NetworkInputData : INetworkInput
{
    public Vector2 Move; // WASD 이동 입력
    public Vector2 Look; // 마우스 시점 회전 입력

    public NetworkButtons Buttons;

    // 아래 셋은 "한 번만" 전달되는 입력이다. InputManager 가 프레임마다 모아 두었다가 다음 입력 수집 때 한 번 보내고 비운다
    // (틱과 프레임이 어긋나 눌림이 사라지거나 두 번 들어가는 것을 막는다).

    /// <summary>숫자키로 고른 슬롯(0~3). 없으면 -1.</summary>
    public int SelectedSlotIndex;

    /// <summary>휠로 넘긴 칸 수. 양수 = 다음 칸, 음수 = 이전 칸, 0 = 없음.</summary>
    public int SlotScrollDirection;

    /// <summary>Q: 선택한 슬롯의 아이템 버리기.</summary>
    public NetworkBool DropItem;
}