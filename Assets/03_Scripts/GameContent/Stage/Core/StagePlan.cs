using System;
using System.Collections.Generic;

namespace Pawntom.Stage
{
    /// <summary>아이템 지점 하나에 나올 아이템.</summary>
    public readonly struct ItemPlacement : IEquatable<ItemPlacement>
    {
        /// <summary><see cref="StageLayout.ItemSpots"/> 안의 인덱스.</summary>
        public readonly int SpotIndex;
        public readonly string SpotId;
        public readonly int ItemId;

        public ItemPlacement(int spotIndex, string spotId, int itemId)
        {
            SpotIndex = spotIndex;
            SpotId = spotId;
            ItemId = itemId;
        }

        public bool Equals(ItemPlacement other)
        {
            return SpotIndex == other.SpotIndex && ItemId == other.ItemId && string.Equals(SpotId, other.SpotId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is ItemPlacement other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (SpotIndex * 397) ^ ItemId;
            }
        }

        public override string ToString()
        {
            return SpotId + "#" + SpotIndex + " -> item " + ItemId;
        }
    }

    /// <summary>
    /// 시드 하나로 정한 스테이지 배치. 호스트가 만들고, 시드만 공유하면 다른 피어도 같은 결과를 다시 만든다.
    /// 인덱스는 모두 원본 <see cref="StageLayout"/> 목록 기준이다.
    /// </summary>
    public sealed class StagePlan
    {
        public int Seed { get; }

        /// <summary>선택된 도킹 후보 인덱스. 후보가 없으면 -1.</summary>
        public int DockingIndex { get; }

        /// <summary>열쇠를 놓을 <see cref="StageLayout.KeySpots"/> 인덱스(중복 없음, 뽑힌 순서).</summary>
        public List<int> KeySpotIndices { get; }

        /// <summary>필요 열쇠 수가 후보보다 많아 후보 수로 잘랐는가.</summary>
        public bool KeysTruncated { get; }

        public List<ItemPlacement> Items { get; }

        /// <summary>K-9 에게 배정할 <see cref="StageLayout.PatrolRoutes"/> 인덱스(중복 없음, 뽑힌 순서 = K-9 번호 순).</summary>
        public List<int> PatrolRouteIndices { get; }

        /// <summary>K-9 수가 경로 수보다 많아 경로 수로 잘랐는가.</summary>
        public bool PatrolRoutesTruncated { get; }

        public StagePlan(int seed, int dockingIndex, List<int> keySpotIndices, bool keysTruncated, List<ItemPlacement> items,
            List<int> patrolRouteIndices, bool patrolRoutesTruncated)
        {
            Seed = seed;
            DockingIndex = dockingIndex;
            KeySpotIndices = keySpotIndices ?? new List<int>();
            KeysTruncated = keysTruncated;
            Items = items ?? new List<ItemPlacement>();
            PatrolRouteIndices = patrolRouteIndices ?? new List<int>();
            PatrolRoutesTruncated = patrolRoutesTruncated;
        }
    }
}
