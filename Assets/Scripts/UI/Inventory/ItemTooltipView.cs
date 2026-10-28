using System.Collections;
using SesiVillage.Items;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SesiVillage.UI
{
    /// <summary>
    /// 아이템 설명 툴팁 (수업 UI 갤러리 TooltipView에서 장비 비교를 빼고 배치 로직은 그대로). 순서가 핵심:
    ///   ① 내용 채우기 → ② ForceRebuildLayoutImmediate로 크기 확정 → ③ 칸 오른쪽에 두기
    ///   → ④ 화면을 넘치면 왼쪽으로 뒤집기 → ⑤ 그래도 넘치면 화면 안으로 Clamp → ⑥ 투명도·크기만 페이드 (위치는 움직이지 않음)
    /// 이 오브젝트의 CanvasGroup.blocksRaycasts는 끈다. 켜져 있으면 커서가 툴팁에 걸려 칸에서 나간 것으로 판정 → 깜빡임.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class ItemTooltipView : MonoBehaviour
    {
        [SerializeField] private Canvas canvas;
        [SerializeField, Tooltip("툴팁이 움직일 수 있는 영역 (화면 전체로 늘린 RectTransform)")]
        private RectTransform layer;

        [Header("내용")]
        [SerializeField] private TMP_Text itemName;
        [SerializeField] private TMP_Text kind;
        [SerializeField] private GameObject equippedBadge;
        [SerializeField] private TMP_Text description;

        [Header("배치")]
        [SerializeField] private float gap = 12f;      // 칸과 툴팁 사이
        [SerializeField] private float margin = 24f;   // 화면 가장자리와 툴팁 사이
        [SerializeField] private float fadeTime = 0.12f;

        private RectTransform rect;
        private CanvasGroup group;
        private Coroutine fade;
        private readonly Vector3[] corners = new Vector3[4];

        public bool IsOpen => group.alpha > 0f;

        private void Awake()
        {
            rect = (RectTransform)transform;
            group = GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;     // 입력을 받지 않는다 (깜빡임 방지)
            group.interactable = false;
            group.alpha = 0f;
            // 피벗 왼쪽 아래, 앵커 레이어 가운데 → anchoredPosition = 레이어 안의 좌표 (계산이 단순해짐)
            rect.pivot = Vector2.zero;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        }

        // anchor : 마우스 커서를 올린 인벤토리 칸. 툴팁을 붙일 기준점 (닻)
        public void Show(InventorySlot slot, bool equipped, RectTransform anchor)
        {
            var item = slot.Item;

            // ① 내용
            itemName.text = slot.Count > 1 ? $"{item.DisplayName} <size=75%>×{slot.Count}</size>" : item.DisplayName;
            kind.text = KindName(item.Type) + (item.IsQuestItem ? " · 퀘스트 물건" : "");
            equippedBadge.SetActive(equipped);
            description.text = string.IsNullOrEmpty(item.Description) ? "(설명 없음)" : item.Description;

            // ② 크기 확정 — ContentSizeFitter는 다음 레이아웃 계산 때에야 크기를 정하므로 지금 강제로 계산
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);

            // ③~⑤ 위치
            Place(anchor);

            // ⑥ 나타나기 (이미 떠 있으면 내용·위치만 바뀜 → 옆 칸으로 옮길 때 깜빡이지 않음)
            if (!IsOpen) StartFade(1f);
        }

        public void Hide()
        {
            if (IsOpen) StartFade(0f);
        }
        
        // MenuCanvas
        // ├ InventoryScreen → Window → Grid → [칸]       ← 칸의 부모 = Grid
        // └ TooltipLayer → [툴팁]                         ← 툴팁의 부모 = TooltipLayer
        // Grid 는 화면 가운데 근처, TooltipLayer 는 화면 전체. 칸의 위치를 툴팁의 기준 주소로 바꿔 말해야함
        // 그걸 RectInLayer 가 해줌
        private void Place(RectTransform anchor)
        {
            Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Rect a = RectInLayer(anchor, cam);  // 커서를 올린 칸의 사각형 (레이어 좌표로 변환한 것)
            Vector2 size = rect.rect.size;
            Rect b = layer.rect;
            float xMin = b.xMin + margin, xMax = b.xMax - margin, yMin = b.yMin + margin, yMax = b.yMax - margin;

            // 칸 오른쪽, 윗변 맞춤 (UGUI는 y가 위로 커짐 → 툴팁 아래 변 = 칸 윗변 - 툴팁 높이)
            float x = a.xMax + gap;
            float y = a.yMax - size.y;
            if (x + size.x > xMax)
            {
                float flipped = a.xMin - gap - size.x;   // 오른쪽이 넘치면 왼쪽으로 뒤집기
                if (flipped >= xMin) x = flipped;
            }
            x = Mathf.Clamp(x, xMin, xMax - size.x);     // 그래도 넘치면 화면 안으로
            y = Mathf.Clamp(y, yMin, yMax - size.y);
            rect.anchoredPosition = new Vector2(x, y); // 부모 기준 몇 픽셀
        }

        /// <summary>칸의 네 모서리 → 화면 좌표 → 툴팁 레이어 안의 좌표 (칸과 툴팁의 부모가 달라도 같은 기준으로 비교하려고)</summary>
        private Rect RectInLayer(RectTransform target, Camera cam)
        {
            target.GetWorldCorners(corners);   // 0 = 왼쪽 아래, 2 = 오른쪽 위
            RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, RectTransformUtility.WorldToScreenPoint(cam, corners[0]), cam, out Vector2 min);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, RectTransformUtility.WorldToScreenPoint(cam, corners[2]), cam, out Vector2 max);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private void StartFade(float to)
        {
            if (fade != null) StopCoroutine(fade);
            fade = StartCoroutine(Fade(to));
        }

        private IEnumerator Fade(float to)
        {
            float from = group.alpha;
            float dur = to > from ? fadeTime : fadeTime * 0.66f;   // 사라질 때가 더 짧다
            for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
            {
                float k = t / dur;
                group.alpha = Mathf.Lerp(from, to, k);
                rect.localScale = Vector3.one * Mathf.Lerp(0.96f, 1f, to > from ? k : 1f - k);
                yield return null;
            }
            group.alpha = to;
            rect.localScale = Vector3.one;
            fade = null;
        }

        private static string KindName(ItemType type) => type switch
        {
            ItemType.Food => "먹을 것",
            ItemType.Outfit => "옷",
            ItemType.Material => "재료",
            ItemType.Weapon => "무기",
            _ => "",
        };
    }
}
