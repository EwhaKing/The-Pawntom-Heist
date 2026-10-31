using System.Collections.Generic;
using UnityEngine;

/// <summary>외부 VFX 에셋 없이 사망 지점에 남기는 발자국/피격 흔적과 짧은 확산 효과.</summary>
public sealed class PlayerDeathTrace : MonoBehaviour
{
    private Mesh _mesh;
    private Mesh _pulseMesh;
    private Transform _pulse;
    private float _elapsed;

    public static GameObject Create(Vector3 position, Vector3 normal, Material material)
    {
        GameObject root = new GameObject("Death Paw Trace");
        root.transform.SetPositionAndRotation(position + normal * 0.015f, Quaternion.FromToRotation(Vector3.up, normal));
        PlayerDeathTrace trace = root.AddComponent<PlayerDeathTrace>();
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        AddDisc(vertices, triangles, new Vector2(0f, -0.06f), new Vector2(0.17f, 0.15f));
        for (int i = 0; i < 4; i++)
            AddDisc(vertices, triangles, new Vector2((i - 1.5f) * 0.115f, 0.15f + (i == 1 || i == 2 ? 0.06f : 0f)), new Vector2(0.05f, 0.07f));
        for (int i = 0; i < 3; i++)
        {
            int first = vertices.Count;
            float x = 0.3f + i * 0.07f;
            vertices.Add(new Vector3(x, 0, -0.18f));
            vertices.Add(new Vector3(x + 0.03f, 0, 0.18f));
            vertices.Add(new Vector3(x + 0.05f, 0, 0.17f));
            triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2);
        }
        trace._mesh = BuildMesh(vertices, triangles);
        root.AddComponent<MeshFilter>().sharedMesh = trace._mesh;
        root.AddComponent<MeshRenderer>().sharedMaterial = material;

        GameObject pulse = new GameObject("Death Pulse");
        pulse.transform.SetParent(root.transform, false);
        trace._pulse = pulse.transform;
        vertices.Clear(); triangles.Clear();
        for (int i = 0; i < 48; i++)
        {
            float a = i * Mathf.PI * 2f / 48;
            float b = (i + 1) * Mathf.PI * 2f / 48;
            int first = vertices.Count;
            vertices.Add(new Vector3(Mathf.Cos(a) * 0.4f, 0f, Mathf.Sin(a) * 0.4f));
            vertices.Add(new Vector3(Mathf.Cos(b) * 0.4f, 0f, Mathf.Sin(b) * 0.4f));
            vertices.Add(new Vector3(Mathf.Cos(a) * 0.43f, 0f, Mathf.Sin(a) * 0.43f));
            vertices.Add(new Vector3(Mathf.Cos(b) * 0.43f, 0f, Mathf.Sin(b) * 0.43f));
            triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2);
            triangles.Add(first + 2); triangles.Add(first + 1); triangles.Add(first + 3);
        }
        trace._pulseMesh = BuildMesh(vertices, triangles);
        pulse.AddComponent<MeshFilter>().sharedMesh = trace._pulseMesh;
        pulse.AddComponent<MeshRenderer>().sharedMaterial = material;
        return root;
    }

    private static void AddDisc(List<Vector3> vertices, List<int> triangles, Vector2 center, Vector2 radius)
    {
        int first = vertices.Count;
        vertices.Add(new Vector3(center.x, 0f, center.y));
        for (int i = 0; i <= 24; i++)
        {
            float angle = -i * Mathf.PI * 2f / 24;
            vertices.Add(new Vector3(center.x + Mathf.Cos(angle) * radius.x, 0f, center.y + Mathf.Sin(angle) * radius.y));
            if (i == 0) continue;
            triangles.Add(first); triangles.Add(first + i); triangles.Add(first + i + 1);
        }
    }

    private static Mesh BuildMesh(List<Vector3> vertices, List<int> triangles)
    {
        Mesh mesh = new Mesh { name = "Death Trace Mesh" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private void Update()
    {
        _elapsed += Time.unscaledDeltaTime;
        if (_pulse != null) _pulse.localScale = Vector3.one * Mathf.Lerp(1f, 3f, _elapsed / 0.65f);
        if (_elapsed < 0.65f) return;
        if (_pulse != null) Destroy(_pulse.gameObject);
        enabled = false;
    }

    private void OnDestroy()
    {
        if (_mesh != null) Destroy(_mesh);
        if (_pulseMesh != null) Destroy(_pulseMesh);
    }
}
