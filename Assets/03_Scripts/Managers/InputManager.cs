using System;
using UnityEngine;

/// <summary>
/// 로컬 플레이어의 입력을 수집하여
/// Fusion에 전달할 NetworkInputData를 생성합니다.
///
/// 현재 구현 기능:
/// - WASD 이동
/// *빠른 테스트를 위해 레거시로 구현해두었습니다. 수정해주세요.
/// *싱글톤으로 만들어졌기 때문에 게임 내내 살아있습니다. 맵 씬에서 입력을 받는 경우 / 그 외 씬들에서 입력을 받는 경우를 구분해두어야 합니다.
///
/// TODO:
/// - 마우스 시점 회전
/// - 점프
/// - 아이템과의 상호작용(획득 및 사용)
/// </summary>
public class InputManager : PawntomSingleton<InputManager>
{
    /// <summary>
    /// 현재 프레임의 이동 및 마우스 입력을 수집합니다.
    /// </summary>
    public NetworkInputData GetNetworkInput()
    {
        NetworkInputData data = new NetworkInputData();

        data.Move = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        );

        // data.Look = new Vector2(
        //     Input.GetAxisRaw("Mouse X"),
        //     Input.GetAxisRaw("Mouse Y")
        // );

        return data;
    }
}

