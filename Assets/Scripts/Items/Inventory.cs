using System;
using System.Collections.Generic;
using UnityEngine;

namespace SesiVillage.Items
{
    /// <summary>아이템 + 개수 (시작 아이템, 퀘스트 보상 등). 정의 데이터라서 실행 중에 바꾸지 않는다</summary>
    [Serializable]
    public struct ItemAmount
    {
        // 필드 이름(item, count)을 바꾸면 이미 저장된 에셋·씬의 값이 사라지므로 바꾸지 말 것
        [SerializeField] private ItemDefinition item;
        [Min(1)]
        [SerializeField] private int count;

        public ItemDefinition Item => item;
        public int Count => count;

        public ItemAmount(ItemDefinition item, int count)
        {
            this.item = item;
            this.count = count;
        }
    }
    
    /// <summary>인벤토리 한 칸: 어떤 아이템이 몇 개. 빈 칸은 Item == null</summary>
    public class InventorySlot
    {
        public ItemDefinition Item { get; private set; }
        public int Count { get; private set; }
        
        public bool IsEmpty => Item == null;

        // 칸 내용은 Inventory만 바꾼다 (다른 코드는 읽기만)
        internal void Set(ItemDefinition item, int count)
        {
            if (item == null || count <= 0) { Item = null; Count = 0; }
            else { Item = item; Count = count; }
        }
    }

    /// <summary>
    /// 플레이어 인벤토리. 위쪽 탭(무기 / 옷 / 아이템·재료)마다 칸 목록이 따로 있다.
    /// 게임 중에 바뀌는 상태라서 Core 씬에 하나만 두고, 저장 대상이다.
    /// - 아이템을 넣고 빼는 건 이 클래스만 한다. 다른 코드는 Count·GetSlots로 읽기만 한다
    /// - 아이템이 들어갈 탭은 아이템 종류로 정해진다 (ItemDefinition.Tab)
    /// - 넣기·빼기는 "전부 되거나 하나도 안 되거나": 일부만 들어가거나 일부만 빠지는 일이 없다
    /// </summary>
    public class Inventory : MonoBehaviour
    {
        public static Inventory Instance { get; private set; }
        
        /// <summary>탭마다 칸 수. 아이템·재료는 먹을 것·재료·퀘스트 물건이 함께 써서 더 많다</summary>
        public static int SlotCountOf(InventoryTab tab) => tab switch
        {
            InventoryTab.Item => 26,
            _ => 5,     // 무기, 옷
        };
        
        [Tooltip("시작할 때 들고 있는 아이템 (예: 이전 명절 보상을 미리 지급)")]
        [SerializeField] private List<ItemAmount> startingItems = new List<ItemAmount>();
        
        private readonly Dictionary<InventoryTab, List<InventorySlot>> tabs = new Dictionary<InventoryTab, List<InventorySlot>>();

        /// <summary>내용이 바뀌었을 때 (인벤토리 UI, 알림, 자동 저장이 듣는다)</summary>
        public event Action Changed;
        /// <summary>자리가 없어 넣지 못했을 때 (아이템, 개수) — 알림 "가방이 가득 찼습니다"가 듣는다. 줍기·드롭·보상 어디서 실패해도 여기로 모임</summary>
        public event Action<ItemDefinition, int> AddFailed;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("[Inventory] 씬에 Inventory가 두 개 있습니다. 하나만 두세요.", this);
                Destroy(gameObject);
                return;
            }
            Instance = this;
            
            foreach (InventoryTab tab in Enum.GetValues(typeof(InventoryTab)))
            {
                int slotCount = SlotCountOf(tab);
                var slots = new List<InventorySlot>(slotCount);
                for (int i = 0; i < slotCount; i++) slots.Add(new InventorySlot());
                tabs.Add(tab, slots);
            }

            // 칸을 다 만든 다음에 시작 아이템을 넣는다 (순서 중요)
            foreach (var start in startingItems)
                if (!Add(start.Item, start.Count))
                    Debug.LogWarning($"[Inventory] 시작 아이템을 넣지 못함: {(start.Item ? start.Item.name : "비어 있음")} x{start.Count}", this);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
        
        /// <summary>한 탭의 칸 목록 (UI가 읽기만 한다)</summary>
        public IReadOnlyList<InventorySlot> GetSlots(InventoryTab tab) => tabs[tab];
        private List<InventorySlot> SlotsFor(ItemDefinition item) => tabs[item.Tab];
        
        /// <summary>이 아이템을 모두 몇 개 가지고 있는가 (여러 칸에 나뉘어 있어도 합침)</summary>
        public int Count(ItemDefinition item)
        {
            if (item == null) return 0;
            int total = 0;
            foreach (var slot in SlotsFor(item))
                if (slot.Item == item) total += slot.Count;
            return total;
        }
        
        /// <summary>amount개를 전부 넣을 자리가 있는가</summary>
        public bool CanAdd(ItemDefinition item, int amount)
        {
            if (item == null || amount <= 0) return false;
            return HasSpace(SlotsFor(item), item, amount);
        }

        /// <summary>
        /// 여러 아이템을 "전부 함께" 넣을 자리가 있는가 (예: 퀘스트 보상이 여러 개일 때).
        /// 하나씩 CanAdd로 물으면 서로 같은 빈 칸을 세서 틀릴 수 있어서,
        /// 칸을 복사한 가짜 인벤토리에 차례로 넣어 본다 (실제 칸은 바꾸지 않음)
        /// </summary>
        public bool CanAddAll(IReadOnlyList<ItemAmount> items)
        {
            // 기존 인벤을 복사하는 부분
            var copies = new Dictionary<InventoryTab, List<InventorySlot>>();
            foreach (var pair in tabs)
            {
                var copy = new List<InventorySlot>(pair.Value.Count);
                foreach (var slot in pair.Value)
                {
                    var c = new InventorySlot();
                    c.Set(slot.Item, slot.Count);
                    copy.Add(c);
                }
                copies.Add(pair.Key, copy);
            }

            foreach (var entry in items)
            {
                if (entry.Item == null) continue;   // 빈 보상 칸은 건너뜀 (GiveRewards도 건너뜀 → 두 쪽 기준이 같아야 함)
                var slots = copies[entry.Item.Tab];
                if (!HasSpace(slots, entry.Item, entry.Count)) return false;
                Fill(slots, entry.Item, entry.Count);   // 앞 아이템이 쓴 자리를 다음 아이템이 다시 세지 않게
            }
            return true;
        }

        /// <summary>넣기. 자리가 모자라면 하나도 넣지 않고 false</summary>
        public bool Add(ItemDefinition item, int amount = 1)
        {
            if (!CanAdd(item, amount))
            {
                // 아이템이 비었거나 개수가 0 이하인 건 호출 실수라 알리지 않음 — "자리가 없음"일 때만
                if (item != null && amount > 0) AddFailed?.Invoke(item, amount);
                return false;
            }
            Fill(SlotsFor(item), item, amount);
            Changed?.Invoke();
            return true;
        }

        // ── 칸 계산 (CanAdd·CanAddAll·Add가 같은 규칙을 쓰도록 한 곳에 둔다) ──

        private static bool HasSpace(List<InventorySlot> slots, ItemDefinition item, int amount)
        {
            int space = 0;
            foreach (var slot in slots)
            {
                if (slot.IsEmpty) space += item.MaxStack;
                // Max: 최대 개수를 나중에 줄여서 이미 넘친 칸이 자리를 깎지 않게
                else if (slot.Item == item) space += Mathf.Max(0, item.MaxStack - slot.Count);

                if (space >= amount) return true;
            }
            return false;
        }

        /// <summary>자리가 있다는 걸 확인한 뒤에만 부른다</summary>
        private static void Fill(List<InventorySlot> slots, ItemDefinition item, int amount)
        {
            int left = amount;
            // 1) 같은 아이템이 있는 칸부터 채운다 (겹치기)
            foreach (var slot in slots)
            {
                if (left == 0) break;
                if (slot.Item != item || slot.Count >= item.MaxStack) continue;
                int put = Mathf.Min(left, item.MaxStack - slot.Count);
                slot.Set(item, slot.Count + put);
                left -= put;
            }
            // 2) 남으면 앞에서부터 빈 칸에
            foreach (var slot in slots)
            {
                if (left == 0) break;
                if (!slot.IsEmpty) continue;
                int put = Mathf.Min(left, item.MaxStack);
                slot.Set(item, put);
                left -= put;
            }
        }

        /// <summary>빼기. 모자라면 하나도 빼지 않고 false</summary>
        public bool Remove(ItemDefinition item, int amount = 1)
        {
            if (item == null || amount <= 0 || Count(item) < amount) return false;

            var slots = SlotsFor(item);
            int left = amount;
            // 뒤 칸부터 뺀다 → 앞 칸(a, b…)에 있는 묶음이 오래 남아 칸이 덜 흩어짐
            for (int i = slots.Count - 1; i >= 0 && left > 0; i--)
            {
                var slot = slots[i];
                if (slot.Item != item) continue;
                int take = Mathf.Min(left, slot.Count);
                slot.Set(item, slot.Count - take);
                left -= take;
            }

            Changed?.Invoke();
            return true;
        }
        
#if UNITY_EDITOR
        // UI가 생기기 전 확인용: Play 중 컴포넌트 ⋮ 메뉴에서 실행
        [ContextMenu("인벤토리 내용 출력")]
        private void PrintContents()
        {
            var sb = new System.Text.StringBuilder("[Inventory]");
            foreach (var pair in tabs)   // pair.Key = 탭, pair.Value = 그 탭의 칸 List
            {
                var slots = pair.Value;
                sb.Append($"\n[{pair.Key}] {slots.Count}칸");
                for (int i = 0; i < slots.Count; i++)
                    if (!slots[i].IsEmpty)
                        sb.Append($"\n  {i}: {slots[i].Item.DisplayName} x{slots[i].Count}");
            }
            Debug.Log(sb.ToString(), this);
        }
#endif
    }
}
