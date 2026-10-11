using UnityEngine;

namespace Pawntom.TutorialBridge
{
    /// <summary>
    /// 튜토리얼 목적지 안내 상자. 플레이어가 가야 할 구역에 연초록 반투명 상자와 밝은 모서리를 띄워
    /// "저쪽으로 가야 하는구나"를 보여 준다. 숨 쉬듯 천천히 밝아졌다 어두워지고, 플레이어가 안에 들어서면 사라진다.
    /// <para>
    /// 에셋 없이 만든다(기본 큐브 메시 + 런타임 머티리얼). 콜라이더는 떼어 내 게임 판정에 끼지 않는다.
    /// 셰이더는 항상 빌드에 포함되는 Sprites/Default(반투명·양면·깊이 쓰기 없음)를 쓴다.
    /// </para>
    /// </summary>
    public sealed class TutorialGuideMarker : MonoBehaviour
    {
        // UI_SPEC 에 확정 색이 없어 요청("연초록")대로 둔다. 바꾸려면 여기만 고친다.
        private static readonly Color FillColor = new Color(0.56f, 1f, 0.5f, 0.17f);
        private static readonly Color EdgeColor = new Color(0.62f, 1f, 0.56f, 0.9f);

        private const float EdgeThickness = 0.04f;
        private const float MinWidth = 1f;
        private const float MinHeight = 2f;
        private const float FadeSeconds = 0.35f;

        private Transform _root;
        private Transform _fill;
        private readonly Transform[] _edges = new Transform[12];
        private Material _fillMaterial;
        private Material _edgeMaterial;

        private Bounds _bounds;
        private float _alpha;
        private float _targetAlpha;

        public void Build()
        {
            Shader shader = Shader.Find("Sprites/Default");

            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            _fillMaterial = new Material(shader) { name = "TutorialGuideFill", hideFlags = HideFlags.DontSave };
            _edgeMaterial = new Material(shader) { name = "TutorialGuideEdge", hideFlags = HideFlags.DontSave };
            _fillMaterial.renderQueue = 3000;
            _edgeMaterial.renderQueue = 3001;

            _root = new GameObject("TutorialGuideBox").transform;
            _root.SetParent(transform, false);

            _fill = CreateCube("Fill", _fillMaterial);

            for (int i = 0; i < _edges.Length; i++)
            {
                _edges[i] = CreateCube("Edge" + i, _edgeMaterial);
            }

            ApplyAlpha(0f);
            _root.gameObject.SetActive(false);
        }

        /// <summary>목적지를 보여 준다. 플레이어가 이미 안에 있으면 숨긴다(도착).</summary>
        public void Show(Bounds bounds, Vector3 viewerPosition)
        {
            Vector3 size = bounds.size;
            size.x = Mathf.Max(size.x, MinWidth);
            size.z = Mathf.Max(size.z, MinWidth);
            size.y = Mathf.Max(size.y, MinHeight);
            bounds.size = size;

            if (bounds.Contains(viewerPosition))
            {
                Hide();
                return;
            }

            if (!_root.gameObject.activeSelf || (bounds.center - _bounds.center).sqrMagnitude > 0.01f || (bounds.size - _bounds.size).sqrMagnitude > 0.01f)
            {
                _bounds = bounds;
                Layout();
            }

            _targetAlpha = 1f;

            if (!_root.gameObject.activeSelf)
            {
                _root.gameObject.SetActive(true);
            }
        }

        public void Hide()
        {
            _targetAlpha = 0f;
        }

        private void Update()
        {
            if (_root == null || !_root.gameObject.activeSelf)
            {
                return;
            }

            _alpha = Mathf.MoveTowards(_alpha, _targetAlpha, Time.unscaledDeltaTime / FadeSeconds);

            // 숨 쉬듯 천천히 밝아졌다 어두워진다.
            float pulse = 0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * 2.6f);
            ApplyAlpha(_alpha * pulse);

            if (_alpha <= 0f && _targetAlpha <= 0f)
            {
                _root.gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (_fillMaterial != null)
            {
                Destroy(_fillMaterial);
            }

            if (_edgeMaterial != null)
            {
                Destroy(_edgeMaterial);
            }
        }

        private void ApplyAlpha(float alpha)
        {
            Color fill = FillColor;
            fill.a *= alpha;
            _fillMaterial.color = fill;

            Color edge = EdgeColor;
            edge.a *= alpha;
            _edgeMaterial.color = edge;
        }

        private void Layout()
        {
            Vector3 center = _bounds.center;
            Vector3 size = _bounds.size;
            Vector3 half = size * 0.5f;

            _root.position = center;
            _root.rotation = Quaternion.identity;
            _fill.localPosition = Vector3.zero;
            _fill.localScale = size;

            int index = 0;
            float t = EdgeThickness;

            // X 방향 모서리 4개
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    SetEdge(index++, new Vector3(0f, half.y * y, half.z * z), new Vector3(size.x + t, t, t));
                }
            }

            // Y 방향 모서리 4개
            for (int x = -1; x <= 1; x += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    SetEdge(index++, new Vector3(half.x * x, 0f, half.z * z), new Vector3(t, size.y + t, t));
                }
            }

            // Z 방향 모서리 4개
            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    SetEdge(index++, new Vector3(half.x * x, half.y * y, 0f), new Vector3(t, t, size.z + t));
                }
            }
        }

        private void SetEdge(int index, Vector3 localPosition, Vector3 localScale)
        {
            _edges[index].localPosition = localPosition;
            _edges[index].localScale = localScale;
        }

        private Transform CreateCube(string cubeName, Material material)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = cubeName;
            cube.transform.SetParent(_root, false);

            Collider cubeCollider = cube.GetComponent<Collider>();

            if (cubeCollider != null)
            {
                cubeCollider.enabled = false; // 같은 프레임에 판정에 끼지 않도록 먼저 끄고 지운다
                Destroy(cubeCollider);
            }

            MeshRenderer meshRenderer = cube.GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return cube.transform;
        }
    }
}
