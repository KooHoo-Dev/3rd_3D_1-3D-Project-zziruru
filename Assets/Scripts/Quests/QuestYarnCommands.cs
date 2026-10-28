using UnityEngine;
using Yarn.Unity;

namespace SesiVillage.Quests
{
    /// <summary>
    /// Yarn 대본에서 퀘스트를 다루는 명령을 등록한다.
    ///   <<accept 퀘스트ID>>            수락
    ///   <<deliver 퀘스트ID NPCID>>      전달 → 결과를 $deliver에 (Delivered / Missing / NothingToDeliver / WrongState)
    ///   <<hand_in 퀘스트ID>>           보고 1단계: 모으기 아이템 내기 → 보상 대기(Completed)
    ///   <<give_reward 퀘스트ID>>       보고 2단계: 보상 받기 → Rewarded
    /// 결과 문자열은 enum 이름 그대로이므로, enum 이름을 바꾸면 대본도 같이 고칠 것.
    /// </summary>
    [RequireComponent(typeof(DialogueRunner))]
    public class QuestYarnCommands : MonoBehaviour
    {
        private DialogueRunner runner;

        private void Awake()
        {
            runner = GetComponent<DialogueRunner>();
            runner.AddCommandHandler<string>("accept", Accept);
            runner.AddCommandHandler<string, string>("deliver", Deliver);
            runner.AddCommandHandler<string>("hand_in", HandIn);
            runner.AddCommandHandler<string>("give_reward", GiveReward);
        }

        private void Accept(string questId)
        {
            if (QuestManager.Instance == null) return;
            if (!QuestManager.Instance.Accept(questId))
                Debug.LogWarning($"[QuestYarn] '{questId}' 수락 실패 (조건·상태 확인)", this);
        }

        private void Deliver(string questId, string npcId)
        {
            var result = QuestManager.Instance != null ? QuestManager.Instance.Deliver(questId, npcId) : DeliverResult.WrongState;
            runner.VariableStorage.SetValue("$deliver", result.ToString());
        }

        // 보고 1단계: 모으기 아이템을 가져가고 보상 대기로 (대본에서 can_hand_in으로 먼저 확인)
        private void HandIn(string questId)
        {
            if (QuestManager.Instance == null) return;
            if (!QuestManager.Instance.HandIn(questId))
                Debug.LogWarning($"[QuestYarn] '{questId}' 내기 실패 — 대본에서 can_hand_in으로 먼저 확인할 것", this);
        }

        // 보고 2단계: 보상 (대본에서 can_receive_reward로 먼저 확인. 자리가 없으면 QuestManager가 '가방이 가득' 알림을 냄)
        private void GiveReward(string questId)
        {
            if (QuestManager.Instance == null) return;
            QuestManager.Instance.GiveRewards(questId);
        }
    }
}