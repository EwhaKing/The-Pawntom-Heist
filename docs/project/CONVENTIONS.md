# CONVENTIONS — The Pawntom Heist

> 노션 「프로그래머 - 공통사항」 / 「깃허브 폴더 구조와 협업에 대하여…」를 요약하고, 현재 리포지토리의 실제 상태를 함께 정리한 문서입니다.
> **[규칙]** 은 노션에 문서화된 팀 합의, **[현황]** 은 리포지토리를 확인한 실제 상태, **[제안]** 은 아직 팀 합의가 없어 이 문서에서 제안하는 내용입니다.

## 0. 환경 [규칙]

| 항목 | 값 |
|---|---|
| Unity | 6000.3.19f1 LTS |
| 렌더 파이프라인 | URP 17.3.0 |
| 네트워크 | Photon Fusion 2 (`Assets/Photon/`) |
| 주요 패키지 | Input System 1.19, AI Navigation 2.0, Cinemachine 3.1, TextMesh Pro, uGUI |
| 저장소 | https://github.com/EwhaKing/The-Pawntom-Heist |

## 1. 폴더 구조 [규칙]

```
Assets/
├─ 01_Scenes/            개발용 및 조립용 씬
├─ 02_Prefabs/           ★가장 중요★ 모든 조립용 부품(프리팹)
├─ 03_Scripts/           모든 C# 스크립트
├─ 04_Art_Resources/     아트 직군이 만든 UI 이미지, 머티리얼
├─ 05_Imported_Assets/   에셋 스토어 등 외부 무료 에셋 (반드시 분리 보관)
├─ 06_Docs/              문서
├─ 07_ScriptableObject/  ScriptableObject 애셋 인스턴스
├─ 08_Fonts/             폰트 및 TMP SDF 애셋
└─ Photon/               Fusion SDK (직접 수정 금지)
```

- `01_Scenes` 용도 구분: `Main_Scene`(최종 조립용, 아직 미사용) / `Player_Scene`(이동·점프·인벤토리 테스트) / `Map_Scene`(복도·방 배치, 아이템 스폰) / `Enemy_Scene`(적 NPC 순찰·시야각). 그 외 현재 `Bootstrap_Scene`, `Lobby_Scene`, `HackingTestScene` 추가됨.
- 외부 에셋은 반드시 `05_Imported_Assets`에 모아 둡니다(직접 작성한 애셋과 섞이면 추적 불가).
- `03_Scripts` 하위는 기능 도메인별로 분리합니다 [현황]:
  `GameContent/{Exit, Item, Minigame}` · `Lobby` · `Managers` · `Player` · `Sound` · `UI` · `Utils`.

## 2. 네이밍

### 2.1 합의된 규칙 [규칙]

- 브랜치: `feature/<작업이름>` (예: `feature/Map`, `feature/Player`, `feature/enemy`). 담당 기능명을 영어로 붙입니다. 수정이 필요하면 브랜치 우클릭 > Rename.
- 커밋 말머리:

| 말머리 | 종류 |
|---|---|
| `feat:` | 새로운 기능 |
| `fix:` | 버그 수정 |
| `refact:` | 코드 리팩토링(폴더 묶기, 파일 정리 등) |
| `build:` | 빌드 관련 파일 수정, 모듈 설치·삭제 |
| `style:` | 코드 스타일 혹은 포맷 |
| `chore:` | 문서나 기타 자잘한 수정 |

  예) `feat: 캐릭터 상하좌우 이동 추가` / `chore: readme 수정` / `refact: Player.cs 파일을 PlayerJump.cs와 PlayerMove.cs로 분리`
  (본문은 한국어로 작성하는 것이 팀 관행입니다.)

### 2.2 애셋·코드 네이밍 [현황 + 제안]

현재 리포지토리에서 실제로 쓰이는 패턴이며, **신규 작업은 아래 열의 규칙을 따릅니다.**

| 대상 | 규칙 | 예시 |
|---|---|---|
| 씬 | `<용도>_Scene` (PascalCase) | `Lobby_Scene`, `Map_Scene`, `Enemy_Scene` |
| 스크립트 파일 | PascalCase, 파일명 = 클래스명 | `PlayerController.cs`, `HackingManager.cs` |
| 매니저 | `<도메인>Manager` | `GameManager`, `InventoryManager`, `ItemSpawnManager` |
| UI 스크립트 | `<대상>UI` / `<대상>View` / `<대상>Popup` | `InventoryUI`, `HackingPopupView`, `CharacterSelectPopup` |
| 데이터 컨테이너 | `<대상>Data` | `PlayerData`, `CatData`, `ItemData`, `HackingGameData` |
| enum | 단수 PascalCase, 멤버도 PascalCase | `CatType { BlackCat, TabbyCat, OrangeCat, CalicoCat }` |
| ScriptableObject 애셋 | `<SO타입>_<식별자>` | `CatData_BlackCat`, `ItemData_Diamond` |
| 프리팹 | PascalCase | `HackingPopup.prefab`, `LobbyPlayerSlot.prefab` |
| 상수 | `SceneNames` 같은 `static class`의 `const string`으로 모아 관리 | `SceneNames.Lobby` |
| private 필드 | `_camelCase` | `_instance` |

**정리 필요 [현황]** — 아래는 규칙에서 벗어난 기존 파일입니다. 손대는 김에 `refact:` 커밋으로 맞추는 것을 권장합니다.

- `Player/moving_player.cs`, `Player/moving_arms.cs` → snake_case (PascalCase로)
- `02_Prefabs/inventoryUI.prefab`, `player.prefab`, `test.mat` → 소문자 시작
- `07_ScriptableObject/ItemData/Item_Necklace.asset` → 다른 애셋들은 `ItemData_*` 접두사
- `GameContent/Item/Old_Single/` → 싱글 플레이용 구버전. 정리 대상 여부 확인 필요

### 2.3 코드 작성 규칙 [현황 + 제안]

- 네임스페이스: 현재 `Hacking` 하나만 사용 중이고 나머지는 전역 네임스페이스입니다 [현황]. **신규 기능 폴더는 폴더명과 일치하는 네임스페이스를 붙이는 것을 제안합니다** (예: `Pawntom.Player`, `Pawntom.UI`). 전역 네임스페이스가 늘어나면 어셈블리 분리(4장) 시 이름 충돌이 발생합니다. [제안]
- 싱글톤은 `Utils/PawntomSingleton<T>`를 상속해 사용합니다. `Awake()`를 오버라이드할 때는 반드시 `base.Awake()`를 호출합니다 (내부에서 `DontDestroyOnLoad` 및 중복 인스턴스 파괴를 처리).
- 씬 이름은 문자열 리터럴 대신 `SceneNames` 상수를 사용합니다.
- **레벨 배치 데이터는 씬에 하드코딩하지 않습니다.** 격벽·아이템·NPC 순찰 경로·금고·열쇠 후보 지점·스프링클러·전등 구역·변수 지점·평면도는 층·단계별 외부 레벨 데이터로 분리합니다(GAME_DESIGN 3.8 참조). 본부 평면도는 이 데이터를 읽어 렌더링해야 합니다.
- 격벽·스프링클러의 근접 접촉 판정은 **공용 접촉 트리거 컴포넌트** 하나를 공유합니다(중복 구현 금지).
- 미니게임은 격벽·스프링클러·전등 3곳에서 호출되므로 **공통 호출 인터페이스**(노드 종류 + 보안 등급 → 성공·실패 콜백)를 통해서만 띄웁니다.

## 3. 브랜치 · 협업 흐름 [규칙]

1. 분기 지점에서 `feature/<작업이름>` 브랜치를 생성합니다(GitKraken: 우클릭 > Create Branch here).
2. 해당 브랜치에서 작업·커밋합니다.
3. `main` 병합이 필요하면 **Pull Request를 보내고 카카오톡으로 알립니다.** 리드가 확인 후 머지합니다.
   → `main`에 직접 푸시하지 않습니다.
4. 실제 사용 중인 브랜치 예시 [현황]: `feature/Map`, `feature/Player`, `feature/enemy`, `feature/inventory`, `feature/inventoryUI`, `feature/minigame`, `feature/minigameUI`, `feature/minimapUI`, `feature/PickUp`, `feature/exit`, `feature/1stFloor`, `fix/exit`, `fix/inventory-delete`.
   → 버그 수정 브랜치는 `fix/<대상>` 형태를 씁니다.

**Unity 프로젝트 주의**: 씬(`.unity`)과 프리팹은 머지 충돌이 사실상 해결 불가에 가깝습니다. 같은 씬을 동시에 건드리지 않도록 작업 씬을 분리하고(`Player_Scene`, `Map_Scene`, `Enemy_Scene`), 공용 조립은 프리팹 단위로 주고받습니다. `.meta` 파일은 반드시 함께 커밋합니다. [제안]

## 4. 어셈블리 정의(asmdef) 분리 규칙

### 4.1 현황

프로젝트 코드에는 **asmdef가 하나도 없습니다.** `Assets/03_Scripts` 전체가 `Assembly-CSharp`에 들어가고, asmdef는 Photon SDK 쪽에만 존재합니다 (`Fusion.Unity`, `Fusion.Unity.Editor`, `Fusion.CodeGen`, `PhotonWebSocket`). 그 결과 스크립트 한 줄만 고쳐도 전체 코드가 재컴파일됩니다.

### 4.2 분리 규칙 [제안] — 팀 합의 전, 도입 시 리드 확인 필요

**언제 나누나**: 폴더 구조가 안정된 도메인부터 하나씩. 기능 개발 중인 폴더를 성급히 자르면 참조 추가 작업만 늘어납니다. 우선순위는 `Utils` → `Managers`/네트워크 → `UI` 순.

**명명**: `Pawntom.<도메인>`, Editor 전용은 `Pawntom.<도메인>.Editor`. asmdef 파일명 = 어셈블리명 = 배치 폴더명 기준.

**제안 구조**

| 어셈블리 | 위치 | 담는 것 | 참조 |
|---|---|---|---|
| `Pawntom.Core` | `03_Scripts/Utils` | 싱글톤, 씬 상수, 씬 유틸, 공용 데이터 타입 | (없음) |
| `Pawntom.Network` | `03_Scripts/Managers` | 네트워크·게임·스폰 매니저 | Core, `Fusion.Unity` |
| `Pawntom.Gameplay` | `03_Scripts/Player`, `GameContent` | 플레이어, 아이템, 적, 격벽, 미니게임 로직 | Core, Network |
| `Pawntom.UI` | `03_Scripts/UI`, `Lobby` | HUD, 로비, 인벤토리, 본부 터미널, 미니게임 뷰 | Core, Gameplay |
| `Pawntom.Editor` | `03_Scripts/Editor`(신설) | 커스텀 인스펙터, 툴 | 전부 + `Editor` 플랫폼 한정 |

**지켜야 할 것**
- **참조는 한 방향으로만.** UI → Gameplay는 되지만 Gameplay → UI는 금지(순환 참조는 Unity가 컴파일 에러로 막습니다). 아래 계층이 위 계층에 알려야 할 때는 이벤트/콜백으로 넘깁니다.
- Fusion 타입(`NetworkBehaviour`, `NetworkObject` 등)을 쓰는 어셈블리는 asmdef의 Assembly Definition References에 `Fusion.Unity`를 추가해야 합니다.
- Editor 전용 코드는 반드시 별도 asmdef + Platforms를 `Editor`로 제한합니다. 런타임 어셈블리에 `UnityEditor`가 섞이면 빌드가 깨집니다.
- `05_Imported_Assets`의 외부 에셋에는 asmdef를 새로 만들지 않습니다(에셋 업데이트 시 충돌).
- asmdef를 추가한 뒤에는 반드시 한 번 풀 컴파일 + 씬 실행으로 참조 누락을 확인하고, `build:` 말머리로 커밋합니다.

## 5. 문서 규칙

- 프로젝트 요약 문서는 `docs/project/` 아래에 둡니다: `GAME_DESIGN.md`(기획), `UI_SPEC.md`(화면), `CONVENTIONS.md`(이 문서).
- 이 문서들은 노션·피그마 원문의 **요약본**입니다. 원문이 갱신되면 해당 파일을 다시 정리하고 문서 하단의 추출일자를 갱신합니다.
- 코드 주석·문서는 한국어로 작성합니다 [현황].

---

**출처**: 노션 「The Pawntom Heist! (new)」 → 「프로그래머 - 공통사항」(유니티·네트워크 버전, 커밋 메시지 양식), 「깃허브 폴더 구조와 협업에 대하여…」(폴더 구조, 브랜치·PR 절차), 「시스템 기획서」(레벨 데이터 분리·공용 컴포넌트 규칙) / 피그마 보드 「[TPH] 기획서 v2」 Page 1 — 본부 섹션(공용 평면도·미니게임 공통 호출 전제) / 리포지토리 `Assets/` 실제 구조 및 `git branch -a` 확인 · 추출일자 2026-08-19
