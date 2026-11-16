using UnityEngine.InputSystem;

/// <summary>
/// 옵션창에서 바꿀 수 있는 키 한 칸. 입력 액션과 그 안의 바인딩 번호(키보드·마우스 쪽)를 가리킨다.
/// 이동(WASD)은 합성 바인딩의 부분 하나가 한 칸이다 — 화살표 쪽 바인딩은 보조 키로 그대로 둔다.
/// </summary>
public sealed class RebindableBinding
{
    public RebindableBinding(string id, string label, InputAction action, int bindingIndex)
    {
        Id = id;
        Label = label;
        Action = action;
        BindingIndex = bindingIndex;
    }

    /// <summary>코드에서 찾을 때 쓰는 이름(예: "Interact").</summary>
    public string Id { get; }

    /// <summary>옵션창에 보이는 이름(예: "상호작용").</summary>
    public string Label { get; }

    public InputAction Action { get; }
    public int BindingIndex { get; }

    /// <summary>지금 실제로 쓰이는 키 경로(오버라이드가 있으면 그것).</summary>
    public string EffectivePath
    {
        get { return Action.bindings[BindingIndex].effectivePath; }
    }

    /// <summary>화면에 보일 키 이름(대문자).</summary>
    public string DisplayName
    {
        get
        {
            string display = Action.GetBindingDisplayString(BindingIndex, InputBinding.DisplayStringOptions.DontIncludeInteractions);
            return string.IsNullOrEmpty(display) ? "-" : display.ToUpperInvariant();
        }
    }
}
