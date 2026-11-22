using Fusion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 파쿠르 튜토리얼 씬을 만든다. 메뉴: Tools/Pawntom Heist/Create Parkour Tutorial Scene.
/// <para>
/// 한 방향으로 이어지는 코스: ① 낮은 벽(1.8m) 붙기·오르기·올라서기 → ② 높은 벽(2.7m) 오르기
/// → ③ 구덩이 위 철봉 4개를 매달려 건너기 → 도착. 구덩이에 떨어지면 ③ 시작 발판으로 돌아온다.
/// 플레이어는 EnemyTestBootstrap(Single 모드 러너)으로 스폰한다 — 로비·본 게임 매니저 없이 바로 Play 하면 된다.
/// </para>
/// </summary>
public static class ParkourTutorialSceneBuilder
{
    private const string ScenePath = "Assets/01_Scenes/Parkour_Tutorial_Scene.unity";
    private const string PlayerPrefabPath = "Assets/02_Prefabs/player.prefab";
    private const string StatusUiPrefabPath = "Assets/02_Prefabs/UI/PlayerStatusUI.prefab";

    // 코스 치수(m). 플레이어 키 1.55, 점프 0.9, 매달린 발끝은 철봉 아래 1.9.
    private const float HalfWidth = 7f;
    private const float LowTop = 1.8f;
    private const float HighTop = 4.5f;
    private const float PitStartZ = 2f;
    private const float PitEndZ = 7f;
    private const float BarHeightAboveFloor = 2.35f;

    [MenuItem("Tools/Pawntom Heist/Create Parkour Tutorial Scene")]
    private static void Create()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null &&
            !EditorUtility.DisplayDialog("Parkour Tutorial Scene", $"{ScenePath} 이(가) 이미 있습니다. 새로 만들어 덮어쓸까요?", "덮어쓰기", "취소"))
        {
            return;
        }

        Build();
    }

    /// <summary>확인 창 없이 씬을 새로 만들어 저장한다(자동화용). 열려 있는 씬의 저장 안 된 변경은 버려진다.</summary>
    public static void Build()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        Material floor = NewMaterial("Floor", new Color(0.30f, 0.32f, 0.35f));
        Material wall = NewMaterial("Wall", new Color(0.80f, 0.68f, 0.48f));
        Material room = NewMaterial("Room", new Color(0.55f, 0.57f, 0.62f));
        Material platform = NewMaterial("Platform", new Color(0.45f, 0.55f, 0.70f));
        Material bar = NewMaterial("Bar", new Color(1.00f, 0.55f, 0.10f));
        Material goal = NewMaterial("Goal", new Color(0.35f, 0.75f, 0.45f));

        CreateLight();
        BuildRoom(floor, room);
        BuildWalls(wall);
        Transform retryPoint = BuildBars(platform, bar, goal);
        BuildFallZone(retryPoint);
        CreateHud();

        Transform spawn = new GameObject("PlayerSpawnPoint").transform;
        spawn.position = new Vector3(0f, 0.1f, -13f);

        CreateBootstrap(spawn);

        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log($"[ParkourTutorialSceneBuilder] {ScenePath} 생성 완료. Play 를 누르면 플레이어가 스폰됩니다.");
    }

    private static void BuildRoom(Material floor, Material room)
    {
        Transform root = Group("Room");
        Box("Floor", root, new Vector3(0f, -0.5f, 0f), new Vector3(HalfWidth * 2f, 1f, 32f), floor);
        Box("Wall_Left", root, new Vector3(-HalfWidth - 0.25f, 5f, 0f), new Vector3(0.5f, 10f, 32f), room);
        Box("Wall_Right", root, new Vector3(HalfWidth + 0.25f, 5f, 0f), new Vector3(0.5f, 10f, 32f), room);
        Box("Wall_Back", root, new Vector3(0f, 5f, -16.25f), new Vector3(HalfWidth * 2f + 1f, 10f, 0.5f), room);
        Box("Wall_Front", root, new Vector3(0f, 5f, 16.25f), new Vector3(HalfWidth * 2f + 1f, 10f, 0.5f), room);
    }

    // ①② 벽: 점프로는 못 넘는 높이라 붙어서 올라야 한다.
    private static void BuildWalls(Material wall)
    {
        Transform root = Group("1_2_Walls");
        Box("LowWall_1.8m", root, new Vector3(0f, LowTop * 0.5f, -5.5f), new Vector3(HalfWidth * 2f, LowTop, 5f), wall);
        Box("HighWall_4.5m", root, new Vector3(0f, HighTop * 0.5f, -0.5f), new Vector3(HalfWidth * 2f, HighTop, 5f), wall);

        Label(root, new Vector3(0f, LowTop + 0.5f, -8f),
            "① 벽 오르기\n점프 + W 로 벽에 붙기 → W 를 뗐다가 다시 눌러 오르기\n꼭대기에서 W 또는 Space 로 올라서기");
        Label(root, new Vector3(0f, HighTop + 0.5f, -3f),
            "② 높은 벽\n같은 방법으로 끝까지 — 붙어 있는 동안 스태미나가 줄어듭니다\n옆이나 뒤를 보고 Space: 벽을 차고 내려오기");
    }

    // ③ 철봉: 구덩이 위를 매달려 건넌다. 떨어지면 돌아올 지점을 돌려준다.
    private static Transform BuildBars(Material platform, Material bar, Material goal)
    {
        Transform root = Group("3_Bars");
        float goalDepth = 16f - PitEndZ;
        Box("GoalPlatform", root, new Vector3(0f, HighTop * 0.5f, PitEndZ + goalDepth * 0.5f),
            new Vector3(HalfWidth * 2f, HighTop, goalDepth), goal);

        // 발판 끝에서 점프하면 첫 철봉이 잡히고, 1.3m 간격으로 도약이 이어진다. 마지막 철봉에서 뛰면 도착 발판에 내린다.
        float barY = HighTop + BarHeightAboveFloor;
        float z = PitStartZ + 0.4f;
        int index = 1;

        for (; z < PitEndZ - 0.3f; z += 1.3f, index++)
        {
            HangingBar($"Bar_{index}", root, new Vector3(0f, barY, z), 3f, bar);
        }

        float pitLength = PitEndZ - PitStartZ;
        Box("Truss", root, new Vector3(0f, barY + 1.8f, PitStartZ + pitLength * 0.5f), new Vector3(0.2f, 0.2f, pitLength), platform);

        Label(root, new Vector3(-4f, HighTop + 0.9f, PitStartZ - 0.1f),
            "③ 철봉 건너기\n철봉 아래에서 점프 → 자동으로 매달림\n다음 철봉을 보고 Space 로 도약\nA/D 옆 이동 · 떨어지면 여기로 돌아옵니다");
        Label(root, new Vector3(0f, HighTop + 1.6f, 13f), "도착!\n파쿠르 튜토리얼 완료");

        Transform retryPoint = new GameObject("RetryPoint").transform;
        retryPoint.SetParent(root, false);
        retryPoint.position = new Vector3(0f, HighTop + 0.1f, PitStartZ - 1.5f);
        return retryPoint;
    }

    private static void BuildFallZone(Transform retryPoint)
    {
        float pitLength = PitEndZ - PitStartZ;
        GameObject zone = new GameObject("FallZone");
        zone.transform.position = new Vector3(0f, 0.6f, PitStartZ + pitLength * 0.5f);

        BoxCollider trigger = zone.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(HalfWidth * 2f, 1.2f, pitLength);

        ParkourFallZone fallZone = zone.AddComponent<ParkourFallZone>();
        SerializedObject so = new SerializedObject(fallZone);
        so.FindProperty("respawnPoint").objectReferenceValue = retryPoint;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // 체력·스태미나 바. 기존 PlayerStatusUI 프리팹을 그대로 쓴다(로컬 플레이어를 스스로 찾는다).
    private static void CreateHud()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StatusUiPrefabPath);

        if (prefab == null)
        {
            Debug.LogWarning($"[ParkourTutorialSceneBuilder] {StatusUiPrefabPath} 가 없어 HUD 를 넣지 못했습니다.");
            return;
        }

        GameObject canvasObject = new GameObject("HUD");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();
        PrefabUtility.InstantiatePrefab(prefab, canvasObject.transform);
    }

    private static void HangingBar(string name, Transform parent, Vector3 localPosition, float length, Material material)
    {
        GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        bar.name = name;
        bar.transform.SetParent(parent, false);
        bar.transform.localPosition = localPosition;
        bar.transform.localRotation = Quaternion.Euler(0f, 0f, 90f); // 원기둥 축(Y)을 좌우(X)로
        bar.transform.localScale = new Vector3(0.08f, length * 0.5f, 0.08f);
        bar.GetComponent<Renderer>().sharedMaterial = material;

        ParkourBar parkourBar = bar.AddComponent<ParkourBar>();
        SerializedObject so = new SerializedObject(parkourBar);
        so.FindProperty("localAxis").vector3Value = Vector3.up;
        so.FindProperty("length").floatValue = length;
        so.ApplyModifiedPropertiesWithoutUndo();

        // 천장 트러스에 매다는 줄(장식).
        for (int side = -1; side <= 1; side += 2)
        {
            GameObject rope = Box("Rope", parent, localPosition + new Vector3(side * length * 0.45f, 0.9f, 0f),
                new Vector3(0.04f, 1.8f, 0.04f), material);
            Object.DestroyImmediate(rope.GetComponent<Collider>());
        }
    }

    private static Transform Group(string name)
    {
        return new GameObject(name).transform;
    }

    private static GameObject Box(string name, Transform parent, Vector3 localPosition, Vector3 size, Material material)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.localPosition = localPosition;
        box.transform.localScale = size;
        box.GetComponent<Renderer>().sharedMaterial = material;
        return box;
    }

    private static void Label(Transform parent, Vector3 localPosition, string text)
    {
        GameObject label = new GameObject("Label");
        label.transform.SetParent(parent, false);
        label.transform.localPosition = localPosition;

        TextMesh mesh = label.AddComponent<TextMesh>();
        mesh.text = text;
        mesh.anchor = TextAnchor.LowerCenter;
        mesh.alignment = TextAlignment.Center;
        mesh.fontSize = 48;
        mesh.characterSize = 0.06f;
        mesh.color = Color.white;
    }

    private static Material NewMaterial(string name, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        Material material = new Material(shader != null ? shader : Shader.Find("Standard")) { name = name };
        material.color = color;
        return material;
    }

    private static void CreateLight()
    {
        GameObject go = new GameObject("Directional Light");
        go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        Light light = go.AddComponent<Light>();
        light.type = LightType.Directional;
        light.shadows = LightShadows.Soft;
    }

    private static void CreateBootstrap(Transform spawn)
    {
        GameObject go = new GameObject("ParkourTutorialBootstrap");
        EnemyTestBootstrap bootstrap = go.AddComponent<EnemyTestBootstrap>();
        SerializedObject so = new SerializedObject(bootstrap);
        so.FindProperty("spawnPoint").objectReferenceValue = spawn;

        string guid = AssetDatabase.AssetPathToGUID(PlayerPrefabPath);

        if (NetworkObjectGuid.TryParse(guid, out NetworkObjectGuid parsed))
        {
            SetGuid(so.FindProperty("playerPrefab"), parsed);
        }
        else
        {
            Debug.LogError($"[ParkourTutorialSceneBuilder] {PlayerPrefabPath} 를 찾지 못했습니다. ParkourTutorialBootstrap 의 Player Prefab 을 직접 지정하세요.");
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static unsafe void SetGuid(SerializedProperty prefabRef, NetworkObjectGuid guid)
    {
        SerializedProperty raw = prefabRef.FindPropertyRelative(nameof(NetworkObjectGuid.RawGuidValue));
        raw.GetFixedBufferElementAtIndex(0).longValue = guid.RawGuidValue[0];
        raw.GetFixedBufferElementAtIndex(1).longValue = guid.RawGuidValue[1];
    }
}
