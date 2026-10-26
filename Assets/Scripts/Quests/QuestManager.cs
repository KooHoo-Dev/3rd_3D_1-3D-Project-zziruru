using System;
using System.Collections.Generic;
using SesiVillage.Items;
using UnityEngine;

namespace SesiVillage.Quests
{
    /// <summary>퀘스트를 NPC에게 보고(제출)한 결과. 대화가 이 값을 보고 대사를 고른다</summary>
    public enum TurnInResult
    {
        WrongState,      // 받지 않았거나 이미 끝난 퀘스트
        Missing,         // 목표가 아직 모자람 → NPC가 필요한 것을 다시 말함
        InventoryFull,   // 목표는 채웠지만 보상을 넣을 자리가 없음 → Completed로 남고 다음에 다시
        Rewarded,        // 완료 + 보상 받음
    }

    /// <summary>퀘스트 하나의 물건을 NPC에게 건넨 결과. 대화가 이 값을 보고 대사를 고른다</summary>
    public enum DeliverResult
    {
        WrongState,       // 진행 중인 퀘스트가 아님
        NothingToDeliver, // 이 퀘스트에서 이 NPC에게 줄 것이 없음 (이미 줬거나 원래 없음)
        Missing,          // 줄 것은 있지만 가진 개수가 모자람 → 아무것도 안 건넴
        Delivered,        // 건넴
    }

    /// <summary>
    /// 모든 퀘스트의 진행 상태를 관리한다. Core 씬에 하나만 둔다 (싱글톤).
    /// - 정의(QuestDefinition)는 읽기만, 상태(QuestProgress)는 여기서만 바꾼다
    /// - 상태는 NotAccepted → InProgress → Completed → Rewarded 순서로 한 칸씩만 바뀐다
    /// - 완료 판단은 NPC에게 보고(TryTurnIn)할 때 한 번만 한다
    ///   · Kill·Talk·Deliver: 일어날 때마다 센 개수로 판단
    ///   · Collect: 보고하는 순간 인벤토리에 가진 개수로 판단, 완료하면 그만큼 가져감
    /// - 몬스터는 Report()로 "무슨 일이 있었는지"만 알리고, 어떤 퀘스트가 자기를 세는지 모른다
    /// - NPC 대화는 플레이어가 고른 퀘스트 하나를 정해서 Deliver()·TryTurnIn()을 부른다
    ///   (한 NPC에 여러 퀘스트가 걸릴 수 있음 → GetQuestsFor()로 목록을 받아 선택지를 만든다)
    /// </summary>
    public class QuestManager : MonoBehaviour
    {
        public static QuestManager Instance { get; private set; }

        [Tooltip("게임에 있는 모든 퀘스트 정의")]
        [SerializeField] private List<QuestDefinition> allQuests = new List<QuestDefinition>();
        [Tooltip("모으기 확인·전달·보상에 쓰는 인벤토리 (Core 씬 Systems/Inventory)")]
        [SerializeField] private Inventory inventory;

        // ID → 퀘스트 정의 (찾기용), ID → 진행 상태 (게임 중에 바뀌고 저장 대상)
        private readonly Dictionary<string, QuestDefinition> definitions = new Dictionary<string, QuestDefinition>();
        private readonly Dictionary<string, QuestProgress> progress = new Dictionary<string, QuestProgress>();

        /// <summary>퀘스트 상태가 바뀌었을 때 (퀘스트 목록 UI, HUD, 알림, 자동 저장이 듣는다)</summary>
        public event Action<QuestDefinition, QuestState> StateChanged;
        /// <summary>목표 진행도가 올랐을 때 (퀘스트, 목표 번호, 현재 개수) — HUD "잡초 3/5"</summary>
        public event Action<QuestDefinition, int, int> ObjectiveProgressed;
        /// <summary>보상을 인벤토리에 넣은 뒤 (아이템, 개수) — 알림·효과음이 듣는다. 인벤토리에는 이미 들어가 있으니 다시 넣지 말 것</summary>
        public event Action<ItemDefinition, int> RewardGranted;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("[QuestManager] 씬에 QuestManager가 두 개 있습니다. 하나만 두세요.", this);
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (inventory == null) Debug.LogError("[QuestManager] Inventory가 연결되지 않음", this);

            foreach (var quest in allQuests)
            {
                if (quest == null) continue;
                if (definitions.ContainsKey(quest.Id))
                {
                    Debug.LogError($"[QuestManager] 퀘스트 ID가 겹칩니다: {quest.Id}", quest);
                    continue;
                }
                definitions.Add(quest.Id, quest);
                progress.Add(quest.Id, new QuestProgress(quest.Objectives.Count));
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        #region 읽기
        public bool TryGetDefinition(string questId, out QuestDefinition quest) => definitions.TryGetValue(questId, out quest);

        /// <summary>퀘스트 상태. 없는 ID면 에러 로그 후 NotAccepted</summary>
        public QuestState GetState(string questId)
        {
            return TryGetProgress(questId, out var p) ? p.state : QuestState.NotAccepted;
        }

        /// <summary>목표 하나의 현재 개수 (Collect는 인벤토리에 가진 개수)</summary>
        public int GetCount(string questId, int objectiveIndex)
        {
            if (!TryGetProgress(questId, out var p)) return 0;
            if (objectiveIndex < 0 || objectiveIndex >= p.counts.Length) return 0;
            var objective = definitions[questId].Objectives[objectiveIndex];
            return objective.Type == ObjectiveType.Collect ? inventory.Count(objective.Item) : p.counts[objectiveIndex];
        }

        /// <summary>아직 모자란 목표 목록 (대화가 "소금이 아직 부족하구나" 같은 대사를 고를 때)</summary>
        public List<QuestObjective> GetUnmetObjectives(string questId)
        {
            var unmet = new List<QuestObjective>();
            if (!TryGetProgress(questId, out _)) return unmet;
            var objectives = definitions[questId].Objectives;
            for (int i = 0; i < objectives.Count; i++)
                if (GetCount(questId, i) < objectives[i].RequiredCount) unmet.Add(objectives[i]);
            return unmet;
        }

        /// <summary>
        /// 이 NPC와 이야기할 거리가 있는 퀘스트 목록 (대화가 선택지를 만들 때).
        /// - 이 NPC가 주는 퀘스트: 수락할 수 있거나, 진행 중이거나, 완료했지만 보상을 못 받았을 때
        /// - 다른 NPC가 준 퀘스트: 진행 중이고 이 NPC에게 아직 건넬 물건이 있을 때
        /// </summary>
        public List<QuestDefinition> GetQuestsFor(string npcId)
        {
            var result = new List<QuestDefinition>();
            foreach (var pair in definitions)
            {
                var quest = pair.Value;
                var p = progress[pair.Key];

                if (quest.GiverNpcId == npcId)
                {
                    bool canAccept = p.state == QuestState.NotAccepted && CanAccept(quest.Id);
                    if (canAccept || p.state == QuestState.InProgress || p.state == QuestState.Completed)
                        result.Add(quest);
                }
                else if (p.state == QuestState.InProgress && HasPendingDelivery(quest, p, npcId))
                {
                    result.Add(quest);
                }
            }
            return result;
        }
        #endregion

        #region 상태 전이 (각 함수는 '지금 상태가 맞을 때만' 바꾸고, 안되면 FALSE)

        /// <summary>수락할 수 있는가: 아직 안 받았고, 선행 퀘스트를 모두 보상까지 받았을 때</summary>
        public bool CanAccept(string questId)
        {
            if (!TryGetProgress(questId, out var p) || p.state != QuestState.NotAccepted) return false;
            foreach (var pre in definitions[questId].Prerequisites)
                if (pre != null && GetState(pre.Id) != QuestState.Rewarded) return false;
            // TODO: 명절 해금 확인 — 명절 전환 시스템을 만들 때
            // 지난 명절의 퀘스트는 닫히지 않으므로 "같은 명절"이 아니라 "이미 온 명절"인지 본다 (Festival enum은 시간 순서)
            // if (definitions[questId].Festival > 현재_명절) return false;   // 아직 오지 않은 명절의 퀘스트
            return true;
        }

        /// <summary>NotAccepted → InProgress (NPC 대화에서 "수락")</summary>
        public bool Accept(string questId)
        {
            if (!CanAccept(questId))
            {
                Debug.LogWarning($"[QuestManager] 수락할 수 없음: {questId} (상태 {GetState(questId)})");
                return false;
            }
            SetState(questId, QuestState.InProgress);
            return true;
        }

        /// <summary>
        /// 처치·대화가 일어났음을 알린다 (예: 잡초가 죽을 때 Report(Kill, "weed")).
        /// 진행 중인 퀘스트에서 맞는 Kill·Talk 목표의 개수를 올린다 (필요 개수를 넘어도 계속 셈, 예: 7/5). 하나라도 올랐으면 true
        /// </summary>
        public bool Report(ObjectiveType type, string targetId)
        {
            if (type != ObjectiveType.Kill && type != ObjectiveType.Talk)
            {
                Debug.LogWarning($"[QuestManager] Report는 Kill·Talk만 셈 (받은 값: {type}). 모으기는 보고할 때 인벤토리로, 전달은 Deliver()로");
                return false;
            }
            bool anyChanged = false;
            foreach (var pair in definitions)
            {
                var p = progress[pair.Key];
                if (p.state != QuestState.InProgress) continue;   // 진행 중인 퀘스트만 센다

                var objectives = pair.Value.Objectives;
                for (int i = 0; i < objectives.Count; i++)
                {
                    if (!objectives[i].Matches(type, targetId, null)) continue;
                    p.counts[i]++;
                    ObjectiveProgressed?.Invoke(pair.Value, i, p.counts[i]);
                    anyChanged = true;
                }
            }
            return anyChanged;
        }

        /// <summary>
        /// 퀘스트 하나의 물건을 이 NPC에게 건넨다 (대화에서 플레이어가 고른 퀘스트).
        /// 이 퀘스트에서 이 NPC에게 줄 목표(Deliver)들을 **모두 채울 만큼 가지고 있을 때만** 한꺼번에 빼고 채운다.
        /// 예: 오단미에게 가죽 1 + 모피 5 → 둘 다 충분할 때만 같이 건넴. 무엇이 모자란지는 GetUnmetObjectives로
        /// </summary>
        public DeliverResult Deliver(string questId, string npcId)
        {
            if (!TryGetProgress(questId, out var p) || p.state != QuestState.InProgress) return DeliverResult.WrongState;
            var objectives = definitions[questId].Objectives;

            // 1) 이 NPC에게 줄, 아직 안 준 목표들의 필요 개수를 아이템별로 합친다
            var needed = new Dictionary<ItemDefinition, int>();
            for (int i = 0; i < objectives.Count; i++)
            {
                if (!IsPendingDelivery(objectives[i], p.counts[i], npcId)) continue;
                needed.TryGetValue(objectives[i].Item, out int sum);
                needed[objectives[i].Item] = sum + objectives[i].RequiredCount;
            }
            if (needed.Count == 0) return DeliverResult.NothingToDeliver;

            // 2) 전부 가지고 있는지 먼저 확인 (하나라도 모자라면 아무것도 안 줌)
            foreach (var need in needed)
                if (inventory.Count(need.Key) < need.Value) return DeliverResult.Missing;

            // 3) 한꺼번에 빼고 목표를 채운다
            foreach (var need in needed) inventory.Remove(need.Key, need.Value);
            for (int i = 0; i < objectives.Count; i++)
            {
                if (!IsPendingDelivery(objectives[i], p.counts[i], npcId)) continue;
                p.counts[i] = objectives[i].RequiredCount;
                ObjectiveProgressed?.Invoke(definitions[questId], i, p.counts[i]);
            }
            return DeliverResult.Delivered;
        }

        /// <summary>
        /// 퀘스트를 준 NPC에게 보고한다. 모든 목표를 채웠으면 모으기 아이템을 가져가고 → Completed → 보상 → Rewarded.
        /// 이미 Completed(전에 인벤토리가 꽉 차서 보상을 못 받음)면 보상만 다시 시도한다 (아이템을 다시 내지 않음)
        /// </summary>
        public TurnInResult TryTurnIn(string questId)
        {
            if (!TryGetProgress(questId, out var p)) return TurnInResult.WrongState;
            // 보고할 수 있는 건 진행 중이거나, 완료했지만 보상을 못 받은 퀘스트뿐 (NotAccepted·Rewarded는 여기서 끝)
            if (p.state != QuestState.InProgress && p.state != QuestState.Completed) return TurnInResult.WrongState;

            if (p.state == QuestState.InProgress)
            {
                if (GetUnmetObjectives(questId).Count > 0) return TurnInResult.Missing;

                // 모으기 아이템을 가져간다 (필요한 만큼만, 남는 건 플레이어 것)
                foreach (var objective in definitions[questId].Objectives)
                    if (objective.Type == ObjectiveType.Collect)
                        inventory.Remove(objective.Item, objective.RequiredCount);
                SetState(questId, QuestState.Completed);
            }

            // 여기서는 항상 Completed
            return GiveRewards(questId) ? TurnInResult.Rewarded : TurnInResult.InventoryFull;
        }

        /// <summary>Completed → Rewarded. 보상을 전부 넣을 자리가 없으면 아무것도 안 넣고 false (Completed로 남음)</summary>
        private bool GiveRewards(string questId)
        {
            var rewards = definitions[questId].Rewards;

            // 보상 전부를 함께 넣을 자리가 있는지 한 번에 확인 (하나씩 물으면 같은 빈 칸을 두 번 셀 수 있음)
            if (!inventory.CanAddAll(rewards))
            {
                Debug.LogWarning($"[QuestManager] 인벤토리에 자리가 없어 보상 보류: {questId}");
                return false;
            }

            // 상태를 먼저 바꾼 뒤 보상을 준다 → 보상 처리 중에 다시 불려도 두 번 나가지 않음
            SetState(questId, QuestState.Rewarded);
            foreach (var reward in rewards)
            {
                if (reward.Item == null) continue;
                // CanAddAll을 통과했으므로 실패할 수 없음. 실패하면 CanAddAll 계산 버그
                if (!inventory.Add(reward.Item, reward.Count))
                {
                    Debug.LogError($"[QuestManager] 보상을 넣지 못함 (CanAddAll 버그): {questId} → {reward.Item.DisplayName} x{reward.Count}", this);
                    break;
                }
                RewardGranted?.Invoke(reward.Item, reward.Count);
                Debug.Log($"[QuestManager] 보상: {reward.Item.DisplayName} x{reward.Count}");
            }
            return true;
        }
        #endregion

        /// <summary>이 목표가 "이 NPC에게 아직 건네지 않은 전달 목표"인가 (Deliver·GetQuestsFor가 같은 기준을 쓰도록)</summary>
        private static bool IsPendingDelivery(QuestObjective objective, int count, string npcId)
        {
            return objective.Type == ObjectiveType.Deliver && objective.TargetId == npcId && count < objective.RequiredCount;
        }

        private static bool HasPendingDelivery(QuestDefinition quest, QuestProgress p, string npcId)
        {
            for (int i = 0; i < quest.Objectives.Count; i++)
                if (IsPendingDelivery(quest.Objectives[i], p.counts[i], npcId)) return true;
            return false;
        }

        private bool TryGetProgress(string questId, out QuestProgress p)
        {
            if (questId != null && progress.TryGetValue(questId, out p)) return true;
            Debug.LogError($"[QuestManager] 없는 퀘스트 ID: '{questId}'", this);
            p = null;
            return false;
        }

        private void SetState(string questId, QuestState next)
        {
            progress[questId].state = next;
            Debug.Log($"[QuestManager] {questId}: {next}");
            StateChanged?.Invoke(definitions[questId], next);
        }
    }
}
