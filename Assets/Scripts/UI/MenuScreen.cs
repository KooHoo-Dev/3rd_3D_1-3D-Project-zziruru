using UnityEngine;
using UnityEngine.InputSystem;

namespace SesiVillage.UI
{
    /// <summary>
    /// 키로 열고 닫는 전체 화면 UI의 공통 부분 (인벤토리 I, 퀘스트 목록 Q). 상속해서 OnOpened·OnClosed만 채운다.
    ///  · 같은 키를 다시 누르거나 Esc(UI/Cancel)를 누르면 닫힘
    ///  · 다른 UI(대화창 등)가 열려 있으면 열리지 않음 → 대화 중에 인벤토리가 겹쳐 뜨지 않게
    ///  · 열고 닫을 때 UIState에 알림 → 커서 보이기, 주인공 이동·카메라 잠금은 UIState를 듣는 쪽이 알아서 한다
    ///  · 숨길 때 SetActive(false)가 아니라 CanvasGroup으로 숨긴다 → 스크립트가 계속 살아 있어 키 입력을 받을 수 있다 (TechNotes 7-8)
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class MenuScreen : MonoBehaviour
    {
        [SerializeField, Tooltip("열고 닫는 키 (Player/Inventory 등)")]
        private InputActionReference toggleAction;
        [SerializeField, Tooltip("닫기 키 (UI/Cancel = Esc)")]
        private InputActionReference cancelAction;

        private CanvasGroup group;
        public bool IsOpen { get; private set; }

        protected virtual void Awake()
        {
            group = GetComponent<CanvasGroup>();
            SetVisible(false);
        }

        protected virtual void OnEnable()
        {
            if (toggleAction != null) toggleAction.action.Enable();
            if (cancelAction != null) cancelAction.action.Enable();
        }

        protected virtual void Update()
        {
            bool toggle = toggleAction != null && toggleAction.action.WasPressedThisFrame();
            bool cancel = cancelAction != null && cancelAction.action.WasPressedThisFrame();

            if (IsOpen)
            {
                if (toggle || cancel) Close();
            }
            else if (toggle)
            {
                // 다른 UI가 열려 있거나, 다른 UI를 방금 닫은 프레임이면 열지 않음 (같은 키가 두 화면에 쓰이는 일 방지)
                var ui = UIState.Instance;
                bool busy = ui != null && (ui.IsOpen || ui.ClosedFrame == Time.frameCount);
                if (!busy) Open();
            }
        }

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            SetVisible(true);
            if (UIState.Instance != null) UIState.Instance.Open(this);
            OnOpened();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            SetVisible(false);
            OnClosed();
            if (UIState.Instance != null) UIState.Instance.Close(this);
        }

        private void OnDisable()
        {
            // 씬이 바뀌거나 꺼질 때 잠금이 남지 않게
            if (IsOpen) Close();
        }

        private void SetVisible(bool visible)
        {
            group.alpha = visible ? 1f : 0f;
            group.blocksRaycasts = visible;   // 숨긴 화면이 마우스 클릭을 가로채지 않게
            group.interactable = visible;
        }

        protected abstract void OnOpened();
        protected virtual void OnClosed() { }
    }
}
