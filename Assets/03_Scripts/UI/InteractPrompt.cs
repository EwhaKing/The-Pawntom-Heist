/// <summary>
/// 상호작용 안내 문구("E | 이름")를 화면 중앙 아래에 띄우는 진입점.
///
/// 사용법:
/// - 플레이어가 지금 상호작용할 수 있는 대상 앞에 있는 동안 <b>매 프레임</b> <see cref="Show"/> 를 부른다.
///   부르지 않은 프레임이 지나면 안내는 저절로 사라진다(숨기는 호출이 따로 없다).
/// - 같은 프레임에 여러 곳이 부르면 마지막 호출이 이긴다.
///
/// 실제 UI 는 처음 부를 때 만들어진다(<see cref="InteractPromptView"/>). 씬에 미리 둘 것이 없다.
/// </summary>
public static class InteractPrompt
{
    /// <param name="interactName">대상 이름(예: "금고 해킹"). 비어 있으면 아무것도 하지 않는다.</param>
    public static void Show(string interactName)
    {
        if (string.IsNullOrEmpty(interactName))
        {
            return;
        }

        InteractPromptView.GetOrCreate().Request(interactName);
    }
}
