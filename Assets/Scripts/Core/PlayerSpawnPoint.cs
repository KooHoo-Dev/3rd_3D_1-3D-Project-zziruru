using UnityEngine;

namespace SesiVillage.Core
{
    /// <summary>
    /// 플레이어가 게임을 시작하는 위치. Village 씬에 하나만 둔다.
    /// GameBootstrap이 이 컴포넌트를 찾아서 플레이어를 이 위치·방향으로 옮긴다.
    /// </summary>
    public class PlayerSpawnPoint : MonoBehaviour
    {
        // 기즈모를 캐릭터 크기에 맞춰 그리기 위한 값 (Player의 CapsuleCollider와 같은 값)
        [SerializeField] private float characterHeight = 1.85f;
        [SerializeField] private float characterRadius = 0.4f;
        
        // Scene 창에 위치와 방향을 그려 준다. (빈 오브젝트는 원래 Scene 창에서 보이지 않음)
        // 게임 화면에는 그려지지 않고, 에디터에서만 보인다.
        private void OnDrawGizmos()
        {
            Vector3 center = transform.position + Vector3.up * (characterHeight / 2f);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(center, characterRadius);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * characterHeight);
            Gizmos.DrawRay(center, transform.forward * 1.5f);
        }
    }
}
