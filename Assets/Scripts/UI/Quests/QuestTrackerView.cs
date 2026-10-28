using System.Collections;
using System.Collections.Generic;
using SesiVillage.Items;
using SesiVillage.Quests;
using UnityEngine;

namespace SesiVillage.UI
{
    /// <summary>
    /// HUD 오른쪽 위 "지금 할 일" (기획 6장 UI "HUD"). 수업 UI 갤러리 QuestTrackerView를 우리 QuestManager에 맞게 바꾼 것.
    /// 자리와 크기를 바꾸지 않아 시선이 습관처럼 돌아온다. 진행 중인 퀘스트만 보이고, 끝나면 "√"를 잠깐 보여 준 뒤 접힌다.
    /// 게임 쪽(QuestManager)은 이 화면을 모른다 — 이벤트만 듣는다.
    /// </summary>
    public class QuestTrackerView : MonoBehaviour
    {
        [SerializeField, Tooltip("블록이 쌓일 곳 (Vertical Layout Group)")]
        private Transform listParent;
        [SerializeField, Tooltip("복제할 블록 원본 (꺼 둔 상태로 listParent 아래)")]
        private QuestTrackerEntry entryTemplate;
        [SerializeField, Tooltip("목표 옆에 (3/5) 개수를 보일까 — 기획 미정 7번, 플레이하면서 결정")]
        private bool showCounts;

        private readonly Dictionary<string, QuestTrackerEntry> entries = new Dictionary<string, QuestTrackerEntry>();
        private QuestManager quests;
        private Inventory inventory;

        private void Awake()
        {
            entryTemplate.gameObject.SetActive(false);
        }

        private void Start()
        {
            quests = QuestManager.Instance;
            inventory = Inventory.Instance;
            if (quests == null) { Debug.LogError("[QuestTrackerView] QuestManager 없음", this); return; }
            quests.StateChanged += OnStateChanged;
            quests.ObjectiveProgressed += OnProgress;
            // 모으기 목표는 인벤토리 개수로 세므로, 개수를 보일 때는 인벤토리가 바뀔 때도 다시 쓴다
            if (inventory != null) inventory.Changed += RefreshAll;
        }

        private void OnDestroy()
        {
            if (quests != null)
            {
                quests.StateChanged -= OnStateChanged;
                quests.ObjectiveProgressed -= OnProgress;
            }
            if (inventory != null) inventory.Changed -= RefreshAll;
        }

        private void OnStateChanged(QuestDefinition quest, QuestState state)
        {
            switch (state)
            {
                case QuestState.InProgress:
                    if (entries.ContainsKey(quest.Id)) break;
                    var e = Instantiate(entryTemplate, listParent);
                    e.gameObject.SetActive(true);
                    e.transform.SetAsFirstSibling();   // 새 퀘스트가 맨 위
                    e.Init(quest.Id, quest.DisplayName);
                    e.SetObjectives(QuestText.Objectives(quest, state, showCounts));
                    entries.Add(quest.Id, e);
                    StartCoroutine(e.Expand());
                    break;

                case QuestState.Completed:   // 보상 대기 — "보상을 받으러 가자"가 붙음
                    if (entries.TryGetValue(quest.Id, out var c)) c.SetObjectives(QuestText.Objectives(quest, state, showCounts));
                    break;

                case QuestState.Rewarded:
                    if (entries.TryGetValue(quest.Id, out var r))
                    {
                        entries.Remove(quest.Id);
                        r.SetObjectives(QuestText.Objectives(quest, state, showCounts));
                        StartCoroutine(CollapseAndRemove(r));
                    }
                    break;
            }
        }

        private IEnumerator CollapseAndRemove(QuestTrackerEntry e)
        {
            yield return e.HoldAndCollapse();
            Destroy(e.gameObject);
        }

        private void OnProgress(QuestDefinition quest, int objectiveIndex, int count)
        {
            if (showCounts && entries.TryGetValue(quest.Id, out var e))
                e.SetObjectives(QuestText.Objectives(quest, quests.GetState(quest.Id), true));
        }

        private void RefreshAll()
        {
            if (!showCounts) return;
            foreach (var pair in entries)
                if (quests.TryGetDefinition(pair.Key, out var q))
                    pair.Value.SetObjectives(QuestText.Objectives(q, quests.GetState(q.Id), true));
        }
    }
}
