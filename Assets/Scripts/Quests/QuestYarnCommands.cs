using UnityEngine;
using Yarn.Unity;

namespace SesiVillage.Quests
{
    /// <summary>
    /// Yarn 대본에서 퀘스트를 다루는 명령을 등록한다.
    ///   <<accept 퀘스트ID>>            수락
    ///   <<deliver 퀘스트ID NPCID>>      전달 → 결과를 $deliver에 (Delivered / Missing / NothingToDeliver / WrongState)
    ///   <<turn_in 퀘스트ID>>           보고·보상 → 결과를 $turn_in에 (Rewarded / Missing / InventoryFull / WrongState)
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
            runner.AddCommandHandler<string>("turn_in", TurnIn);
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

        private void TurnIn(string questId)
        {
            var result = QuestManager.Instance != null ? QuestManager.Instance.TryTurnIn(questId) : TurnInResult.WrongState;
            runner.VariableStorage.SetValue("$turn_in", result.ToString());
        }
    }
}