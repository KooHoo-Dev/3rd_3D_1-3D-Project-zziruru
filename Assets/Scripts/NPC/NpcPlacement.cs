using UnityEngine;

namespace SesiVillage.NPC
{
    /// <summary>
    /// 시작할 때 같은 spotId를 가진 NpcSpot으로 옮겨 간다 (위치 + 방향).
    /// 맵을 다시 만들어 자리가 바뀌어도 NPC가 따라간다.
    /// </summary>
    public class NpcPlacement : MonoBehaviour
    {
        [SerializeField] private string spotId;

        public string SpotId => spotId;

        private void Start() => SnapToSpot();

        /// <summary>같은 ID의 자리로 옮긴다. 찾으면 true (에디터 도구도 쓴다)</summary>
        public bool SnapToSpot()
        {
            foreach (var spot in FindObjectsByType<NpcSpot>(FindObjectsSortMode.None))
            {
                if (spot.SpotId != spotId) continue;
                transform.SetPositionAndRotation(spot.transform.position, spot.transform.rotation);
                return true;
            }
            Debug.LogWarning($"[NpcPlacement] '{spotId}' 자리를 찾지 못함 — Village 씬이 열려 있는지, 빌더에 ID가 있는지 확인", this);
            return false;
        }

        // 에디터에서도 쓸 수 있게: 컴포넌트 ⋮ 메뉴 → "자리로 옮기기" (Village 씬도 열려 있어야 함)
        [ContextMenu("자리로 옮기기")]
        private void SnapToSpotMenu() => SnapToSpot();
    }
}
