using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace Pawntom.TutorialBridge
{
    /// <summary>솔로 튜토리얼 단계 진입 시의 위치·소지품·월드 아이템·격벽·기믹 상태.</summary>
    public sealed class TutorialStageCheckpoint
    {
        public int Stage { get; }
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        private readonly int[] _slots = new int[InventoryManager.MaxSlotCount];
        private readonly int _selectedSlot;
        private readonly List<ItemState> _items = new List<ItemState>();
        private readonly List<System.Action> _restoreWorld = new List<System.Action>();

        private struct ItemState
        {
            public NetworkObject Prefab;
            public int ItemId;
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Scale;
        }

        public TutorialStageCheckpoint(int stage, PlayerData player)
        {
            Stage = stage;
            Position = player.transform.position;
            Rotation = player.transform.rotation;
            InventoryManager inventory = player.GetComponent<InventoryManager>();
            _selectedSlot = inventory != null ? inventory.SelectedSlotIndex : -1;
            for (int i = 0; i < _slots.Length; i++) _slots[i] = inventory != null ? inventory.Slots[i] : 0;

            foreach (NetworkItem item in Object.FindObjectsByType<NetworkItem>(FindObjectsSortMode.None))
            {
                if (item.Object == null || !item.Object.IsValid || item.Runner != player.Runner || item.IsPickedUp) continue;
                ItemData data = ItemDatabase.Instance != null ? ItemDatabase.Instance.GetItemData(item.ItemId) : null;
                NetworkObject prefab = data != null && data.modelPrefab != null ? data.modelPrefab.GetComponent<NetworkObject>() : null;
                if (prefab == null || prefab.GetComponent<NetworkItem>() == null) continue;
                _items.Add(new ItemState { Prefab = prefab, ItemId = item.ItemId, Position = item.transform.position,
                    Rotation = item.transform.rotation, Scale = item.transform.localScale });
            }
            foreach (ControllableWall wall in Object.FindObjectsByType<ControllableWall>(FindObjectsSortMode.None))
            {
                if (wall.Object == null || !wall.Object.IsValid || wall.Runner != player.Runner) continue;
                bool unlocked = wall.IsUnlocked, open = wall.IsOpen;
                _restoreWorld.Add(() => { if (wall != null && wall.Object != null && wall.Object.IsValid) wall.RestoreTutorialCheckpoint(unlocked, open); });
            }
            foreach (TutorialSecurityNode node in TutorialSecurityNode.All)
            {
                TutorialSecurityNode captured = node;
                bool connected = node.IsConnected;
                _restoreWorld.Add(() => { if (captured != null) captured.RestoreCheckpoint(connected); });
            }
            foreach (TutorialVault vault in TutorialVault.All)
            {
                TutorialVault captured = vault;
                bool opened = vault.IsOpened;
                _restoreWorld.Add(() => { if (captured != null) captured.RestoreCheckpoint(opened); });
            }
            foreach (var enemy in Pawntom.Enemy.Authoring.EnemyAgent.Active)
            {
                var captured = enemy;
                Vector3 position = enemy.transform.position;
                Quaternion rotation = enemy.transform.rotation;
                _restoreWorld.Add(() => { if (captured != null) captured.ResetAfterTeleport(position, rotation); });
            }
        }

        public void RestoreWorld(PlayerData player)
        {
            // 체크포인트 뒤에 주웠거나 사망으로 떨어뜨린 아이템이 복제되지 않게 월드를 복구합니다.
            foreach (NetworkItem item in Object.FindObjectsByType<NetworkItem>(FindObjectsSortMode.None))
            {
                if (item.Object == null || !item.Object.IsValid || item.Runner != player.Runner || !item.Object.HasStateAuthority) continue;
                ItemData data = ItemDatabase.Instance != null ? ItemDatabase.Instance.GetItemData(item.ItemId) : null;
                if (data != null && data.modelPrefab != null && data.modelPrefab.GetComponent<NetworkItem>() != null)
                    player.Runner.Despawn(item.Object);
            }
            foreach (ItemState item in _items)
            {
                NetworkObject spawned = player.Runner.Spawn(item.Prefab, item.Position, item.Rotation,
                    onBeforeSpawned: (runner, obj) => obj.GetComponent<NetworkItem>().Init(item.ItemId));
                if (spawned != null) spawned.transform.localScale = item.Scale;
            }
            InventoryManager inventory = player.GetComponent<InventoryManager>();
            if (inventory != null)
            {
                for (int i = 0; i < _slots.Length; i++) inventory.Slots.Set(i, _slots[i]);
                inventory.SelectedSlotIndex = _selectedSlot;
            }
            foreach (var restore in _restoreWorld) restore();
        }
    }
}
