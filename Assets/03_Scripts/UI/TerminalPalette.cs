using UnityEngine;

/// <summary>
/// 본부 단말기(CCTV·격벽 지도) 색 팔레트.
/// 해킹 미니게임 UI 에셋(04_Art_Resources/UI/Minigame/MinigameSheet.png)에서 뽑은 색이다 —
/// 어두운 금속 패널 + 민트 강조 + 코랄 경고. 단말기 색을 바꿀 때는 여기만 고친다.
/// </summary>
public static class TerminalPalette
{
    /// <summary>가장 어두운 바탕·외곽(#0D0D0D).</summary>
    public static readonly Color Black = new Color(0.051f, 0.051f, 0.051f, 1f);

    /// <summary>패널 몸통·지도 바닥(#232323).</summary>
    public static readonly Color Dark = new Color(0.137f, 0.137f, 0.137f, 1f);

    /// <summary>버튼·헤더 바탕(#3D3D3D).</summary>
    public static readonly Color Charcoal = new Color(0.239f, 0.239f, 0.239f, 1f);

    /// <summary>패널 테두리·비활성 글자(#545454).</summary>
    public static readonly Color Mid = new Color(0.329f, 0.329f, 0.329f, 1f);

    /// <summary>벽 선·밝은 테두리(#939393).</summary>
    public static readonly Color Light = new Color(0.576f, 0.576f, 0.576f, 1f);

    /// <summary>본문 글자(#E6E6E6).</summary>
    public static readonly Color Text = new Color(0.902f, 0.902f, 0.902f, 1f);

    /// <summary>강조·성공·열림(#10FFB8).</summary>
    public static readonly Color Mint = new Color(0.063f, 1f, 0.722f, 1f);

    /// <summary>경고·잠김·적(#FF4E56).</summary>
    public static readonly Color Coral = new Color(1f, 0.306f, 0.337f, 1f);

    /// <summary>해금(닫힘)·아군 위치(#2F8CFF). 에셋에 파랑이 없어 민트·코랄과 어울리는 밝은 파랑으로 둔다.</summary>
    public static readonly Color Blue = new Color(0.184f, 0.549f, 1f, 1f);
}
