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
}