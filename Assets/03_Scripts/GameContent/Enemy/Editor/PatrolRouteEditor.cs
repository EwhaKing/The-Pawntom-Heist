using Pawntom.Enemy.Authoring;
using UnityEditor;
using UnityEngine;

namespace Pawntom.Enemy.EditorTools
{
    /// <summary>
    /// 순찰 경로를 씬 뷰에 그린다.
    /// 지점마다 순번을 띄우고 지점 사이를 선으로 이어, 디자이너가 순서를 눈으로 확인할 수 있게 한다.
    /// </summary>
    [CustomEditor(typeof(PatrolRoute))]
    public sealed class PatrolRouteEditor : UnityEditor.Editor
    {
        private static readonly GUIStyle LabelStyle = new GUIStyle();
        private static bool _styleReady;

        private void OnSceneGUI()
        {
            PatrolRoute route = (PatrolRoute)target;
            if (route == null || route.Waypoints == null || route.Waypoints.Count == 0)
            {
                return;
            }

            EnsureStyle();

            Color color = route.LineColor;
            Handles.color = color;
            LabelStyle.normal.textColor = color;

            int count = route.Waypoints.Count;
            for (int i = 0; i < count; i++)
            {
                Transform current = route.Waypoints[i];
                if (current == null)
                {
                    continue;
                }

                Vector3 position = current.position;

                Handles.SphereHandleCap(0, position, Quaternion.identity, route.HandleRadius * 2f, EventType.Repaint);
                Handles.Label(position + Vector3.up * (route.HandleRadius + 0.35f), i.ToString(), LabelStyle);

                int nextIndex = i + 1;
                if (nextIndex >= count)
                {
                    if (!route.Loop)
                    {
                        continue;
                    }

                    nextIndex = 0;
                }

                Transform next = route.Waypoints[nextIndex];
                if (next == null)
                {
                    continue;
                }

                DrawArrow(position, next.position);
            }
        }

        private static void DrawArrow(Vector3 from, Vector3 to)
        {
            Handles.DrawLine(from, to);

            Vector3 direction = to - from;
            float length = direction.magnitude;
            if (length < 0.001f)
            {
                return;
            }

            direction /= length;
            Vector3 middle = from + direction * (length * 0.5f);
            Vector3 side = Vector3.Cross(direction, Vector3.up);
            if (side.sqrMagnitude < 0.0001f)
            {
                side = Vector3.right;
            }

            side.Normalize();

            float head = Mathf.Min(0.35f, length * 0.25f);
            Handles.DrawLine(middle, middle - direction * head + side * head * 0.5f);
            Handles.DrawLine(middle, middle - direction * head - side * head * 0.5f);
        }

        private static void EnsureStyle()
        {
            if (_styleReady)
            {
                return;
            }

            LabelStyle.fontStyle = FontStyle.Bold;
            LabelStyle.fontSize = 12;
            _styleReady = true;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            PatrolRoute route = (PatrolRoute)target;
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "웨이포인트로 쓸 빈 오브젝트를 만들어 지나갈 순서대로 넣으세요. " +
                "K-9 개체마다 서로 다른 PatrolRoute 를 지정할 수 있습니다.\n" +
                "현재 지점 수: " + route.WaypointCount,
                MessageType.Info);
        }
    }
}
