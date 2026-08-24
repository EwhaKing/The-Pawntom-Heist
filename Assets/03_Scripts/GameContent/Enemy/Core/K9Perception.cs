using UnityEngine;

namespace Pawntom.Enemy.Core
{
    /// <summary>
    /// 감지 단서의 종류. 값이 클수록 우선순위가 높다.
    /// 새 감지 수단(예: 흔적 추적)이 생겨도 이 열거형의 기존 값은 바뀌지 않는다.
    /// </summary>
    public enum K9DetectionKind
    {
        /// <summary>간접 단서. 조사(Investigate) 를 유발한다.</summary>
        Trace = 0,

        /// <summary>시야 안에서 대상을 직접 확인했다. 추격(Chase) 의 유일한 조건이다.</summary>
        Sight = 1,

        /// <summary>접촉 또는 초근접. 경계(Alert) 를 유발한다.</summary>
        Contact = 2
    }

    /// <summary>
    /// 감지 소스 하나가 이번 틱에 보고한 단서. 구조체라 힙 할당이 없다.
    /// </summary>
    public readonly struct K9Detection
    {
        /// <summary>단서가 가리키는 월드 좌표.</summary>
        public readonly Vector3 Position;

        /// <summary>단서의 종류.</summary>
        public readonly K9DetectionKind Kind;

        public K9Detection(Vector3 position, K9DetectionKind kind)
        {
            Position = position;
            Kind = kind;
        }

        public static K9Detection Trace(Vector3 position)
        {
            return new K9Detection(position, K9DetectionKind.Trace);
        }

        public static K9Detection Sight(Vector3 position)
        {
            return new K9Detection(position, K9DetectionKind.Sight);
        }

        public static K9Detection Contact(Vector3 position)
        {
            return new K9Detection(position, K9DetectionKind.Contact);
        }
    }

    /// <summary>
    /// 한 틱 동안 모든 감지 소스에서 모은 결과.
    /// 두뇌가 필드로 들고 재사용하므로 틱마다 새 인스턴스를 만들지 않는다.
    /// </summary>
    public struct K9PerceptionSnapshot
    {
        public bool HasTrace;
        public Vector3 TracePosition;

        public bool HasSight;
        public Vector3 SightPosition;

        public bool HasContact;
        public Vector3 ContactPosition;

        /// <summary>단서가 하나라도 있는가.</summary>
        public bool HasAny
        {
            get { return HasTrace || HasSight || HasContact; }
        }

        /// <summary>우선순위가 가장 높은 단서 좌표(접촉 &gt; 시야 &gt; 흔적).</summary>
        public Vector3 BestPosition
        {
            get
            {
                if (HasContact)
                {
                    return ContactPosition;
                }

                if (HasSight)
                {
                    return SightPosition;
                }

                return TracePosition;
            }
        }

        public void Clear()
        {
            HasTrace = false;
            HasSight = false;
            HasContact = false;
            TracePosition = Vector3.zero;
            SightPosition = Vector3.zero;
            ContactPosition = Vector3.zero;
        }

        /// <summary>같은 종류가 여러 번 들어오면 마지막 것이 남는다.</summary>
        public void Add(in K9Detection detection)
        {
            switch (detection.Kind)
            {
                case K9DetectionKind.Trace:
                    HasTrace = true;
                    TracePosition = detection.Position;
                    break;

                case K9DetectionKind.Sight:
                    HasSight = true;
                    SightPosition = detection.Position;
                    break;

                case K9DetectionKind.Contact:
                    HasContact = true;
                    ContactPosition = detection.Position;
                    break;
            }
        }
    }
}
