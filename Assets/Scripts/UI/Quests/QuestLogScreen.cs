using System.Collections.Generic;
using SesiVillage.Items;
using SesiVillage.Quests;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SesiVillage.UI
{
    /// <summary>
    /// 퀘스트 목록 (Q). 왼쪽에 진행 중 / 완료 목록, 누르면 오른쪽에 할 일 + 필요한 것 (1개면 이름만, 2개 이상이면 가진 개수 / 총개수).
    /// (기획 6장은 "조용한 형 — 개수 없음"이었으나 2026-10-28 사용자 결정으로 상세에서는 보임)
    ///
    /// 받은 퀘스트를 어떻게 아나: QuestManager.StateChanged를 듣고, 처음 InProgress가 된 순서대로 기억한다.
    /// 절충: QuestManager에 "모든 퀘스트" 읽기 목록이 없어서 이벤트로 모은다. 저장·불러오기를 만들 때 불러온 상태로도
    /// StateChanged가 나가게 하거나, QuestManager에 AllQuests 읽기 속성을 두고 여기서 상태로 거르는 게 더 단단하다.
    /// </summary>
    public class QuestLogScreen : MenuScreen
    {
        [Header("목록 (왼쪽)")]
        [SerializeField, Tooltip("목록이 쌓일 곳 (Vertical Layout Group)")]
        private RectTransform list;
        [SerializeField, Tooltip("\"진행 중\" / \"완료\" 제목 원본 (꺼 둔 상태로 list 아래)")]
        private TMP_Text headerTemplate;
        [SerializeField, Tooltip("퀘스트 한 줄 원본 (Button + 자식 TMP 글자, 꺼 둔 상태로 list 아래)")]
        private Button entryTemplate;
        [SerializeField] private TMP_Text emptyLabel;

        [Header("설명 (오른쪽)")]
        [SerializeField] private GameObject detailRoot;
        [SerializeField] private TMP_Text detailTitle;
        [SerializeField] private TMP_Text detailState;
        [SerializeField] private TMP_Text detailDescription;
        [SerializeField] private TMP_Text detailObjectives;

        [SerializeField] private Color selectedColor = new Color32(196, 150, 90, 255);
        [SerializeField] private Color normalColor = new Color32(0, 0, 0, 0);

        private readonly List<QuestDefinition> known = new List<QuestDefinition>();   // 받은 순서
        private readonly List<GameObject> spawned = new List<GameObject>();            // 다시 그릴 때 지울 줄들
        private QuestDefinition selected;

        protected override void Awake()
        {
            base.Awake();
            headerTemplate.gameObject.SetActive(false);
            entryTemplate.gameObject.SetActive(false);
        }

        // 화면이 닫혀 있어도 받은 퀘스트는 기억해야 하므로 시작부터 듣는다
        private void Start()
        {
            if (QuestManager.Instance != null) QuestManager.Instance.StateChanged += OnQuestStateChanged;
            else Debug.LogError("[QuestLogScreen] QuestManager 없음", this);
            // 가진 개수가 바뀌면 상세의 "잡초 3 / 5"도 바뀌어야 함
            if (Inventory.Instance != null) Inventory.Instance.Changed += OnInventoryChanged;
        }

        private void OnDestroy()
        {
            if (QuestManager.Instance != null) QuestManager.Instance.StateChanged -= OnQuestStateChanged;
            if (Inventory.Instance != null) Inventory.Instance.Changed -= OnInventoryChanged;
        }

        // 목록은 그대로, 오른쪽 상세만 다시 쓴다
        private void OnInventoryChanged()
        {
            if (IsOpen) DrawDetail();
        }

        private void OnQuestStateChanged(QuestDefinition quest, QuestState state)
        {
            if (state != QuestState.NotAccepted && !known.Contains(quest)) known.Add(quest);
            if (IsOpen) Redraw();
        }

        protected override void OnOpened()
        {
            // 고른 게 없으면 진행 중인 첫 퀘스트를 고른다
            if (selected == null)
                foreach (var q in known)
                    if (!IsFinished(q)) { selected = q; break; }
            if (selected == null && known.Count > 0) selected = known[0];
            Redraw();
        }

        private void Redraw()
        {
            // 줄 수가 적고(퀘스트 십여 개) 화면을 열 때·상태가 바뀔 때만 그리므로, 매번 지우고 새로 만든다 (단순함 우선)
            foreach (var go in spawned) Destroy(go);
            spawned.Clear();

            AddSection("진행 중", false);
            AddSection("완료", true);
            emptyLabel.gameObject.SetActive(known.Count == 0);
            DrawDetail();
        }

        private void AddSection(string title, bool finished)
        {
            bool any = false;
            foreach (var q in known)
            {
                if (IsFinished(q) != finished) continue;
                if (!any)
                {
                    var header = Instantiate(headerTemplate, list);
                    header.text = title;
                    header.gameObject.SetActive(true);
                    spawned.Add(header.gameObject);
                    any = true;
                }
                var entry = Instantiate(entryTemplate, list);
                entry.gameObject.SetActive(true);   // 원본이 꺼져 있어 복사본도 꺼진 채 → 켜야 GetComponentInChildren이 자식을 찾음
                entry.GetComponentInChildren<TMP_Text>().text = q.DisplayName;
                entry.GetComponent<Image>().color = q == selected ? selectedColor : normalColor;
                var quest = q;   // 람다가 이번 값을 기억하도록 복사
                entry.onClick.AddListener(() => { selected = quest; Redraw(); });
                spawned.Add(entry.gameObject);
            }
        }

        private void DrawDetail()
        {
            detailRoot.SetActive(selected != null);
            if (selected == null) return;
            var state = QuestManager.Instance.GetState(selected.Id);
            detailTitle.text = selected.DisplayName;
            detailState.text = StateName(state);
            detailDescription.text = selected.Description;
            detailObjectives.text = QuestText.Objectives(selected, state, showCounts: true);
        }

        /// <summary>목록의 "완료" 칸에 들어가는가. 보상을 못 받은 Completed는 아직 할 일(보상 받기)이 있어서 진행 중에 둔다</summary>
        private static bool IsFinished(QuestDefinition q) => QuestManager.Instance.GetState(q.Id) == QuestState.Rewarded;

        private static string StateName(QuestState state) => state switch
        {
            QuestState.InProgress => "진행 중",
            QuestState.Completed => "보상 대기",
            QuestState.Rewarded => "완료",
            _ => "",
        };
    }
}
