using RPGCharacterAnims;
using UnityEngine;

namespace SesiVillage.Player
{
    /// <summary>
    /// Shift를 누르는 동안 달린다. 이동 속도(runSpeed)와 이동 애니메이션 재생 속도(LocomotionSpeed)를
    /// 같은 배율로 올려서 발이 미끄러지지 않게 한다.
    /// </summary>
    [RequireComponent(typeof(PlayerInputController))]
    [RequireComponent(typeof(RPGCharacterMovementController))]
    public class PlayerSprint : MonoBehaviour
    {
        private static readonly int LocomotionSpeed = Animator.StringToHash("LocomotionSpeed");

        [Tooltip("달릴 때 평소 달리기의 몇 배로 빨라지는가")]
        [SerializeField] private float sprintMultiplier = 1.6f;
        [Tooltip("배율이 1초에 얼마나 빨리 바뀌는가 (클수록 즉시 바뀜)")]
        [SerializeField] private float blendSpeed = 4f;

        private PlayerInputController input;
        private RPGCharacterMovementController movement;
        private RPGCharacterController character;
        private float baseRunSpeed;
        private float currentMultiplier = 1f;
       
        private void Awake()
        {
            input = GetComponent<PlayerInputController>();
            movement = GetComponent<RPGCharacterMovementController>();
            character = GetComponent<RPGCharacterController>();
            baseRunSpeed = movement.runSpeed;
        }

        private void Update()
        {
            bool sprinting = input.HasSprintInput() && input.HasMoveInput();
            float target = sprinting ? sprintMultiplier : 1f;
            currentMultiplier = Mathf.MoveTowards(currentMultiplier, target, blendSpeed * Time.deltaTime);

            movement.runSpeed = baseRunSpeed * currentMultiplier;
            character.animator.SetFloat(LocomotionSpeed, currentMultiplier);
        }
    }
}
