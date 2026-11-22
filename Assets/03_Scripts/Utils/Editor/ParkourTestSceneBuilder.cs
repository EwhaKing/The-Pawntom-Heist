using Fusion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 파쿠르 수치 조정용 테스트 씬을 만든다. 메뉴: Tools/Pawntom Heist/Create Parkour Test Scene.
/// <para>
/// 구역: ① 빈 벽 오르기·턱 넘기 ② 철봉 매달리기·도약.
/// 플레이어는 EnemyTestBootstrap(Single 모드 러너)으로 스폰한다 — 로비·본 게임 매니저 없이 바로 Play 하면 된다.
/// </para>
/// </summary>
public static class ParkourTestSceneBuilder
{
    private const string ScenePath = "Assets/01_Scenes/Parkour_TestScene.unity";
    private const string PlayerPrefabPath = "Assets/02_Prefabs/player.prefab";

    [MenuItem("Tools/Pawntom Heist/Create Parkour Test Scene")]
    private static void Create()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null &&
            !EditorUtility.DisplayDialog("Parkour Test Scene", $"{ScenePath} 이(가) 이미 있습니다. 새로 만들어 덮어쓸까요?", "덮어쓰기", "취소"))
        {
            return;
        }

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        Material floor = NewMaterial("Floor", new Color(0.30f, 0.32f, 0.35f));
        Material wall = NewMaterial("Wall", new Color(0.80f, 0.68f, 0.48f));
        Material platform = NewMaterial("Platform", new Color(0.45f, 0.55f, 0.70f));
        Material bar = NewMaterial("Bar", new Color(1.00f, 0.55f, 0.10f));

        CreateLight();
        Box("Floor", null, new Vector3(0f, -0.5f, 0f), new Vector3(30f, 1f, 30f), floor);

        BuildWallStation(new Vector3(-5f, 0f, 2f), wall);
        BuildBarStation(new Vector3(5f, 0f, 2f), platform, bar);

        Transform spawn = new GameObject("PlayerSpawnPoint").transform;
        spawn.position = new Vector3(0f, 0.1f, -9f);

        CreateBootstrap(spawn);

        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log($"[ParkourTestSceneBuilder] {ScenePath} 생성 완료. Play 를 누르면 플레이어가 스폰됩니다. 파쿠르는 전용 키 없이 공중에서 벽 쪽으로 가면 붙습니다.");
    }

    // ① 빈 벽: 공중에서 벽에 붙어 오르기 → 꼭대기에서 턱 넘기.
    private static void BuildWallStation(Vector3 origin, Material material)
    {
        Transform root = Station("1_Wall_ClimbMantle", origin);
        Box("Tower_4m", root, new Vector3(0f, 2f, 0f), new Vector3(5f, 4f, 5f), material);
        Box("Step_2m", root, new Vector3(0f, 1f, -4f), new Vector3(5f, 2f, 3f), material);
        Label(root, new Vector3(0f, 4.8f, -2.6f), "① 빈 벽 오르기 / 턱 넘기\nSpace + W 로 벽에 붙기 (W 는 한 번 뗐다 다시)\nW 오르기, 꼭대기에서 W 또는 Space 로 올라서기");
    }

    // ② 철봉: 철봉 아래에서 점프 → 손이 닿으면 자동으로 매달림 → 다음 철봉을 보고 Space 도약.
    private static void BuildBarStation(Vector3 origin, Material platform, Material bar)
    {
        Transform root = Station("2_Bars_HangLeap", origin);
        // 바닥에 서면 손 높이 약 1.7m, 점프하면 약 2.6m. 바닥에서 점프해 닿는 높이(2.35m)에 철봉을 둔다.
        float[] z = { -3f, -1.7f, -0.4f, 0.9f };

        for (int i = 0; i < z.Length; i++)
        {
            HangingBar($"Bar_{i + 1}", root, new Vector3(0f, 2.35f, z[i]), 2.2f, bar);
        }

        Box("Truss", root, new Vector3(0f, 4.25f, -1.05f), new Vector3(0.2f, 0.2f, 5f), platform);
        Label(root, new Vector3(0f, 4.6f, -4.5f), "② 철봉 매달리기\n철봉 아래에서 점프 → 손이 닿으면 자동으로 매달림\nA/D 옆 이동, 다음 철봉을 보고 Space 도약");
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
        AddBar(bar, Vector3.up, length);

        // 천장 트러스에 매다는 줄(장식).
        for (int side = -1; side <= 1; side += 2)
        {
            GameObject rope = Box("Rope", parent, localPosition + new Vector3(side * length * 0.45f, 0.9f, 0f),
                new Vector3(0.04f, 1.8f, 0.04f), material);
            Object.DestroyImmediate(rope.GetComponent<Collider>());
        }
    }

    private static void AddBar(GameObject target, Vector3 localAxis, float length)
    {
        ParkourBar bar = target.AddComponent<ParkourBar>();
        SerializedObject so = new SerializedObject(bar);
        so.FindProperty("localAxis").vector3Value = localAxis;
        so.FindProperty("length").floatValue = length;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Transform Station(string name, Vector3 position)
    {
        Transform root = new GameObject(name).transform;
        root.position = position;
        return root;
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
        GameObject go = new GameObject("ParkourTestBootstrap");
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
            Debug.LogError($"[ParkourTestSceneBuilder] {PlayerPrefabPath} 를 찾지 못했습니다. ParkourTestBootstrap 의 Player Prefab 을 직접 지정하세요.");
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
