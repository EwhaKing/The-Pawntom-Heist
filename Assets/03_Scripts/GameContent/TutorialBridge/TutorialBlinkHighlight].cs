using System.Collections.Generic;
using UnityEngine;

namespace Pawntom.TutorialBridge
{
    /// <summary>
    /// 오브젝트를 연초록으로 천천히 깜빡이게 한다(지금 상호작용할 대상이라는 표시). 길 안내 화살표는 목적지 가까이에서
    /// 끝나 버려서, 바로 앞에 서 있으면 무엇을 하라는지 놓치기 쉽다 — 대상 자체가 빛나게 해서 보완한다.
    /// <para>
    /// 머티리얼을 복제하지 않고 MaterialPropertyBlock 으로 기본 색(_BaseColor / _Color)만 초록 쪽으로 섞는다.
    /// 끄면 블록을 지워 원래 모습으로 돌아간다. 아래 Renderer 는 처음 켤 때 한 번 모은다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TutorialBlinkHighlight : MonoBehaviour
    {
        private static readonly Color GlowColor = new Color(0.45f, 1f, 0.4f, 1f);
        private const float PeriodSeconds = 1.6f;   // 한 번 밝아졌다 어두워지는 데 걸리는 시간
        private const float MaxBlend = 0.75f;       // 가장 밝을 때 원래 색에서 초록으로 섞는 비율

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private struct Slot
        {
            public Renderer Renderer;
            public int MaterialIndex;
            public Color Original;
            public bool HasBaseColor;
            public bool HasColor;
        }

        private readonly List<Slot> _slots = new List<Slot>(8);
        private MaterialPropertyBlock _block;
        private bool _collected;
        private bool _on;
        private float _time;

        /// <summary>깜빡임을 켜거나 끈다. 같은 값이면 아무것도 하지 않는다.</summary>
        public void SetOn(bool on)
        {
            if (_on == on)
            {
                return;
            }

            _on = on;
            _time = 0f;

            if (!on)
            {
                Restore();
            }
        }

        private void Update()
        {
            if (!_on)
            {
                return;
            }

            if (!_collected)
            {
                Collect();
            }

            _time += Time.unscaledDeltaTime;
            float wave = 0.5f - 0.5f * Mathf.Cos(_time * Mathf.PI * 2f / PeriodSeconds); // 0 → 1 → 0
            float blend = wave * MaxBlend;

            for (int i = 0; i < _slots.Count; i++)
            {
                Slot slot = _slots[i];

                if (slot.Renderer == null)
                {
                    continue;
                }

                Color color = Color.Lerp(slot.Original, GlowColor, blend);
                color.a = slot.Original.a;

                slot.Renderer.GetPropertyBlock(_block, slot.MaterialIndex);

                if (slot.HasBaseColor)
                {
                    _block.SetColor(BaseColorId, color);
                }

                if (slot.HasColor)
                {
                    _block.SetColor(ColorId, color);
                }

                slot.Renderer.SetPropertyBlock(_block, slot.MaterialIndex);
            }
        }

        private void OnDisable()
        {
            Restore();
            _on = false;
        }

        private void Collect()
        {
            _collected = true;
            _block = new MaterialPropertyBlock();

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

            for (int r = 0; r < renderers.Length; r++)
            {
                Renderer target = renderers[r];

                // 파티클·선 같은 효과용 렌더러는 건드리지 않는다.
                if (!(target is MeshRenderer) && !(target is SkinnedMeshRenderer))
                {
                    continue;
                }

                Material[] materials = target.sharedMaterials;

                for (int m = 0; m < materials.Length; m++)
                {
                    Material material = materials[m];

                    if (material == null)
                    {
                        continue;
                    }

                    bool hasBase = material.HasProperty(BaseColorId);
                    bool hasColor = material.HasProperty(ColorId);

                    if (!hasBase && !hasColor)
                    {
                        continue;
                    }

                    _slots.Add(new Slot
                    {
                        Renderer = target,
                        MaterialIndex = m,
                        Original = hasBase ? material.GetColor(BaseColorId) : material.GetColor(ColorId),
                        HasBaseColor = hasBase,
                        HasColor = hasColor
                    });
                }
            }
        }

        private void Restore()
        {
            if (_block == null)
            {
                return;
            }

            for (int i = 0; i < _slots.Count; i++)
            {
                Slot slot = _slots[i];

                if (slot.Renderer == null)
                {
                    continue;
                }

                _block.Clear();
                slot.Renderer.SetPropertyBlock(_block, slot.MaterialIndex);
            }
        }
    }
}