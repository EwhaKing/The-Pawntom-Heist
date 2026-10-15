using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Pawntom.TutorialBridge
{
    /// <summary>
    /// 키 안내 문구의 <c>{토큰}</c>을 실제 키 이름으로 바꾸는 표.
    /// 기획서는 상호작용을 F로 적었지만 지금 입력 바인딩(InputSystemActions)은 E다. 안내는 실제로 눌러서 되는 키를 보여줘야 하므로
    /// 바인딩에서 읽는다 — 팀이 바인딩을 F로 바꾸면 안내도 자동으로 F가 된다.
    /// </summary>
    public static class TutorialKeyLabels
    {
        private const string KeyboardGroup = "Keyboard&Mouse";

        public static Dictionary<string, string> Build()
        {
            Dictionary<string, string> tokens = new Dictionary<string, string>();
            tokens["Interact"] = ReadInteractBinding("F");

            // MainbaseControlUI.Update 가 Esc 로 단말기를 닫는다(기획서는 F). 동작과 안내를 맞춘다.
            tokens["TerminalExit"] = "Esc";
            return tokens;
        }

        private static string ReadInteractBinding(string fallback)
        {
            InputSystemActions actions = null;

            try
            {
                actions = new InputSystemActions();
                string display = actions.Player.Interact.GetBindingDisplayString(InputBinding.MaskByGroup(KeyboardGroup));

                if (!string.IsNullOrEmpty(display))
                {
                    return display.ToUpperInvariant();
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[TutorialKeyLabels] 상호작용 키 바인딩을 읽지 못해 " + fallback + "로 표시합니다: " + exception.Message);
            }
            finally
            {
                actions?.Dispose();
            }

            return fallback;
        }
    }
}
