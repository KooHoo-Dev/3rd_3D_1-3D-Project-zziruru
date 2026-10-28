using UnityEngine;
using UnityEngine.EventSystems;

namespace SesiVillage.UI
{
    /// <summary>
    /// 인벤토리 미리보기 화면(RawImage)을 마우스로 좌우로 끌면 미리보기 캐릭터가 돈다 (젤다식).
    /// 이 컴포넌트는 RawImage에 붙인다 — UI 이벤트(IDragHandler)는 Raycast Target이 켜진 UI 요소에만 온다.
    /// </summary>
    public class CharacterPreviewDrag : MonoBehaviour, IDragHandler
    {
        [Tooltip("돌릴 대상 (미리보기 무대의 캐릭터)")]
        [SerializeField] private Transform target;
        [Tooltip("마우스 1픽셀당 몇 도")]
        [SerializeField] private float degreesPerPixel = 0.4f;

        public void OnDrag(PointerEventData eventData)
        {
            if (target == null) return;
            // 오른쪽으로 끌면 캐릭터가 오른쪽으로 돌아 보이게 (y축 기준, 음수)
            target.Rotate(0f, -eventData.delta.x * degreesPerPixel, 0f, Space.World);
        }
    }
}
