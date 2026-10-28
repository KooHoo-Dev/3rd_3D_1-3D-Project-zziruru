using UnityEngine;
using Yarn.Unity;

namespace SesiVillage.Interaction
{
    /// <summary>
    /// 대화를 시작하는 창구 (Core 씬, 하나만). 다른 씬의 NPC는 Inspector로 DialogueRunner를 연결할 수 없으므로
    /// DialogueDirector.Instance.StartTalk(노드 이름)으로 대화를 연다.
    /// </summary>
    public class DialogueDirector : MonoBehaviour
    {
        public static DialogueDirector Instance { get; private set; }

        [SerializeField] private DialogueRunner runner;

        public bool IsTalking => runner != null && runner.IsDialogueRunning;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Debug.LogError("[DialogueDirector] 이미 있음", this); Destroy(this); return; }
            Instance = this;
            if (runner == null) Debug.LogError("[DialogueDirector] DialogueRunner가 연결되지 않음", this);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Yarn 노드 하나를 실행한다 (노드 이름 = NPC ID로 맞춤, 예: jeongeoeon)</summary>
        public void StartTalk(string nodeName)
        {
            if (runner == null || IsTalking) return;   // 이미 대화 중이면 무시 (중복 시작 방지)
            if (!runner.Dialogue.NodeExists(nodeName))
            {
                Debug.LogWarning($"[DialogueDirector] 대본에 '{nodeName}' 노드 없음", this); 
                return;
            }
            _ = runner.StartDialogue(nodeName);         // 끝날 때까지 기다리지 않음 (_ = 결과를 일부러 버림)
        }
    }
}