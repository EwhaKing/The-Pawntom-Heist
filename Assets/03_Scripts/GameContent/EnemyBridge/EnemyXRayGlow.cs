using System.Collections.Generic;
using Pawntom.Enemy.Authoring;
using UnityEngine;

namespace Pawntom.EnemyBridge
{
    /// <summary>
    /// 경비견을 벽 너머로 붉은 실루엣으로 비춘다(튜토리얼 6단계 순찰 안내 연출).
    /// <para>
    /// 경비견의 원래 재질·레이어·물리는 건드리지 않는다. 대신 몸의 렌더러마다 같은 메시·같은 뼈대를 쓰는
    /// 복제 렌더러(<see cref="GhostName"/>)를 자식으로 만들어 두고, 켜고 끄기만 한다.
    /// 복제 렌더러는 <c>Pawntom/Enemy XRay</c> 재질을 써서 벽 등에 가려진 부분만 그린다.
    /// </para>
    /// <para>
    /// 경비견은 네트워크 스폰이라 씬에 미리 없다. <see cref="ApplyToAll"/> 을 주기적으로 불러 새로 나온 개체도 따라잡는다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Pawntom/Enemy/Enemy XRay Glow")]
    public sealed class EnemyXRayGlow : MonoBehaviour
    {
        public const string GhostName = "XRayGhost";
        private const string MaterialPath = "EnemyXRay/Mat_EnemyXRay";

        private static Material _material;

        private readonly List<Renderer> _ghosts = new List<Renderer>(4);
        private bool _built;
        private bool _visible;

        /// <summary>지금 이 개체가 실루엣을 보이고 있는가.</summary>
        public bool IsVisible
        {
            get { return _visible; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _material = null;
        }

        /// <summary>씬의 모든 경비견에게 실루엣을 켜거나 끈다. 새로 생긴 개체에도 적용된다.</summary>
        public static void ApplyToAll(bool visible)
        {
            EnemyAgent[] agents = FindObjectsByType<EnemyAgent>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            for (int i = 0; i < agents.Length; i++)
            {
                EnemyXRayGlow glow = agents[i].GetComponent<EnemyXRayGlow>();

                if (glow == null)
                {
                    if (!visible)
                    {
                        continue; // 한 번도 켠 적 없는 개체는 끌 것이 없다
                    }

                    glow = agents[i].gameObject.AddComponent<EnemyXRayGlow>();
                }

                glow.SetVisible(visible);
            }
        }

        public void SetVisible(bool visible)
        {
            if (visible && !_built)
            {
                Build();
            }

            _visible = visible;

            for (int i = _ghosts.Count - 1; i >= 0; i--)
            {
                if (_ghosts[i] == null)
                {
                    _ghosts.RemoveAt(i);
                    continue;
                }

                _ghosts[i].enabled = visible;
            }
        }

        private void OnDisable()
        {
            // 비활성(풀 반납 등) 동안 켜진 채 남지 않게 한다. 다시 켜질 때는 ApplyToAll 이 처리한다.
            for (int i = 0; i < _ghosts.Count; i++)
            {
                if (_ghosts[i] != null)
                {
                    _ghosts[i].enabled = false;
                }
            }

            _visible = false;
        }

        private void Build()
        {
            _built = true;

            if (_material == null)
            {
                _material = Resources.Load<Material>(MaterialPath);
            }

            if (_material == null)
            {
                Debug.LogWarning("[EnemyXRayGlow] Resources/" + MaterialPath + " 재질을 찾지 못했습니다 — 경비견 실루엣을 만들 수 없습니다.", this);
                return;
            }

            SkinnedMeshRenderer[] skinned = GetComponentsInChildren<SkinnedMeshRenderer>(false);

            for (int i = 0; i < skinned.Length; i++)
            {
                SkinnedMeshRenderer source = skinned[i];

                if (!source.enabled || source.sharedMesh == null || source.gameObject.name == GhostName)
                {
                    continue;
                }

                GameObject ghostObject = NewGhostObject(source.transform);
                SkinnedMeshRenderer ghost = ghostObject.AddComponent<SkinnedMeshRenderer>();
                ghost.sharedMesh = source.sharedMesh;
                ghost.bones = source.bones;
                ghost.rootBone = source.rootBone;
                ghost.localBounds = source.localBounds;
                ghost.updateWhenOffscreen = source.updateWhenOffscreen;
                ConfigureGhost(ghost, source.sharedMesh.subMeshCount);
            }

            MeshRenderer[] meshes = GetComponentsInChildren<MeshRenderer>(false);

            for (int i = 0; i < meshes.Length; i++)
            {
                MeshRenderer source = meshes[i];
                MeshFilter filter = source.GetComponent<MeshFilter>();

                if (!source.enabled || filter == null || filter.sharedMesh == null || source.gameObject.name == GhostName)
                {
                    continue;
                }

                GameObject ghostObject = NewGhostObject(source.transform);
                ghostObject.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                MeshRenderer ghost = ghostObject.AddComponent<MeshRenderer>();
                ConfigureGhost(ghost, filter.sharedMesh.subMeshCount);
            }
        }

        private static GameObject NewGhostObject(Transform parent)
        {
            GameObject ghostObject = new GameObject(GhostName);
            ghostObject.layer = parent.gameObject.layer;
            ghostObject.transform.SetParent(parent, false);
            return ghostObject;
        }

        private void ConfigureGhost(Renderer ghost, int subMeshCount)
        {
            Material[] materials = new Material[Mathf.Max(1, subMeshCount)];

            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = _material;
            }

            ghost.sharedMaterials = materials;
            ghost.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ghost.receiveShadows = false;
            ghost.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            ghost.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            ghost.enabled = false;
            _ghosts.Add(ghost);
        }
    }
}
