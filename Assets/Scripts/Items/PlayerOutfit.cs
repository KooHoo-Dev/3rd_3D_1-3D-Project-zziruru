using System;
using System.Collections.Generic;
using UnityEngine;

namespace SesiVillage.Items
{
    /// <summary>
    /// 주인공이 지금 입은 옷. 자리(상의·하의·모자·신발)마다 한 벌씩. Core 씬 Systems에 하나만 둔다 — 인벤토리처럼 게임 상태이고 저장 대상이다.
    /// - 옷을 바꾸는 건 이 클래스만 한다. 모습을 바꾸는 쪽(OutfitAppearance)과 "착용" 표시(인벤토리 화면)는 Changed를 듣는다
    /// - 같은 자리의 옷을 입으면 그 자리의 옷만 바뀐다. 한 벌 옷(FullBody)은 상의·하의를 함께 벗기고, 반대로 상의나 하의를 입으면 한 벌 옷을 벗긴다
    /// - 입을 수 있는 건 가방에 있는 Outfit 아이템뿐 (젤다식: 입어도 가방 칸에 그대로 있고 "착용" 표시만)
    /// - 입은 옷이 가방에서 없어지면 자동으로 벗는다
    /// </summary>
    public class PlayerOutfit : MonoBehaviour
    {
        public static PlayerOutfit Instance { get; private set; }

        [Tooltip("가방 (Core 씬 Systems/Inventory)")]
        [SerializeField] private Inventory inventory;

        // 자리 → 입은 옷. 한 벌 옷은 FullBody 자리에 들어가고, 그동안 Top·Bottom 자리는 비어 있다
        private readonly Dictionary<OutfitSlot, ItemDefinition> worn = new Dictionary<OutfitSlot, ItemDefinition>();

        /// <summary>지금 입은 옷들 (자리 순서와 관계없음)</summary>
        public IEnumerable<ItemDefinition> Worn => worn.Values;

        /// <summary>입은 옷이 바뀌었을 때 (무엇이 바뀌었든 듣는 쪽은 Worn을 다시 읽는다)</summary>
        public event Action Changed;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("[PlayerOutfit] 씬에 두 개 있습니다. 하나만 두세요.", this);
                Destroy(this);
                return;
            }
            Instance = this;
            if (inventory == null) Debug.LogError("[PlayerOutfit] Inventory가 연결되지 않음", this);
        }

        private void Start()
        {
            if (inventory != null) inventory.Changed += OnInventoryChanged;
        }

        private void OnDestroy()
        {
            if (inventory != null) inventory.Changed -= OnInventoryChanged;
            if (Instance == this) Instance = null;
        }

        /// <summary>이 옷을 입고 있나 (대본 조건 "설빔을 입고 있으면" 등). 아이템 ID로 묻는다</summary>
        public bool IsWearing(string itemId)
        {
            foreach (var item in worn.Values)
                if (item.Id == itemId) return true;
            return false;
        }

        public bool IsWorn(ItemDefinition outfit) =>
            outfit != null && worn.TryGetValue(outfit.OutfitSlot, out var w) && w == outfit;

        /// <summary>입기. 옷이 아니거나 가방에 없으면 false. 같은 자리(와 겹치는 자리)의 옷은 벗긴다</summary>
        public bool Wear(ItemDefinition outfit)
        {
            if (outfit == null || outfit.Type != ItemType.Outfit) return false;
            if (inventory.Count(outfit) <= 0) return false;
            if (IsWorn(outfit)) return true;   // 이미 입음 → 알리지 않음

            foreach (var slot in BlockedBy(outfit.OutfitSlot)) worn.Remove(slot);
            worn[outfit.OutfitSlot] = outfit;
            Changed?.Invoke();
            return true;
        }

        /// <summary>이 옷 벗기 (그 자리는 기본 옷차림으로)</summary>
        public void TakeOff(ItemDefinition outfit)
        {
            if (!IsWorn(outfit)) return;
            worn.Remove(outfit.OutfitSlot);
            Changed?.Invoke();
        }

        /// <summary>인벤토리 칸을 눌렀을 때: 입은 옷이면 벗고, 아니면 입는다</summary>
        public void Toggle(ItemDefinition outfit)
        {
            if (IsWorn(outfit)) TakeOff(outfit);
            else Wear(outfit);
        }

        // 이 자리에 입으면 함께 비워야 하는 자리들 (자기 자리 포함)
        private static IEnumerable<OutfitSlot> BlockedBy(OutfitSlot slot)
        {
            yield return slot;
            if (slot == OutfitSlot.FullBody) { yield return OutfitSlot.Top; yield return OutfitSlot.Bottom; }
            if (slot == OutfitSlot.Top || slot == OutfitSlot.Bottom) yield return OutfitSlot.FullBody;
        }

        // 입은 옷을 NPC에게 주거나 해서 가방에서 없어지면 벗는다 (입은 옷과 가방이 어긋나지 않게)
        private void OnInventoryChanged()
        {
            List<OutfitSlot> gone = null;
            foreach (var pair in worn)
                if (inventory.Count(pair.Value) <= 0) (gone ??= new List<OutfitSlot>()).Add(pair.Key);
            if (gone == null) return;
            foreach (var slot in gone) worn.Remove(slot);   // 돌면서 지우면 Dictionary가 에러 → 모아 두었다가 지움
            Changed?.Invoke();
        }
    }
}
