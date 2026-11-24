using System.Collections.Generic;

namespace Pawntom.TutorialBridge
{
    /// <summary>
    /// 키 안내 문구의 <c>{토큰}</c>을 실제 키 이름으로 바꾸는 표.
    /// 안내는 실제로 눌러서 되는 키를 보여줘야 하므로 InputManager 의 현재 바인딩(옵션창에서 바꾼 키 포함)에서 읽는다.
    /// 예전에는 <c>new InputSystemActions()</c> 로 에셋 기본값을 읽어, 키를 바꿔도 안내는 기본 키를 보여 줬다.
    /// 키를 바꾸면 <see cref="Refresh"/> 로 같은 표를 다시 채운다(TutorialHintView 가 InputManager.BindingsChanged 때 부른다).
    /// </summary>
    public static class TutorialKeyLabels
    {
        public static Dictionary<string, string> Build()
        {
            Dictionary<string, string> tokens = new Dictionary<string, string>();
            Refresh(tokens);
            return tokens;
        }

        public static void Refresh(Dictionary<string, string> tokens)
        {
            if (tokens == null)
            {
                return;
            }

            InputManager input = InputManager.Instance;

            // 기획서는 상호작용을 F로 적었지만 지금 기본 바인딩은 E 다. 읽지 못했을 때만 기획서 표기를 쓴다.
            tokens["Interact"] = input.GetKeyDisplayName("Interact", "F");
            tokens["Sprint"] = input.GetKeyDisplayName("Sprint", "Shift");
            tokens["Jump"] = input.GetKeyDisplayName("Jump", "Space");
            tokens["Drop"] = input.GetKeyDisplayName("Drop", "Q");
            tokens["Move"] = MoveLabel(input);

            // MainbaseControlUI.Update 가 Esc 로 단말기를 닫는다(기획서는 F). 동작과 안내를 맞춘다. Esc 는 바꿀 수 없는 키다.
            tokens["TerminalExit"] = "Esc";
        }

        // 앞·왼쪽·뒤·오른쪽 순서로 붙인다(기본 WASD). 한 글자 키만이면 붙여 쓰고, 아니면 "/" 로 나눈다.
        private static string MoveLabel(InputManager input)
        {
            string up = input.GetKeyDisplayName("MoveForward", "W");
            string left = input.GetKeyDisplayName("MoveLeft", "A");
            string down = input.GetKeyDisplayName("MoveBack", "S");
            string right = input.GetKeyDisplayName("MoveRight", "D");

            bool compact = up.Length == 1 && left.Length == 1 && down.Length == 1 && right.Length == 1;
            return compact ? up + left + down + right : up + "/" + left + "/" + down + "/" + right;
        }
    }
}
