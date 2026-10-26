namespace SesiVillage.Quests
{
    /// <summary>퀘스트 상태. 이 순서대로 한 칸씩만 앞으로 간다.</summary>
    public enum QuestState
    {
        NotAccepted,   // 아직 안 받음
        InProgress,    // 진행 중
        Completed,     // 목표 다 채움 (보상 받기 전)
        Rewarded,      // 보상 받음 (끝)
    }

    /// <summary>
    /// 퀘스트 하나의 진행 상태 (플레이 중에 바뀌고, 나중에 저장하는 대상).
    /// QuestManager만 만들고 고친다. 다른 코드는 QuestManager의 Get 함수로 읽기만 한다.
    /// </summary>
    public class QuestProgress
    {
        public QuestState state = QuestState.NotAccepted;
        public int[] counts;   // 목표별 진행도 (QuestData.Objectives와 같은 순서)

        public QuestProgress(int objectiveCount)
        {
            counts = new int[objectiveCount];
        }
    }
}