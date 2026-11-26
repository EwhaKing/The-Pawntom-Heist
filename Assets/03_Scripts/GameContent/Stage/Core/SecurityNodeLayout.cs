using System.Collections.Generic;

namespace Pawntom.Stage
{
    /// <summary>
    /// 분전반(보안 노드)을 레벨 데이터에 기록한다(엔진 비의존). 굽기 도구가 분전반의 <c>AppendTo</c> 를 부를 때 쓴다.
    /// <para>
    /// 분전반 1개 = <see cref="PowerPanelEntry"/> 1개. 해금 구역은 명시값이 있으면 그것, 없으면 연결된 첫 구역이다.
    /// 연결된 구역은 아직 다른 기록원이 없으므로 여기서 <see cref="ZoneEntry"/> 로 함께 기록하되,
    /// 이미 있는 Id 는 다시 넣지 않는다(두 분전반이 같은 구역을 열어도 Id 중복 오류가 나지 않게).
    /// </para>
    /// </summary>
    public static class SecurityNodeLayout
    {
        public static void Append(StageLayout layout, string panelId, StageVector position, string unlockZoneId, IReadOnlyList<string> linkedZoneIds)
        {
            if (layout == null)
            {
                return;
            }

            string unlock = IsBlank(unlockZoneId) ? FirstNonBlank(linkedZoneIds) : unlockZoneId.Trim();
            layout.PowerPanels.Add(new PowerPanelEntry(panelId, position, unlock ?? string.Empty));

            if (linkedZoneIds != null)
            {
                for (int i = 0; i < linkedZoneIds.Count; i++)
                {
                    AddZoneIfMissing(layout, linkedZoneIds[i]);
                }
            }

            AddZoneIfMissing(layout, unlock);
        }

        private static void AddZoneIfMissing(StageLayout layout, string zoneId)
        {
            if (IsBlank(zoneId))
            {
                return;
            }

            string id = zoneId.Trim();

            for (int i = 0; i < layout.Zones.Count; i++)
            {
                if (layout.Zones[i] != null && string.Equals(layout.Zones[i].Id, id, System.StringComparison.Ordinal))
                {
                    return;
                }
            }

            layout.Zones.Add(new ZoneEntry(id));
        }

        private static string FirstNonBlank(IReadOnlyList<string> ids)
        {
            if (ids == null)
            {
                return null;
            }

            for (int i = 0; i < ids.Count; i++)
            {
                if (!IsBlank(ids[i]))
                {
                    return ids[i].Trim();
                }
            }

            return null;
        }

        private static bool IsBlank(string value)
        {
            return value == null || value.Trim().Length == 0;
        }
    }
}
