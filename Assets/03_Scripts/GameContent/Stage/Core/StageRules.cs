using System;
using System.Collections.Generic;

namespace Pawntom.Stage
{
    /// <summary>
    /// 스테이지 배치 규칙(GAME_DESIGN §3.8, §3.6). 호스트가 시드 하나로 도킹·열쇠·아이템을 정한다.
    /// <para>
    /// 난수는 <see cref="StageRandom"/> 만 쓴다. <see cref="CreatePlan"/> 의 소비 순서(도킹 → 열쇠 → 아이템)가 바뀌면
    /// 같은 시드라도 배치가 바뀐다 — 순서를 바꿀 때는 모든 피어가 같은 빌드를 쓰는지 확인한다.
    /// </para>
    /// </summary>
    public static class StageRules
    {
        /// <summary>GAME_DESIGN §3.8: 1층에 도킹 지점 3곳 사전 배치.</summary>
        public const int MinDockingCandidates = 3;

        /// <summary>순찰 경로는 최소 두 지점을 오가야 경로다.</summary>
        public const int MinWaypointsPerRoute = 2;

        /// <summary>단계별 K-9 수 기본값(오케스트레이터 지시, 2026-11-25). 경로 풀은 약 13개.</summary>
        public const int DefaultK9Count = 6;

        /// <summary>GAME_DESIGN §3.8 단계 표: 1~4단계.</summary>
        public const int MaxStageNumber = 4;

        /// <summary>아이템 지점이 열쇠 후보 지점과 이 거리(m) 안이면 같은 자리로 보고 아이템을 놓지 않는다.</summary>
        public const float ItemKeyOverlapRadius = 0.5f;

        /// <summary>GAME_DESIGN §3.8: N단계는 열쇠 N개. 1 미만은 1로 본다.</summary>
        public static int RequiredKeysForStage(int stageNumber)
        {
            return stageNumber < 1 ? 1 : stageNumber;
        }

        /// <summary>도킹 후보 중 1곳의 인덱스. 후보가 없으면 -1.</summary>
        public static int PickDocking(StageRandom rng, int candidateCount)
        {
            if (rng == null)
            {
                throw new ArgumentNullException(nameof(rng));
            }

            return candidateCount <= 0 ? -1 : rng.NextInt(candidateCount);
        }

        /// <summary>
        /// 열쇠 후보 중 <paramref name="requiredCount"/> 곳을 중복 없이 고른다(부분 Fisher–Yates).
        /// 결과는 <paramref name="results"/> 에 뽑힌 순서대로 채운다(기존 내용은 지운다).
        /// </summary>
        /// <returns>필요 수가 후보보다 많아 후보 수로 잘랐으면 true.</returns>
        public static bool PickKeySpots(StageRandom rng, int candidateCount, int requiredCount, List<int> results)
        {
            return PickDistinct(rng, candidateCount, requiredCount, results);
        }

        /// <summary>
        /// 순찰 경로 풀(<paramref name="routeCount"/>개) 중 K-9 <paramref name="k9Count"/> 마리에게 줄 경로를 중복 없이 고른다.
        /// 결과 순서가 K-9 번호 순이다. 기존 내용은 지운다.
        /// </summary>
        /// <returns>K-9 수가 경로 수보다 많아 경로 수로 잘랐으면 true.</returns>
        public static bool PickPatrolRoutes(StageRandom rng, int routeCount, int k9Count, List<int> results)
        {
            return PickDistinct(rng, routeCount, k9Count, results);
        }

        /// <summary>[0, count) 에서 take 개를 중복 없이 고른다(부분 Fisher–Yates). 잘렸으면 true.</summary>
        private static bool PickDistinct(StageRandom rng, int candidateCount, int requiredCount, List<int> results)
        {
            if (rng == null)
            {
                throw new ArgumentNullException(nameof(rng));
            }

            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            results.Clear();

            if (requiredCount <= 0)
            {
                return false;
            }

            int count = candidateCount < 0 ? 0 : candidateCount;
            bool truncated = requiredCount > count;
            int take = truncated ? count : requiredCount;

            for (int i = 0; i < count; i++)
            {
                results.Add(i);
            }

            for (int i = 0; i < take; i++)
            {
                int j = i + rng.NextInt(count - i);
                int swap = results[i];
                results[i] = results[j];
                results[j] = swap;
            }

            results.RemoveRange(take, count - take);
            return truncated;
        }

        /// <summary>
        /// 아이템 지점마다 확률을 굴려 나올 아이템을 정한다. 결과는 <paramref name="results"/> 에 채운다(기존 내용은 지운다).
        /// <para>
        /// 열쇠 후보 지점(<paramref name="keySpots"/>)과 <see cref="ItemKeyOverlapRadius"/> 안에 있는 지점은 건너뛴다 —
        /// 한 자리에 열쇠와 아이템이 겹쳐 나오지 않게 한다. 허용 아이템이 없는 지점도 건너뛴다.
        /// 건너뛴 지점은 난수를 소비하지 않는다.
        /// </para>
        /// </summary>
        public static void RollItems(StageRandom rng, IReadOnlyList<ItemSpot> spots, IReadOnlyList<KeySpot> keySpots, List<ItemPlacement> results)
        {
            if (rng == null)
            {
                throw new ArgumentNullException(nameof(rng));
            }

            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            results.Clear();

            if (spots == null)
            {
                return;
            }

            for (int i = 0; i < spots.Count; i++)
            {
                ItemSpot spot = spots[i];

                if (spot == null || spot.AllowedItemIds.Count == 0 || OverlapsAnyKeySpot(spot.Position, keySpots))
                {
                    continue;
                }

                // NaN 은 비교가 모두 false 라 "나오지 않음"이 된다.
                if (!(rng.NextFloat01() < spot.SpawnChance))
                {
                    continue;
                }

                int itemId = spot.AllowedItemIds[rng.NextInt(spot.AllowedItemIds.Count)];
                results.Add(new ItemPlacement(i, spot.Id, itemId));
            }
        }

        /// <summary>
        /// 시드 하나로 배치 전체를 정한다. 난수 소비 순서: 도킹 → 열쇠 → 아이템 → 순찰 경로.
        /// 새 판정은 맨 뒤에 붙인다 — 앞 판정의 결과가 바뀌지 않게.
        /// 아이템은 열쇠 후보 지점 전체와 겹치는 자리를 피한다(뽑히지 않은 후보 포함 — 열쇠 풀과 아이템 풀을 분리).
        /// </summary>
        public static StagePlan CreatePlan(StageLayout layout, int seed)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            var rng = new StageRandom(seed);

            int docking = PickDocking(rng, layout.DockingCandidates.Count);

            var keys = new List<int>(layout.RequiredKeyCount > 0 ? layout.RequiredKeyCount : 0);
            bool truncated = PickKeySpots(rng, layout.KeySpots.Count, layout.RequiredKeyCount, keys);

            var items = new List<ItemPlacement>(layout.ItemSpots.Count);
            RollItems(rng, layout.ItemSpots, layout.KeySpots, items);

            var routes = new List<int>(layout.K9Count > 0 ? layout.K9Count : 0);
            bool routesTruncated = PickPatrolRoutes(rng, layout.PatrolRoutes.Count, layout.K9Count, routes);

            return new StagePlan(seed, docking, keys, truncated, items, routes, routesTruncated);
        }

        /// <summary>레이아웃 검증. 오류가 없으면 빈 목록.</summary>
        public static List<StageLayoutIssue> Validate(StageLayout layout)
        {
            var issues = new List<StageLayoutIssue>();
            Validate(layout, issues);
            return issues;
        }

        /// <summary>레이아웃 검증. 오류를 <paramref name="issues"/> 에 덧붙인다.</summary>
        public static void Validate(StageLayout layout, List<StageLayoutIssue> issues)
        {
            if (issues == null)
            {
                throw new ArgumentNullException(nameof(issues));
            }

            if (layout == null)
            {
                issues.Add(new StageLayoutIssue(StageLayoutIssueCode.MissingLayout, "Layout", null, "레벨 데이터가 없다."));
                return;
            }

            if (layout.StageNumber < 1 || layout.StageNumber > MaxStageNumber)
            {
                issues.Add(new StageLayoutIssue(StageLayoutIssueCode.InvalidStageNumber, "Layout", null,
                    "단계 번호 " + layout.StageNumber + " — 1~" + MaxStageNumber + " 이어야 한다."));
            }

            if (layout.FloorNumber < 1 || layout.FloorNumber > layout.StageNumber)
            {
                issues.Add(new StageLayoutIssue(StageLayoutIssueCode.InvalidFloorNumber, "Layout", null,
                    "층 번호 " + layout.FloorNumber + " — " + layout.StageNumber + "단계에서 접근 가능한 층은 1~" + layout.StageNumber + "층이다."));
            }

            if (layout.DockingCandidates.Count < MinDockingCandidates)
            {
                issues.Add(new StageLayoutIssue(StageLayoutIssueCode.TooFewDockingCandidates, "Docking", null,
                    "도킹 후보 " + layout.DockingCandidates.Count + "곳 — 최소 " + MinDockingCandidates + "곳."));
            }

            if (layout.RequiredKeyCount < 1)
            {
                issues.Add(new StageLayoutIssue(StageLayoutIssueCode.InvalidRequiredKeyCount, "Key", null,
                    "필요 열쇠 수 " + layout.RequiredKeyCount + " — 1 이상이어야 한다."));
            }
            else if (layout.RequiredKeyCount > layout.KeySpots.Count)
            {
                issues.Add(new StageLayoutIssue(StageLayoutIssueCode.RequiredKeysExceedCandidates, "Key", null,
                    "필요 열쇠 " + layout.RequiredKeyCount + "개 > 열쇠 후보 " + layout.KeySpots.Count + "곳."));
            }

            if (layout.K9Count < 0)
            {
                issues.Add(new StageLayoutIssue(StageLayoutIssueCode.InvalidK9Count, "PatrolRoute", null,
                    "K-9 수 " + layout.K9Count + " — 0 이상이어야 한다."));
            }
            else if (layout.K9Count > layout.PatrolRoutes.Count)
            {
                issues.Add(new StageLayoutIssue(StageLayoutIssueCode.K9CountExceedsRoutes, StageLayoutIssueSeverity.Warning, "PatrolRoute", null,
                    "K-9 " + layout.K9Count + "마리 > 순찰 경로 " + layout.PatrolRoutes.Count + "개 — 경로 수만큼만 나온다."));
            }

            CheckIds(layout.DockingCandidates, "Docking", issues);
            CheckIds(layout.ItemSpots, "ItemSpot", issues);
            CheckIds(layout.KeySpots, "KeySpot", issues);
            CheckIds(layout.PatrolRoutes, "PatrolRoute", issues);
            CheckIds(layout.K9Spawns, "K9Spawn", issues);
            CheckIds(layout.BrutusSpawns, "BrutusSpawn", issues);
            CheckIds(layout.Doors, "Door", issues);
            CheckIds(layout.Vents, "Vent", issues);
            CheckIds(layout.PowerPanels, "PowerPanel", issues);
            CheckIds(layout.Zones, "Zone", issues);
            CheckIds(layout.Vaults, "Vault", issues);

            for (int i = 0; i < layout.ItemSpots.Count; i++)
            {
                ItemSpot spot = layout.ItemSpots[i];
                if (spot == null)
                {
                    continue;
                }

                if (float.IsNaN(spot.SpawnChance) || spot.SpawnChance < 0f || spot.SpawnChance > 1f)
                {
                    issues.Add(new StageLayoutIssue(StageLayoutIssueCode.InvalidSpawnChance, "ItemSpot", spot.Id,
                        "스폰 확률 " + spot.SpawnChance + " — 0~1 이어야 한다."));
                }

                if (spot.AllowedItemIds.Count == 0)
                {
                    issues.Add(new StageLayoutIssue(StageLayoutIssueCode.NoAllowedItems, "ItemSpot", spot.Id,
                        "허용 아이템이 없어 아무것도 나오지 않는다."));
                }
            }

            for (int i = 0; i < layout.PatrolRoutes.Count; i++)
            {
                PatrolRouteEntry route = layout.PatrolRoutes[i];
                if (route != null && route.Waypoints.Count < MinWaypointsPerRoute)
                {
                    issues.Add(new StageLayoutIssue(StageLayoutIssueCode.TooFewWaypoints, "PatrolRoute", route.Id,
                        "웨이포인트 " + route.Waypoints.Count + "개 — 최소 " + MinWaypointsPerRoute + "개."));
                }
            }

            for (int i = 0; i < layout.K9Spawns.Count; i++)
            {
                K9Spawn spawn = layout.K9Spawns[i];
                if (spawn != null && !IsBlank(spawn.RouteId) && !ContainsId(layout.PatrolRoutes, spawn.RouteId))
                {
                    issues.Add(new StageLayoutIssue(StageLayoutIssueCode.UnknownRouteReference, "K9Spawn", spawn.Id,
                        "순찰 경로 '" + spawn.RouteId + "' 가 레이아웃에 없다."));
                }
            }

            for (int i = 0; i < layout.PowerPanels.Count; i++)
            {
                PowerPanelEntry panel = layout.PowerPanels[i];
                if (panel != null && !ContainsId(layout.Zones, panel.UnlockZoneId))
                {
                    issues.Add(new StageLayoutIssue(StageLayoutIssueCode.UnknownZoneReference, "PowerPanel", panel.Id,
                        "해금 구역 '" + panel.UnlockZoneId + "' 가 레이아웃에 없다."));
                }
            }
        }

        private static bool OverlapsAnyKeySpot(StageVector position, IReadOnlyList<KeySpot> keySpots)
        {
            if (keySpots == null)
            {
                return false;
            }

            const float sqrRadius = ItemKeyOverlapRadius * ItemKeyOverlapRadius;

            for (int i = 0; i < keySpots.Count; i++)
            {
                KeySpot key = keySpots[i];
                if (key != null && StageVector.SqrDistance(position, key.Position) <= sqrRadius)
                {
                    return true;
                }
            }

            return false;
        }

        private static void CheckIds<T>(List<T> entries, string category, List<StageLayoutIssue> issues) where T : class, IStageLayoutEntry
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < entries.Count; i++)
            {
                T entry = entries[i];

                if (entry == null || IsBlank(entry.Id))
                {
                    issues.Add(new StageLayoutIssue(StageLayoutIssueCode.MissingId, category, null,
                        (i + 1) + "번째 항목에 Id 가 없다."));
                    continue;
                }

                if (!seen.Add(entry.Id))
                {
                    issues.Add(new StageLayoutIssue(StageLayoutIssueCode.DuplicateId, category, entry.Id,
                        "Id 가 중복된다."));
                }
            }
        }

        private static bool ContainsId<T>(List<T> entries, string id) where T : class, IStageLayoutEntry
        {
            if (IsBlank(id))
            {
                return false;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                T entry = entries[i];
                if (entry != null && string.Equals(entry.Id, id, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsBlank(string value)
        {
            return string.IsNullOrWhiteSpace(value);
        }
    }
}
