using Yarn.Unity;

namespace SesiVillage.Quests
{
    /// <summary>Yarn 대본의 조건문에서 쓰는 퀘스트 조회 함수 (값만 읽음, 상태를 바꾸지 않음)</summary>
    public static class QuestYarnFunctions
    {
        /// <summary>quest_state("seollal_weed") → "NotAccepted" / "InProgress" / "Completed" / "Rewarded"</summary>
        [YarnFunction("quest_state")]
        public static string GetQuestState(string questId)
        {
            return QuestManager.Instance != null ? QuestManager.Instance.GetState(questId).ToString() : "NotAccepted";
        }

        /// <summary>can_accept("seollal_shrine") → 선행 퀘스트 등 수락 조건을 만족하는가</summary>
        [YarnFunction("can_accept")]
        public static bool CanAccept(string questId)
        {
            return QuestManager.Instance != null && QuestManager.Instance.CanAccept(questId);
        }

        /// <summary>can_hand_in("seollal_weed") → 잡초 같은 목표를 다 채워서 낼 수 있나 (진행 중일 때만)</summary>
        [YarnFunction("can_hand_in")]
        public static bool CanHandIn(string questId)
        {
            return QuestManager.Instance != null && QuestManager.Instance.CanHandIn(questId);
        }

        /// <summary>can_receive_reward("seollal_weed") → 보상 대기 중이고 가방에 보상 넣을 자리가 있나</summary>
        [YarnFunction("can_receive_reward")]
        public static bool CanReceiveReward(string questId)
        {
            return QuestManager.Instance != null && QuestManager.Instance.CanReceiveReward(questId);
        }

        /// <summary>deliver_check("seollal_shrine", "chilseongdang") → 지금 건네면? "Delivered" / "Missing" / "NothingToDeliver" / "WrongState"</summary>
        [YarnFunction("deliver_check")]
        public static string CheckDeliver(string questId, string npcId)
        {
            return QuestManager.Instance != null ? QuestManager.Instance.CheckDeliver(questId, npcId).ToString() : "WrongState";
        }
    }
}