using UnityEngine;

namespace SesiVillage.Player
{
    /// <summary>
    /// 칼을 넣었을 때 등에 칼이 보이게 한다.
    /// Explosive(RPGCharacterWeaponController)가 손 칼을 켜고 끄면,
    /// 등 칼은 항상 그 반대 상태로 맞춘다.
    /// </summary>
    public class BackSwordDisplay : MonoBehaviour
    {
        [SerializeField] private GameObject handSword;
        [SerializeField] private GameObject backSword;

        // Update와 코루틴이 끝난뒤, 화면 그리기 직전에 실행
        // 프레임의 최종 손 칼 상태를 보고 칼을 맞출 수 있음
        private void LateUpdate()
        {
            bool showBackSword = !handSword.activeSelf;
            if (backSword.activeSelf != showBackSword)
            {
                backSword.SetActive(showBackSword);
            }
        }
    }
}

