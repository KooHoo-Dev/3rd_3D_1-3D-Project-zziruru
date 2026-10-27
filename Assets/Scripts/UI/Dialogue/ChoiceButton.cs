using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SesiVillage.UI
{
    /// <summary>
    /// 선택지 버튼 한 개의 포커스 연출 — 강조색 판 + 옆으로 밀기 + ▶ 표시. 수업 UI 갤러리의 ChoiceButton을 줄인 것.
    /// Button의 Transition은 None으로 두고 이 스크립트가 색·위치를 직접 바꾼다 (마우스·키보드 경험을 같게).
    /// 확정은 Button.onClick → DialogueView.OnChoice(index) 한 곳으로 모은다.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class ChoiceButton : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler
    {
        [SerializeField] private Image frame;                 // 배경 판
        [SerializeField] private TMP_Text label;
        [SerializeField] private RectTransform content;       // 포커스 때 옆으로 밀리는 묶음 (글자 + ▶)
        [SerializeField] private GameObject marker;           // ▶
        [SerializeField] private Color idleFrame = new Color(0.12f, 0.09f, 0.06f, 0.85f);
        [SerializeField] private Color idleText = new Color(0.92f, 0.88f, 0.80f);
        [SerializeField] private Color accentFrame = new Color(0.85f, 0.62f, 0.30f);
        [SerializeField] private Color accentText = new Color(0.12f, 0.08f, 0.04f);
        [SerializeField, Tooltip("포커스 강조 시간 (초) — 150ms를 넘기지 않는다")] private float focusTime = 0.1f;
        [SerializeField, Tooltip("포커스 때 밀리는 거리 (px)")] private float pushDistance = 20f;

        // DialogueView.Awake가 먼저 불릴 수 있으므로(실행 순서 미정) 처음 쓸 때 가져온다
        private Button button;
        private CanvasGroup group;
        public Button Button
        {
            get
            {
                if (button == null) button = GetComponent<Button>();
                return button;
            }
        }
        public CanvasGroup Group
        {
            get
            {
                if (group == null) group = GetComponent<CanvasGroup>();
                if (group == null) group = gameObject.AddComponent<CanvasGroup>();
                return group;
            }
        }
        public int Index { get; private set; }

        /// <summary>이 버튼이 포커스를 받았을 때 (번호)</summary>
        public event Action<int> Focused;

        private bool focused;
        private float t;

        private void Awake() => Button.transition = Selectable.Transition.None;

        public void Setup(int index, string text)
        {
            Index = index;
            label.text = text;
            focused = false;
            t = 0f;
            Apply();
        }

        public void OnSelect(BaseEventData e)
        {
            focused = true;
            Focused?.Invoke(Index);
        }

        public void OnDeselect(BaseEventData e) => focused = false;

        // 마우스를 올리면 포커스가 따라온다 → 마우스·키보드가 같은 '하나의 포커스'를 공유
        public void OnPointerEnter(PointerEventData e)
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(gameObject);
        }

        private void Update()
        {
            // 일시정지 메뉴 위에서도 움직이도록 unscaled
            t = Mathf.MoveTowards(t, focused ? 1f : 0f, Time.unscaledDeltaTime / Mathf.Max(0.01f, focusTime));
            Apply();
        }

        private void Apply()
        {
            frame.color = Color.Lerp(idleFrame, accentFrame, t);
            label.color = Color.Lerp(idleText, accentText, t);
            label.fontStyle = focused ? FontStyles.Bold : FontStyles.Normal;
            content.anchoredPosition = new Vector2(pushDistance * t, 0f);
            marker.SetActive(focused);
        }
    }
}
