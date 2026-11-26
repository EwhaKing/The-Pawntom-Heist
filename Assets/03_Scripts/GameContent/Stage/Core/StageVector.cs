using System;
using System.Globalization;

namespace Pawntom.Stage
{
    /// <summary>
    /// 엔진 비의존 월드 좌표(m). 이 어셈블리는 UnityEngine 을 참조하지 않으므로 Vector3 대신 쓴다.
    /// 변환은 Assembly-CSharp 쪽(<c>LevelLayoutData</c>)이 한다.
    /// </summary>
    [Serializable]
    public struct StageVector : IEquatable<StageVector>
    {
        public float X;
        public float Y;
        public float Z;

        public StageVector(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static float SqrDistance(StageVector a, StageVector b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            float dz = a.Z - b.Z;
            return dx * dx + dy * dy + dz * dz;
        }

        public bool Equals(StageVector other)
        {
            return X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
        }

        public override bool Equals(object obj)
        {
            return obj is StageVector other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = X.GetHashCode();
                hash = (hash * 397) ^ Y.GetHashCode();
                hash = (hash * 397) ^ Z.GetHashCode();
                return hash;
            }
        }

        public override string ToString()
        {
            CultureInfo inv = CultureInfo.InvariantCulture;
            return "(" + X.ToString("0.##", inv) + ", " + Y.ToString("0.##", inv) + ", " + Z.ToString("0.##", inv) + ")";
        }
    }
}
