using RPGCharacterAnims;
using RPGCharacterAnims.Actions;
using RPGCharacterAnims.Lookups;
using UnityEngine;

namespace SesiVillage.Player
{
    /// <summary>
    /// 칼 꺼내기·넣기를 담당한다. 평소에는 등에 넣어 두고,
    /// 마지막 공격 후 일정 시간이 지나면 자동으로 넣는다.
    /// </summary>
    
    [RequireComponent(typeof(RPGCharacterController))]
    public class WeaponHolster : MonoBehaviour
    {
        [SerializeField] private Weapon equippedWeapon = Weapon.TwoHandSword;
        [Tooltip("마지막 공격 후 이 시간(초)이 지나면 칼을 넣는다.")]
        [SerializeField] private float sheathDelay = 5f;
        
        private RPGCharacterController character;
        private float lastCombatTime;
        
        /// <summary>지금 손에 무기를 들고 있는가</summary>
        public bool IsDrawn => character.rightWeapon != Weapon.Unarmed;
        /// <summary>꺼낼 무기를 가지고 있는가 (Equipped Weapon이 Unarmed면 맨손 전투)</summary>
        public bool HasWeapon => equippedWeapon != Weapon.Unarmed;
        
        private void Awake()
        {
            character = GetComponent<RPGCharacterController>();
        }
        
        private void Update()
        {
            // 칼을 든 채로 일정 시간 공격이 없으면 넣는다.
            if (IsDrawn && Time.time - lastCombatTime >= sheathDelay)
            {
                Sheath();
            }
        }
        
        /// <summary>공격했다고 알린다. 자동 넣기 타이머가 다시 시작된다.</summary>
        public void NotifyAttack()
        {
            lastCombatTime = Time.time;
        }
        
        /// <summary>칼을 꺼낸다.</summary>
        public void Draw()
        {
            if (IsDrawn) { return; }

            // 꺼내기 시작에 성공하면 그때부터 자동 넣기 타이머를 잰다.
            if (SwitchTo(equippedWeapon)) { lastCombatTime = Time.time; }
        }
        
        /// <summary>칼을 넣는다. NPC 대화를 시작할 때 대화 시스템에서 호출할 예정.</summary>
        public void Sheath()
        {
            if (!IsDrawn) { return; }
            SwitchTo(Weapon.Unarmed);
        }

        // Explosive의 무기 교체 기능을 호출한다. 시작에 성공하면 true.
        private bool SwitchTo(Weapon weapon)
        {
            // 공격·무기 교체 등 다른 동작 중이면 시작하지 않는다.
            if (!character.canAction) { return false; }

            var context = new SwitchWeaponContext
            {
                type = "Switch",
                side = weapon == Weapon.Unarmed ? "Both" : "None",
                leftWeapon = Weapon.Unarmed,
                rightWeapon = weapon,
            };

            // 이미 무기 교체 중이면 TryStartAction이 거절하고 false를 돌려준다.
            return character.TryStartAction(HandlerTypes.SwitchWeapon, context);
        }
    }
}
