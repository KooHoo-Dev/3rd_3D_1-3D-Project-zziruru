using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using SesiVillage.Items;
using UnityEngine;

namespace SesiVillage.Quests
{
    /// <summary>목표 종류. 몬스터·아이템·NPC가 QuestManager에 "무슨 일이 있었는지" 알릴 때도 이 값을 쓴다.</summary>
    public enum ObjectiveType
    {
        Collect,   // 아이템 줍기 (item)
        Kill,      // 몬스터 처치 (targetId = 몬스터 ID)
        Talk,      // NPC와 대화 (targetId = NPC ID)
        Deliver,   // 아이템을 NPC에게 전달 (item + targetId = NPC ID)
    }
    
    // TODO: QuestObjective 는 나중에 [SerializeReference] 로 확장 안할경우 struct 로 바꿔도 됨
    /// <summary>목표 하나: "무엇을(종류) 누구/무엇에게(대상) 몇 번(개수)"</summary>
    [Serializable]
    public class QuestObjective
    {
        [SerializeField] private ObjectiveType type;
        [Tooltip("Kill = 몬스터 ID, Talk·Deliver = NPC ID. Collect는 비워 둠")]
        [SerializeField] private string targetId;
        [Tooltip("Collect·Deliver일 때 대상 아이템")]
        [SerializeField] private ItemDefinition item;
        [Min(1)]
        [SerializeField] private int requiredCount = 1;
        [Tooltip("퀘스트 목록·HUD에 보일 문장 (예: 묘지의 잡초 처치)")]
        [SerializeField] private string description;

        public ObjectiveType Type => type;
        public string TargetId => targetId;
        public ItemDefinition Item => item;
        public int RequiredCount => requiredCount;
        public string Description => description;
        
        /// <summary>Inspector에서 새로 추가된 칸은 0으로 시작하므로 1 이상으로 맞춘다.</summary>
        public void FixInvalidValues()
        {
            if (requiredCount < 1) requiredCount = 1;
        }
        
        /// <summary>보고된 일(종류·대상·아이템)이 이 목표에 해당하는가</summary>
        public bool Matches(ObjectiveType reportedType, string reportedTargetId, ItemDefinition reportedItem)
        {
            if (type != reportedType) return false;
            switch (type)
            {
                case ObjectiveType.Collect: return item == reportedItem;
                case ObjectiveType.Kill:
                case ObjectiveType.Talk:    return targetId == reportedTargetId;
                case ObjectiveType.Deliver: return item == reportedItem && targetId == reportedTargetId;
                default:                    return false;
            }
        }
    }
    
    /// <summary>퀘스트가 속한 명절. 명절 해금·이동에 쓴다.</summary>
    public enum Festival
    {
        Gimjang,   // 김장절 (음력 10월)
        Dongji,    // 동지 (11월)
        Seotdal,   // 섣달 (12월)
        Seollal,   // 설날 + 대보름 (정월)
    }
    
    /// <summary>
    /// 퀘스트 정의 (게임 중에 바뀌지 않는 정보). 진행 상태(수락 여부, 몇 개 했는지)는 QuestManager가 따로 관리한다.
    /// 퀘스트 하나를 추가할 때 이 에셋 1개 + 대화 1개만 만들면 되도록, 퀘스트 내용을 코드에 쓰지 않는다.
    /// </summary>
    [CreateAssetMenu(fileName = "Quest_", menuName = "SesiVillage/Quest Definition")]
    public class QuestDefinition : ScriptableObject
    {
        private static readonly Regex IdRule = new Regex("^[a-z0-9_]+$");
        
        [Tooltip("영문 소문자·숫자·밑줄만. 저장 파일·대화(Yarn)에서 이 이름으로 찾는다. 만든 뒤에는 바꾸지 말 것")]
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [TextArea(2, 4)]
        [SerializeField] private string description;
        [Tooltip("퀘스트를 주는 NPC의 ID")]
        [SerializeField] private string giverNpcId;
        [SerializeField] private Festival festival;
        [Tooltip("이 퀘스트들을 보상까지 받아야 수락할 수 있음")]
        [SerializeField] private List<QuestDefinition> prerequisites = new List<QuestDefinition>();
        [SerializeField] private List<QuestObjective> objectives = new List<QuestObjective>();
        [SerializeField] private List<ItemAmount> rewards = new List<ItemAmount>();
        
        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public string GiverNpcId => giverNpcId;
        public Festival Festival => festival;
        public IReadOnlyList<QuestDefinition> Prerequisites => prerequisites;
        public IReadOnlyList<QuestObjective> Objectives => objectives;
        public IReadOnlyList<ItemAmount> Rewards => rewards;
        
#if UNITY_EDITOR
        private void OnValidate()
        {
            foreach (var o in objectives) o?.FixInvalidValues();
            for (int i = 0; i < rewards.Count; i++)
            {
                // struct는 목록 안의 값을 직접 못 고친다 → 고친 값을 새로 만들어 통째로 넣는다
                if (rewards[i].Count < 1) rewards[i] = new ItemAmount(rewards[i].Item, 1);
                if (rewards[i].Item == null) Debug.LogWarning($"[Quest] {name}: 보상 {i}번 아이템이 비어 있음", this);
            }
            
            if (string.IsNullOrEmpty(id))
                Debug.LogWarning($"[Quest] {name}: ID가 비어 있음", this);
            else if (!IdRule.IsMatch(id))
                Debug.LogWarning($"[Quest] {name}: ID '{id}'는 영문 소문자·숫자·밑줄만 쓸 수 있음", this);
            if (objectives.Count == 0)
                Debug.LogWarning($"[Quest] {name}: 목표가 하나도 없음", this);
        }
#endif
    }
}