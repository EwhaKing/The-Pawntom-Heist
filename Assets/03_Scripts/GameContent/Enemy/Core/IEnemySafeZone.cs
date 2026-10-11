using UnityEngine;

namespace Pawntom.Enemy.Core
{
    /// <summary>
    /// 적이 들어올 수 없는 구역(본부). 두뇌는 이 안을 가리키는 단서(시야·접촉·흔적)와 추적 좌표를 버린다 —
    /// 대상이 안으로 들어가면 마지막으로 본 바깥 지점(입구)까지만 쫓고 추격을 끝낸다.
    /// </summary>
    public interface IEnemySafeZone
    {
        bool Contains(Vector3 worldPoint);
    }

    /// <summary>
    /// 회전된 상자 안쪽 판정. 상자는 (월드→로컬 변환 결과의) 중심·크기로 준다.
    /// </summary>
    public static class EnemySafeBox
    {
        /// <param name="localPoint">상자 기준 로컬 좌표(Transform.InverseTransformPoint 결과)</param>
        /// <param name="center">로컬 중심(BoxCollider.center)</param>
        /// <param name="size">로컬 크기(BoxCollider.size)</param>
        /// <param name="margin">로컬 단위 여유. 양수면 상자를 그만큼 키워 판정한다</param>
        public static bool Contains(Vector3 localPoint, Vector3 center, Vector3 size, float margin)
        {
            Vector3 offset = localPoint - center;

            return Mathf.Abs(offset.x) <= size.x * 0.5f + margin &&
                   Mathf.Abs(offset.y) <= size.y * 0.5f + margin &&
                   Mathf.Abs(offset.z) <= size.z * 0.5f + margin;
        }
    }
}
