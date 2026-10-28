using UnityEngine;

namespace SesiVillage.Interaction
{
    /// <summary>
    /// E로 말을 걸 수 있는 NPC. 루트에 붙이고, Interactable 레이어의 트리거가 PlayerInteractor에게 잡힌다.
    /// npcId는 퀘스트 데이터(Giver Npc Id, Target Id)와 Yarn 대본의 노드 이름(title)과 같아야 한다.
    /// </summary>
    public class NpcInteractable : MonoBehaviour, IInteractable
    {
        [Tooltip("퀘스트·대본에서 이 NPC를 찾는 ID (영문 소문자·밑줄, 예: jeongeoeon)")]
        [SerializeField] private string npcId;
        [Tooltip("대화창에 보일 이름 (예: 정어언)")]
        [SerializeField] private string displayName;
        [Tooltip("안내 문구의 동사 → \"E 대화하기\"")]
        [SerializeField] private string prompt = "대화하기";

        public string NpcId => npcId;
        public string DisplayName => displayName;

        // ── IInteractable ──
        public string Prompt => prompt;
        public bool CanInteract => true;   // 나중에: 명절이 아니면 숨기기 등

        public void Interact(GameObject interactor)
        {
            if (DialogueDirector.Instance == null) { Debug.LogWarning("[NPC] DialogueDirector 없음", this); return; }
            DialogueDirector.Instance.StartTalk(npcId);
        }
    }
}
