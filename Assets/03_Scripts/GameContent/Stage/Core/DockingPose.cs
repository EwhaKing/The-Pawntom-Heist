using System;

namespace Pawntom.Stage
{
    /// <summary>수평 포즈: 위치 + yaw(도, Unity 왼손 좌표 — Y 축 시계 방향).</summary>
    public readonly struct StagePose
    {
        public readonly StageVector Position;
        public readonly float Yaw;

        public StagePose(StageVector position, float yaw)
        {
            Position = position;
            Yaw = yaw;
        }
    }

    /// <summary>
    /// 본부 도킹 이동 계산(GAME_DESIGN §3.8). 본부는 기준 도킹에 붙은 채로 씬에 저장돼 있다.
    /// 선택된 도킹으로 옮길 때 "기준 도킹 포즈의 역 × 본부 포즈"(= 도킹 기준 본부의 상대 포즈)를 유지한다.
    /// 모든 피어가 같은 입력으로 같은 값을 얻는다 — 난수·시간을 쓰지 않는다.
    /// </summary>
    public static class DockingPose
    {
        private const double DegToRad = Math.PI / 180.0;

        /// <summary>Y 축 회전. Unity <c>Quaternion.Euler(0, yaw, 0) * v</c> 와 같다.</summary>
        public static StageVector RotateYaw(StageVector v, float yawDegrees)
        {
            double r = yawDegrees * DegToRad;
            double c = Math.Cos(r);
            double s = Math.Sin(r);
            return new StageVector((float)(v.X * c + v.Z * s), v.Y, (float)(-v.X * s + v.Z * c));
        }

        /// <summary>기준 → 대상 회전량(도, 0~360).</summary>
        public static float DeltaYaw(float referenceYaw, float targetYaw)
        {
            return Normalize(targetYaw - referenceYaw);
        }

        /// <summary>
        /// <paramref name="reference"/> 도킹에 붙어 있던 <paramref name="body"/> 를 <paramref name="target"/> 도킹으로 옮긴 포즈.
        /// 대상이 기준과 같으면 원래 포즈 그대로다.
        /// </summary>
        public static StagePose Relocate(StagePose reference, StagePose target, StagePose body)
        {
            var offset = new StageVector(
                body.Position.X - reference.Position.X,
                body.Position.Y - reference.Position.Y,
                body.Position.Z - reference.Position.Z);

            StageVector local = RotateYaw(offset, -reference.Yaw);
            StageVector world = RotateYaw(local, target.Yaw);

            var position = new StageVector(
                target.Position.X + world.X,
                target.Position.Y + world.Y,
                target.Position.Z + world.Z);

            return new StagePose(position, Normalize(body.Yaw + DeltaYaw(reference.Yaw, target.Yaw)));
        }

        private static float Normalize(float degrees)
        {
            float d = degrees % 360f;

            if (d < 0f)
            {
                d += 360f;
            }

            // 359.9999 같은 부동소수점 잔여는 0 으로 본다.
            return d >= 360f - 1e-4f ? 0f : d;
        }
    }
}
