using System;
using System.Collections.Generic;

namespace Pawntom.Stage
{
    /// <summary>계획에서 나온 아이템 스폰 1건(열쇠 또는 전리품).</summary>
    public readonly struct StageItemSpawn : IEquatable<StageItemSpawn>
    {
        public readonly string SpotId;
        public readonly StageVector Position;
        public readonly int ItemId;
        public readonly bool IsKey;

        public StageItemSpawn(string spotId, StageVector position, int itemId, bool isKey)
        {
            SpotId = spotId;
            Position = position;
            ItemId = itemId;
            IsKey = isKey;
        }

        public bool Equals(StageItemSpawn other)
        {
            return ItemId == other.ItemId && IsKey == other.IsKey && Position.Equals(other.Position)
                && string.Equals(SpotId, other.SpotId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is StageItemSpawn other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Position.GetHashCode() * 397) ^ ItemId ^ (IsKey ? 1 : 0);
            }
        }

        public override string ToString()
        {
            return (IsKey ? "key " : "item ") + ItemId + " @" + SpotId + " " + Position;
        }
    }

    /// <summary>
    /// <see cref="StagePlan"/> → 실제 스폰 목록. 호스트만 스폰하지만 같은 레이아웃·시드면 어느 피어가 계산해도 같다.
    /// 인덱스가 레이아웃 범위를 벗어난 항목(오래된 계획 등)은 조용히 건너뛴다.
    /// </summary>
    public static class StageSpawnList
    {
        /// <summary>열쇠(계획 순서) → 전리품(계획 순서). 기존 내용은 지운다.</summary>
        public static void BuildItemSpawns(StageLayout layout, StagePlan plan, int keyItemId, List<StageItemSpawn> results)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            results.Clear();

            for (int i = 0; i < plan.KeySpotIndices.Count; i++)
            {
                int index = plan.KeySpotIndices[i];

                if (index < 0 || index >= layout.KeySpots.Count || layout.KeySpots[index] == null)
                {
                    continue;
                }

                KeySpot spot = layout.KeySpots[index];
                results.Add(new StageItemSpawn(spot.Id, spot.Position, keyItemId, true));
            }

            for (int i = 0; i < plan.Items.Count; i++)
            {
                ItemPlacement placement = plan.Items[i];

                if (placement.SpotIndex < 0 || placement.SpotIndex >= layout.ItemSpots.Count || layout.ItemSpots[placement.SpotIndex] == null)
                {
                    continue;
                }

                ItemSpot spot = layout.ItemSpots[placement.SpotIndex];
                results.Add(new StageItemSpawn(spot.Id, spot.Position, placement.ItemId, false));
            }
        }

        /// <summary>K-9 번호 순으로 배정된 순찰 경로 Id(= 씬 PatrolRoute GameObject 이름, 굽기 도구 규칙). 기존 내용은 지운다.</summary>
        public static void BuildPatrolRouteIds(StageLayout layout, StagePlan plan, List<string> results)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            results.Clear();

            for (int i = 0; i < plan.PatrolRouteIndices.Count; i++)
            {
                int index = plan.PatrolRouteIndices[i];

                if (index < 0 || index >= layout.PatrolRoutes.Count || layout.PatrolRoutes[index] == null)
                {
                    continue;
                }

                results.Add(layout.PatrolRoutes[index].Id);
            }
        }
    }
}
