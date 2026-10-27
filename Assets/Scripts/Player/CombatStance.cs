using RPGCharacterAnims;
using UnityEngine;

namespace SesiVillage.Player
{
    /// <summary>
    /// 맨손 대기 자세를 정한다: 평소엔 편한 자세, 맨손으로 싸우는 중엔 맨손 전투 자세.
    /// 마지막 맨손 공격 후 relaxDelay초가 지나면 편한 자세로 천천히 돌아간다.
    /// 무기가 있을 때는 알림을 받지 않으므로 칼을 넣는 순간 바로 편한 자세가 된다.
    /// Animator의 "Combat" 값(0 = 편한 자세, 1 = 전투 자세)만 바꾸고, 섞는 건 블렌드 트리가 한다.
    /// </summary>
    [RequireComponent(typeof(RPGCharacterController))]
    public class CombatStance : MonoBehaviour
    {
        private static readonly int CombatParam = Animator.StringToHash("Combat");

        [Tooltip("맨손 공격 후 이 시간(초)이 지나면 편한 자세로")]
        [SerializeField] private float relaxDelay = 5f;
        [Tooltip("두 자세가 바뀌는 데 걸리는 시간 (초)")]
        [SerializeField] private float blendTime = 0.4f;

        private Animator animator;
        private float lastCombatTime = float.NegativeInfinity;   // 시작은 편한 자세

        public bool IsInCombat => Time.time - lastCombatTime < relaxDelay;

        // Start: RPGCharacterController가 Awake에서 animator를 준비한 뒤에 가져온다
        private void Start()
        {
            animator = GetComponent<RPGCharacterController>().animator;
        }

        /// <summary>맨손으로 공격했다고 알린다 → 전투 자세 타이머 다시 시작</summary>
        public void NotifyAttack() => lastCombatTime = Time.time;

        /// <summary>바로 편한 자세로 (NPC 대화 시작할 때 대화 시스템이 부를 예정)</summary>
        public void Relax() => lastCombatTime = float.NegativeInfinity;

        private void Update()
        {
            // 목표값으로 blendTime 동안 부드럽게 따라간다 (SetFloat의 damp 기능)
            animator.SetFloat(CombatParam, IsInCombat ? 1f : 0f, blendTime, Time.deltaTime);
        }
    }
}