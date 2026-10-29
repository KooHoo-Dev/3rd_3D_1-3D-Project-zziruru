using System;
using System.Collections.Generic;
using UnityEngine;

namespace SesiVillage.Items
{
    /// <summary>
    /// 주인공이 지금 입고 든 장비. 자리(EquipSlot)마다 하나씩. Core 씬 Systems에 하나만 둔다 — 인벤토리처럼 게임 상태이고 저장 대상이다.
    /// - 장비를 바꾸는 건 이 클래스만 한다. 외형·무기 동작·"착용" 표시는 Changed를 듣고 다시 읽는다
    /// - 입을 수 있는 건 가방에 있는 장비뿐 (젤다식: 입어도 가방 칸에 그대로 있고 "착용" 표시만)
    /// - 한 벌 옷(FullBody)은 상의·하의와 함께 입을 수 없다 → 입으면 겹치는 자리를 비운다
    /// - 무기도 벗을 수 있다 → 무기 자리가 비면 J/K가 맨손 전투 (WeaponHolster가 Changed를 듣고 맞춤)
    /// - 입은 장비가 가방에서 없어지면 자동으로 벗는다 (다시 자동으로 입히지는 않음)
    /// </summary>
    public class PlayerEquipment : MonoBehaviour
    {
        public static PlayerEquipment Instance { get; private set; }

        [Tooltip("가방 (Core 씬 Systems/Inventory)")]
        [SerializeField] private Inventory inventory;
        [Tooltip("새 게임을 시작할 때 입고 있는 장비 (예: 목도). 인벤토리 시작 아이템에도 들어 있어야 입힐 수 있다")]
        [SerializeField] private List<EquipmentItem> startingEquipment = new List<EquipmentItem>();

        // 자리 → 지금 장비. 한 벌 옷을 입으면 FullBody에 들어가고, 그동안 Top·Bottom은 비어 있다
        private readonly Dictionary<EquipSlot, EquipmentItem> equipped = new Dictionary<EquipSlot, EquipmentItem>();

        /// <summary>장비가 바뀌었을 때. 무엇이 바뀌었든 듣는 쪽은 TryGet·IsEquipped로 다시 읽는다</summary>
        public event Action Changed;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("[PlayerEquipment] 씬에 두 개 있습니다. 하나만 두세요.", this);
                Destroy(this);
                return;
            }
            Instance = this;
            if (inventory == null) Debug.LogError("[PlayerEquipment] Inventory가 연결되지 않음", this);
        }

        // Start: 시작 아이템은 Inventory.Awake에서 들어가므로, 그 뒤에 시작 장비를 입힌다
        private void Start()
        {
            if (inventory == null) return;
            inventory.Changed += OnInventoryChanged;

            foreach (var item in startingEquipment)
            {
                if (item == null) continue;
                if (inventory.Count(item) > 0) Put(item);
                else Debug.LogWarning($"[PlayerEquipment] 시작 장비 {item.DisplayName}이(가) 가방에 없어 입히지 못함 — Inventory 시작 아이템에도 넣으세요", this);
            }
            if (equipped.Count > 0) Changed?.Invoke();
        }

        private void OnDestroy()
        {
            if (inventory != null) inventory.Changed -= OnInventoryChanged;
            if (Instance == this) Instance = null;
        }

        /* ───────── 읽기 ───────── */

        /// <summary>이 자리에 무엇을 입었나 (외형·무기 동작이 자리별로 읽는다)</summary>
        public bool TryGet(EquipSlot slot, out EquipmentItem item) => equipped.TryGetValue(slot, out item);

        /// <summary>이 아이템을 입거나 들고 있나 (인벤토리 "착용" 표시). 장비가 아닌 아이템은 항상 false</summary>
        public bool IsEquipped(ItemDefinition item) =>
            item is EquipmentItem equipment
            && equipped.TryGetValue(equipment.Slot, out var current)
            && current == equipment;

        /// <summary>이 ID의 장비를 입고 있나 (대본 조건 "설빔을 입고 있으면")</summary>
        public bool IsWearing(string itemId)
        {
            foreach (var item in equipped.Values)
                if (item.Id == itemId) return true;
            return false;
        }

        /* ───────── 바꾸기 ───────── */

        /// <summary>입기·들기. 가방에 없으면 false. 같은 자리와 겹치는 자리의 장비는 벗긴다</summary>
        public bool Equip(EquipmentItem item)
        {
            if (item == null || inventory == null) return false;
            if (inventory.Count(item) <= 0) return false;
            if (IsEquipped(item)) return true;   // 이미 입음 → 알리지 않음

            Put(item);
            Changed?.Invoke();
            return true;
        }

        /// <summary>벗기. 무기를 벗으면 맨손이 된다</summary>
        public bool Unequip(EquipmentItem item)
        {
            if (!IsEquipped(item)) return false;

            equipped.Remove(item.Slot);
            Changed?.Invoke();
            return true;
        }

        /// <summary>인벤토리 칸을 눌렀을 때: 입은 것이면 벗고, 아니면 입는다</summary>
        public void Toggle(EquipmentItem item)
        {
            if (IsEquipped(item)) Unequip(item);
            else Equip(item);
        }

        /* ───────── 저장·불러오기 (저장 시스템이 부른다) ───────── */

        /// <summary>저장할 값: 지금 장비의 아이템 ID 목록 (맨손이면 무기 ID가 없음)</summary>
        public List<string> GetEquippedIds()
        {
            var ids = new List<string>(equipped.Count);
            foreach (var item in equipped.Values) ids.Add(item.Id);
            return ids;
        }

        /// <summary>불러오기. 저장된 그대로 복원한다 (시작 장비는 쓰지 않음). ⚠️ 인벤토리를 먼저 복원한 뒤 부를 것</summary>
        public void Restore(IEnumerable<string> ids, ItemDatabase database)
        {
            equipped.Clear();
            if (ids != null && database != null)
            {
                foreach (var id in ids)
                {
                    if (!database.TryGet(id, out var definition))
                    {
                        Debug.LogWarning($"[PlayerEquipment] 저장된 장비 '{id}'를 찾지 못함", this);
                        continue;
                    }
                    if (definition is not EquipmentItem item) continue;   // 장비가 아님 (데이터가 바뀐 경우)
                    if (inventory.Count(item) > 0) Put(item);
                }
            }
            Changed?.Invoke();   // 다 넣은 뒤 한 번만 알림
        }

        /* ───────── 내부 ───────── */

        // 알리지 않고 넣기만 (여러 개를 넣고 마지막에 한 번 알릴 때도 씀)
        private void Put(EquipmentItem item)
        {
            foreach (var slot in BlockedBy(item.Slot)) equipped.Remove(slot);
            equipped[item.Slot] = item;
        }

        // 이 자리에 입으면 함께 비워야 하는 자리들 (자기 자리 포함)
        private static IEnumerable<EquipSlot> BlockedBy(EquipSlot slot)
        {
            yield return slot;
            if (slot == EquipSlot.FullBody)
            {
                yield return EquipSlot.Top;
                yield return EquipSlot.Bottom;
            }
            else if (slot == EquipSlot.Top || slot == EquipSlot.Bottom)
            {
                yield return EquipSlot.FullBody;
            }
        }

        // 입은 장비가 가방에서 없어지면 벗는다 (NPC에게 줬을 때 등)
        private void OnInventoryChanged()
        {
            List<EquipSlot> gone = null;
            foreach (var pair in equipped)
                if (inventory.Count(pair.Value) <= 0) (gone ??= new List<EquipSlot>()).Add(pair.Key);
            if (gone == null) return;

            // 돌면서 바로 지우면 에러 → 모아 두었다가 다 돈 뒤에 지운다
            foreach (var slot in gone) equipped.Remove(slot);
            Changed?.Invoke();
        }

#if UNITY_EDITOR
        // Play 중에 컴포넌트 이름 오른쪽 ⋮ → 이 메뉴로 지금 장비를 Console에 찍는다 (확인용)
        [ContextMenu("로그: 지금 장비")]
        private void LogEquipped()
        {
            if (equipped.Count == 0) { Debug.Log("[PlayerEquipment] 장비 없음 (맨손)", this); return; }
            foreach (var pair in equipped)
                Debug.Log($"[PlayerEquipment] {pair.Key}: {pair.Value.DisplayName} ({pair.Value.Id})", this);
        }
#endif
    }
}