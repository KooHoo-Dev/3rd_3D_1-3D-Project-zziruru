using System;
using UnityEngine;

namespace SesiVillage.Items
{
    /// <summary>
    /// 주인공이 지금 든 무기 (한 자루). Core 씬 Systems에 하나만 둔다 — PlayerOutfit과 같은 구조, 저장 대상.
    /// - 무기는 모두 양손검 동작을 쓰고 모양만 바뀐다 (기획 6장 "무기 교체 범위"). 모양은 WeaponAppearance가 Changed를 듣고 바꾼다
    /// - 맨손으로 벗는 건 없다: 무기 칸을 누르면 그 무기로 바꾸기만 함 (칼 꺼내기·넣기는 WeaponHolster가 따로)
    /// - 시작할 때·든 무기가 가방에서 없어졌을 때는 가방의 첫 무기를 든다
    /// </summary>
    public class PlayerWeapon : MonoBehaviour
    {
        public static PlayerWeapon Instance { get; private set; }

        [Tooltip("가방 (Core 씬 Systems/Inventory)")]
        [SerializeField] private Inventory inventory;

        /// <summary>지금 든 무기. null = 가방에 무기가 없음 (원래 칼 모양으로 보임)</summary>
        public ItemDefinition Equipped { get; private set; }

        /// <summary>든 무기가 바뀌었을 때 (새 무기, 없으면 null)</summary>
        public event Action<ItemDefinition> Changed;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("[PlayerWeapon] 씬에 두 개 있습니다. 하나만 두세요.", this);
                Destroy(this);
                return;
            }
            Instance = this;
            if (inventory == null) Debug.LogError("[PlayerWeapon] Inventory가 연결되지 않음", this);
        }

        // Start: 시작 아이템은 Inventory.Awake에서 들어가므로 그 뒤에 첫 무기를 고른다
        private void Start()
        {
            if (inventory == null) return;
            inventory.Changed += OnInventoryChanged;
            if (Equipped == null) Set(FirstWeaponInBag());
        }

        private void OnDestroy()
        {
            if (inventory != null) inventory.Changed -= OnInventoryChanged;
            if (Instance == this) Instance = null;
        }

        /// <summary>이 무기로 바꾸기. 무기가 아니거나 가방에 없으면 false</summary>
        public bool Equip(ItemDefinition weapon)
        {
            if (weapon == null || weapon.Type != ItemType.Weapon) return false;
            if (inventory.Count(weapon) <= 0) return false;
            Set(weapon);
            return true;
        }

        private void Set(ItemDefinition weapon)
        {
            if (Equipped == weapon) return;   // 같으면 알리지 않음
            Equipped = weapon;
            Changed?.Invoke(Equipped);
        }

        private ItemDefinition FirstWeaponInBag()
        {
            foreach (var slot in inventory.GetSlots(InventoryTab.Weapon))
                if (!slot.IsEmpty) return slot.Item;
            return null;
        }

        private void OnInventoryChanged()
        {
            if (Equipped != null && inventory.Count(Equipped) > 0) return;
            Set(FirstWeaponInBag());   // 든 무기가 없어졌거나, 맨손이었는데 무기가 생김
        }
    }
}
