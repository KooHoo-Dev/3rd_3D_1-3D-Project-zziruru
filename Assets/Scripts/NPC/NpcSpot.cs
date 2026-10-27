using UnityEngine;

namespace SesiVillage.NPC
{
    /// <summary>
    /// NPC가 설 자리. VillageBuilder가 Village 씬에 만든다.
    /// NPC는 Village_GamePlay 씬에 있어서 직접 연결할 수 없으므로, 같은 spotId를 가진 자리를 실행할 때 찾아간다.
    /// </summary>
    public class NpcSpot : MonoBehaviour
    {
        [Tooltip("자리 이름 (예: seollal, shrine, chief). NpcPlacement의 Spot Id와 같아야 함")]
        [SerializeField] private string spotId;

        public string SpotId => spotId;

        // 빌더가 만들 때 ID를 넣는다
        public void Init(string id) => spotId = id;

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.9f, 0.4f);
            Gizmos.DrawRay(transform.position + Vector3.up * 0.9f, transform.forward * 1.5f);   // 바라보는 방향
        }
    }
}
