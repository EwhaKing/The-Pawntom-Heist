public static class SceneNames
{
    public const string Lobby = "Lobby_Scene";
    public const string Map = "Map_Scene";

    // 데모(feature/DemoMap) 전용. DemoQuickStart가 P 키로 들어가는 별도 씬이다.
    // Map을 대체하지 않는다 — 로비의 "방 만들기 → 시작" 정상 흐름은 여전히 Map을 탄다.
    public const string DemoTutorialMap = "Tutorial_Scene";
}