using UnityEngine;
using UnityEngine.InputSystem;

namespace SesiVillage.Core
{
    /// <summary>
    /// 평소에는 커서를 숨기고 화면 가운데에 고정한다 (마우스 = 카메라 회전).
    /// UI(대화창·인벤토리·메뉴)가 열리면 ShowCursor(true)로 커서를 보이게 할 예정.
    /// </summary>
    public class CursorController : MonoBehaviour
    {
        private void Start()
        {
            ShowCursor(false);
        }
        
        private void Update()
        {
            // 임시: Esc로 커서 보이기/숨기기 (에디터에서 마우스를 쓰기 위해). 메뉴 UI가 생기면 그쪽으로 옮긴다.
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
                ShowCursor(Cursor.lockState == CursorLockMode.Locked);
        }

        public void ShowCursor(bool show)
        {
            // UI가 열리면 커서를 보이게 하고 창 밖으로 못 나가게(Confined) 한다
            Cursor.lockState = show ? CursorLockMode.Confined : CursorLockMode.Locked;
            Cursor.visible = show;
        }
    }
}
