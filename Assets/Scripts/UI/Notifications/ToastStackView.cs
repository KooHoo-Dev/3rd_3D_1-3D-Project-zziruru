using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace SesiVillage.UI
{
    /// <summary>
    /// 획득 알림 묶음 — 화면 왼쪽 아래에 "떡국을 얻었습니다"가 아래에서 위로 쌓였다 사라진다 (기획 6장 UI "알림").
    /// 수업 UI 갤러리 ToastStackView를 줄인 것. NotificationQueue(순수 로직)를 만들고 OnShow·OnUpdate·OnHide만 받아 그린다.
    /// 알림을 띄우려면 ToastStackView.Post(...) — 띄우는 쪽은 화면이 어떻게 생겼는지 모른다.
    /// 카드는 미리 만들어 두고 다시 쓴다 (오브젝트 풀): 알림마다 Instantiate·Destroy하면 GC가 쌓인다.
    /// </summary>
    public class ToastStackView : MonoBehaviour
    {
        public static ToastStackView Instance { get; private set; }

        [Header("큐 규칙")]
        [SerializeField] private QueueConfig config = new QueueConfig();
        [SerializeField, Tooltip("켜면 대화창·인벤토리 등 UI가 열려 있는 동안 알림 시간이 줄지 않는다. 기본은 끔 (2026-10-28 사용자 결정: 그냥 알림이니 UI를 보고 있어도 사라져도 됨)")]
        private bool freezeWhileUIOpen = false;

        [Header("뷰")]
        [SerializeField, Tooltip("Vertical Layout Group이 붙은 곳. 카드가 여기 아래에 쌓인다")]
        private RectTransform stack;
        [SerializeField, Tooltip("복제할 카드 원본 (꺼 둔 상태로 stack 아래)")]
        private ToastCardView cardTemplate;
        [SerializeField, Tooltip("\"+N\" — 자리가 없어 기다리는 알림 수")]
        private TMP_Text overflowLabel;
        [SerializeField, Tooltip("미리 만들어 둘 카드 수 (최대 장수 + 나가는 중인 카드 여유분)")]
        private int poolSize = 6;

        public NotificationQueue Notifications { get; private set; }

        private readonly Stack<ToastCardView> pool = new Stack<ToastCardView>();
        private readonly Dictionary<int, ToastCardView> views = new Dictionary<int, ToastCardView>();

        /// <summary>알림 하나 띄우기. key가 같은 알림이 잠깐 사이에 또 오면 한 장으로 합쳐 ×N</summary>
        public static void Post(string key, string message, int amount = 1, Sprite icon = null)
        {
            if (Instance == null) { Debug.Log($"[알림] {message} ×{amount}"); return; }   // 알림 UI가 없는 씬에서도 로그는 남김
            Instance.Notifications.Enqueue(new ToastRequest(key, message, amount, icon));
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("[ToastStackView] 이미 있음 — 하나만 두세요", this);
                Destroy(this);
                return;
            }
            Instance = this;

            cardTemplate.gameObject.SetActive(false);
            for (int i = 0; i < poolSize; i++) pool.Push(CreateCard());

            Notifications = new NotificationQueue(config);
            Notifications.OnShow += HandleShow;
            Notifications.OnUpdate += HandleUpdate;
            Notifications.OnHide += HandleHide;
            Notifications.OnChanged += RefreshOverflow;
            RefreshOverflow();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (Notifications == null) return;
            Notifications.OnShow -= HandleShow;
            Notifications.OnUpdate -= HandleUpdate;
            Notifications.OnHide -= HandleHide;
            Notifications.OnChanged -= RefreshOverflow;
        }

        private void Update()
        {
            bool frozen = freezeWhileUIOpen && UIState.Instance != null && UIState.Instance.IsOpen;
            // 일시정지(Time.timeScale = 0)일 때 deltaTime은 0 → 알림도 멈춘다
            Notifications.Tick(Time.deltaTime, frozen);
            foreach (var t in Notifications.active)
                if (views.TryGetValue(t.uid, out var v)) v.SetTimer(t.Remaining01);
        }

        private ToastCardView CreateCard()
        {
            var v = Instantiate(cardTemplate, stack);
            v.gameObject.SetActive(false);
            return v;
        }

        private void HandleShow(Toast t)
        {
            var v = pool.Count > 0 ? pool.Pop() : CreateCard();
            v.gameObject.SetActive(true);
            v.transform.SetAsLastSibling();   // 아래에서 위로 쌓기: 새 카드가 맨 아래(마지막 형제)
            v.Bind(t);
            v.PlayEnter();
            views[t.uid] = v;
        }

        private void HandleUpdate(Toast t)
        {
            if (views.TryGetValue(t.uid, out var v)) v.Bump();
        }

        private void HandleHide(Toast t)
        {
            if (!views.TryGetValue(t.uid, out var v)) return;
            views.Remove(t.uid);
            v.PlayExit(() =>
            {
                v.gameObject.SetActive(false);
                pool.Push(v);   // 버리지 않고 다음 알림에 다시 씀
            });
        }

        private void RefreshOverflow()
        {
            if (overflowLabel == null) return;
            int n = Notifications.pending.Count;
            overflowLabel.gameObject.SetActive(n > 0);
            if (n > 0) overflowLabel.text = $"+{n}";
        }
    }
}
