using SesiVillage.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SesiVillage.Core
{
    /// <summary>
    /// 평소에는 커서를 숨기고 화면 가운데에 고정한다 (마우스 = 카메라 회전).
    /// UIState가 열림이면 커서를 보이고 창 안에 가둔다(Confined).
    /// </summary>
    public class CursorController : MonoBehaviour
    {
        private bool debugOpen;   // 임시 Esc로 연 상태인가
        
        private void Start()
        {
            ShowCursor(false);
            
            if (UIState.Instance == null) { Debug.LogError("[CursorController] UIState 없음", this); return; }
            UIState.Instance.Changed += ShowCursor;
            ShowCursor(UIState.Instance.IsOpen);   // 구독 전에 이미 열려 있었을 수도 있으니 지금 상태를 한 번 적용
        }
        
        private void OnDestroy()
        {
            if (UIState.Instance != null) UIState.Instance.Changed -= ShowCursor;
        }
        
        private void Update()
        {
            // 임시: Esc로 UI 열림 상태를 흉내 (에디터에서 마우스를 쓰기 위해). 메뉴 UI가 생기면 메뉴로 옮긴다.
            // 커서만 바꾸지 않고 UIState를 거쳐서 → 주인공·카메라도 같이 멈춘다
            if (Keyboard.current.escapeKey.wasPressedThisFrame && UIState.Instance != null)
            {
                debugOpen = !debugOpen;
                if (debugOpen) UIState.Instance.Open(this);
                else UIState.Instance.Close(this);
            }
        }

        public void ShowCursor(bool show)
        {
            // UI가 열리면 커서를 보이게 하고 창 밖으로 못 나가게(Confined) 한다
            Cursor.lockState = show ? CursorLockMode.Confined : CursorLockMode.Locked;
            Cursor.visible = show;
        }
    }
}
