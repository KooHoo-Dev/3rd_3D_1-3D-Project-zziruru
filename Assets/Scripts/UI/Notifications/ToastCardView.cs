using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SesiVillage.UI
{
    /// <summary>
    /// 알림 카드 1장 (수업 UI 갤러리 ToastCardView를 줄인 것). 두 단계 구조:
    ///   ToastCard (이 컴포넌트 + LayoutElement)   ← 레이아웃 담당: preferredHeight를 0 ↔ 카드 높이로 바꿔 접기·펼치기
    ///   └ Card (RectTransform + CanvasGroup)      ← 연출 담당: 옆에서 미끄러져 들어오기, 투명도
    /// Layout Group의 직계 자식은 위치를 직접 움직이면 다음 레이아웃 계산 때 덮어써지므로, 미끄러지는 건 안쪽 Card만 움직인다.
    /// 연출 시간은 Time.unscaledDeltaTime — 일시정지 중에도 들어오고 나가는 연출은 끝까지 재생된다.
    /// </summary>
    public class ToastCardView : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private LayoutElement slot;
        [SerializeField] private RectTransform card;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text message;
        [SerializeField] private TMP_Text countBadge;
        [SerializeField] private Image timerFill;           // Image Type = Filled, Horizontal

        [Header("크기 · 시간")]
        [SerializeField] private float slotHeight = 62f;    // 카드 높이 + 간격
        [SerializeField] private float enterSeconds = 0.22f;
        [SerializeField] private float exitSeconds = 0.18f;
        [SerializeField] private float slideDistance = 24f;

        public Toast Toast { get; private set; }
        private Coroutine running, bumping;

        public void Bind(Toast t)
        {
            Toast = t;
            message.text = t.req.message;
            icon.sprite = t.req.icon;
            icon.enabled = t.req.icon != null;   // 아이콘이 아직 없는 아이템이 많아서 없으면 숨김
            SetCount(t);
            SetTimer(1f);
        }

        public void SetCount(Toast t)
        {
            bool show = t.amount > 1;
            countBadge.gameObject.SetActive(show);
            if (show) countBadge.text = $"×{t.amount}";
        }

        /// <summary>남은 표시 시간 막대. fillAmount만 바꿔서 글자 메시를 다시 만들지 않는다</summary>
        public void SetTimer(float remaining01) => timerFill.fillAmount = remaining01;

        public void PlayEnter()
        {
            if (running != null) StopCoroutine(running);
            running = StartCoroutine(Enter());
        }

        public void PlayExit(Action done)
        {
            if (running != null) StopCoroutine(running);
            running = StartCoroutine(Exit(done));
        }

        /// <summary>같은 알림이 합쳐졌을 때: 개수를 바꾸고 숫자를 톡 키웠다 줄인다</summary>
        public void Bump()
        {
            SetCount(Toast);
            if (bumping != null) StopCoroutine(bumping);
            bumping = StartCoroutine(BumpRoutine());
        }

        private IEnumerator Enter()
        {
            var from = new Vector2(-slideDistance, 0f);   // 왼쪽 화면 가장자리 쪽에서 들어옴
            slot.preferredHeight = 0f;
            group.alpha = 0f;
            card.anchoredPosition = from;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / enterSeconds)
            {
                float k = 1f - Mathf.Pow(1f - t, 3f);     // ease-out-cubic: 빠르게 나왔다 천천히 멈춤
                slot.preferredHeight = Mathf.Lerp(0f, slotHeight, k);
                card.anchoredPosition = Vector2.LerpUnclamped(from, Vector2.zero, k);
                group.alpha = k;
                yield return null;
            }
            slot.preferredHeight = slotHeight;
            card.anchoredPosition = Vector2.zero;
            group.alpha = 1f;
            running = null;
        }

        private IEnumerator Exit(Action done)
        {
            float h0 = slot.preferredHeight;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / exitSeconds)
            {
                float k = t * t * t;                       // ease-in-cubic: 천천히 시작해 빠르게 사라짐
                slot.preferredHeight = Mathf.Lerp(h0, 0f, k);
                group.alpha = 1f - k;
                yield return null;
            }
            slot.preferredHeight = 0f;
            group.alpha = 0f;
            running = null;
            done?.Invoke();
        }

        private IEnumerator BumpRoutine()
        {
            var tr = countBadge.rectTransform;
            const float half = 0.08f;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / half) { tr.localScale = Vector3.one * Mathf.Lerp(1f, 1.2f, t); yield return null; }
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / half) { tr.localScale = Vector3.one * Mathf.Lerp(1.2f, 1f, t); yield return null; }
            tr.localScale = Vector3.one;
            bumping = null;
        }
    }
}
