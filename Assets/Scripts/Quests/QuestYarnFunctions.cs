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
    }
}