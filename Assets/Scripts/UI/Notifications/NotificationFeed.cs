using System.Collections.Generic;
using SesiVillage.Items;
using SesiVillage.Quests;
using UnityEngine;

namespace SesiVillage.UI
{
    /// <summary>
    /// 게임에서 일어난 일을 알림 문장으로 바꿔 ToastStackView에 보낸다 (시스템 ↔ 알림 화면 사이의 연결부).
    /// Inventory·QuestManager는 알림을 모르고 이벤트만 낸다 → 이 컴포넌트가 듣고 "무엇을 띄울지" 정한다.
    ///  · 인벤토리: 바뀔 때마다 아이템별 개수를 지난번과 비교 → 늘면 "얻었습니다", 줄면 "건넸습니다"
    ///  · 퀘스트: 수락 / 완료
    ///  · 가방이 가득 참: Inventory.AddFailed(줍기·드롭) / QuestManager.RewardBlocked(퀘스트 보상)
    ///
    /// 절충: Inventory.Changed는 "무엇이 몇 개" 바뀌었는지 알려 주지 않아서, 직전 개수와 비교(diff)해서 알아낸다.
    /// 표준은 Inventory가 ItemAdded(아이템, 개수) 같은 이벤트를 따로 내는 것 — 그게 생기면 비교 부분을 그 이벤트로 바꾸면 된다.
    /// </summary>
    public class NotificationFeed : MonoBehaviour
    {
        private readonly Dictionary<ItemDefinition, int> lastCounts = new Dictionary<ItemDefinition, int>();
        private readonly Dictionary<ItemDefinition, int> nowCounts = new Dictionary<ItemDefinition, int>();
        private Inventory inventory;
        private QuestManager quests;

        // Start: Inventory·QuestManager의 Awake(싱글톤 등록, 시작 아이템 넣기)가 끝난 뒤 → 시작 아이템은 알림이 뜨지 않음
        private void Start()
        {
            inventory = Inventory.Instance;
            quests = QuestManager.Instance;

            if (inventory != null)
            {
                CountAll(lastCounts);
                inventory.Changed += OnInventoryChanged;
                inventory.AddFailed += OnAddFailed;
            }
            else Debug.LogWarning("[NotificationFeed] Inventory 없음 — 아이템 알림이 뜨지 않음", this);

            if (quests != null)
            {
                quests.StateChanged += OnQuestStateChanged;
                quests.RewardBlocked += OnRewardBlocked;
            }
            else Debug.LogWarning("[NotificationFeed] QuestManager 없음 — 퀘스트 알림이 뜨지 않음", this);
        }

        private void OnDestroy()
        {
            if (inventory != null)
            {
                inventory.Changed -= OnInventoryChanged;
                inventory.AddFailed -= OnAddFailed;
            }
            if (quests != null)
            {
                quests.StateChanged -= OnQuestStateChanged;
                quests.RewardBlocked -= OnRewardBlocked;
            }
        }

        // 줍기·몬스터 드롭 등 어디서든 넣기에 실패하면 (아이템은 들어가지 않은 상태)
        private void OnAddFailed(ItemDefinition item, int amount)
        {
            // 같은 아이템끼리 합침 → 잡초를 연달아 베어도 한 장에 ×N (아이템마다 key가 달라야 문장의 이름이 맞음)
            ToastStackView.Post("full:" + item.Id, $"가방이 가득 차서 {KoreanJosa.EulReul(item.DisplayName)} 받지 못했습니다", amount, item.Icon);
        }

        // 퀘스트 보상을 넣을 자리가 없을 때 (퀘스트는 보상 대기로 남음 → 가방을 비우고 다시 말을 걸면 받음)
        private void OnRewardBlocked(QuestDefinition quest)
        {
            ToastStackView.Post("fullreward:" + quest.Id, "가방이 가득 차 보상을 받지 못했습니다");
        }

        private void OnInventoryChanged()
        {
            CountAll(nowCounts);

            // 늘어난 것 (새로 생긴 아이템 포함)
            foreach (var pair in nowCounts)
            {
                lastCounts.TryGetValue(pair.Key, out int before);
                int diff = pair.Value - before;
                if (diff > 0)
                    ToastStackView.Post("get:" + pair.Key.Id, $"{KoreanJosa.EulReul(pair.Key.DisplayName)} 얻었습니다", diff, pair.Key.Icon);
            }
            // 줄어든 것 (다 없어진 아이템 포함)
            foreach (var pair in lastCounts)
            {
                nowCounts.TryGetValue(pair.Key, out int after);
                int diff = pair.Value - after;
                if (diff > 0)
                    ToastStackView.Post("give:" + pair.Key.Id, $"{KoreanJosa.EulReul(pair.Key.DisplayName)} 건넸습니다", diff, pair.Key.Icon);
            }

            // 이번 개수를 다음 비교의 기준으로 (두 Dictionary를 바꿔 써서 새로 만들지 않음)
            lastCounts.Clear();
            foreach (var pair in nowCounts) lastCounts.Add(pair.Key, pair.Value);
        }

        /// <summary>모든 탭의 칸을 돌며 아이템별 개수를 합친다 (한 아이템이 여러 칸에 나뉘어 있어도 하나로)</summary>
        private void CountAll(Dictionary<ItemDefinition, int> result)
        {
            result.Clear();
            foreach (InventoryTab tab in System.Enum.GetValues(typeof(InventoryTab)))
                foreach (var slot in inventory.GetSlots(tab))
                {
                    if (slot.IsEmpty) continue;
                    result.TryGetValue(slot.Item, out int sum);
                    result[slot.Item] = sum + slot.Count;
                }
        }

        private void OnQuestStateChanged(QuestDefinition quest, QuestState state)
        {
            string name = $"“{quest.DisplayName}”";   // 「」는 폰트에 없어 □로 나옴 → 대체 폰트(LiberationSans)에 있는 “” 사용
            switch (state)
            {
                case QuestState.InProgress:
                    ToastStackView.Post("accept:" + quest.Id, $"새 부탁 {name}");
                    break;
                case QuestState.Completed:
                    // 보상을 바로 받으면 곧이어 Rewarded가 오므로, "보상 대기"는 가방이 찼을 때만 의미가 있다 → 완료 알림은 Rewarded에서
                    break;
                case QuestState.Rewarded:
                    ToastStackView.Post("done:" + quest.Id, $"{name} 완료");
                    break;
            }
        }
    }
}
