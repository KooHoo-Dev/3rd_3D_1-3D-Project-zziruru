using SesiVillage.Items;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SesiVillage.UI
{
    /// <summary>
    /// 인벤토리 칸 하나 (수업 UI 갤러리 SlotView를 우리 인벤토리에 맞게 줄인 것).
    /// Selectable을 상속해 '마우스 올리기'와 '키보드·패드 선택'을 같은 InventoryScreen.FocusSlot()으로 보낸다
    /// → 입력 장치가 달라도 툴팁 상태는 하나. 칸은 화면을 직접 바꾸지 않고 Owner에게 알리기만 한다.
    /// </summary>
    public class SlotView : Selectable, ISubmitHandler, IPointerClickHandler
    {
        [SerializeField] private Image icon;
        [SerializeField, Tooltip("아이콘이 없는 아이템은 이름 첫 글자를 대신 보여 준다")]
        private TMP_Text initial;
        [SerializeField] private TMP_Text count;
        [SerializeField, Tooltip("\"착용 중\" 표시")]
        private GameObject equippedMark;
        [SerializeField, Tooltip("툴팁이 뜨기까지 남은 시간 막대 (Filled, Horizontal)")]
        private Image delayBar;

        public InventorySlot Slot { get; private set; }
        public InventoryScreen Owner { get; set; }
        public bool IsEmpty => Slot == null || Slot.IsEmpty;

        public void Bind(InventorySlot slot, bool equipped)
        {
            Slot = slot;
            var item = slot.Item;
            bool has = item != null;

            icon.enabled = has && item.Icon != null;
            if (icon.enabled) icon.sprite = item.Icon;
            initial.text = has && item.Icon == null && item.DisplayName.Length > 0 ? item.DisplayName.Substring(0, 1) : "";
            count.text = has && slot.Count > 1 ? slot.Count.ToString() : "";
            equippedMark.SetActive(has && equipped);
            SetDelay(0f);
        }

        public void SetDelay(float t01)
        {
            delayBar.enabled = t01 > 0f;
            delayBar.fillAmount = t01;
        }

        // ───── 입력: 모두 같은 상태로 모은다 ─────
        public override void OnPointerEnter(PointerEventData e)
        {
            base.OnPointerEnter(e);
            Owner.FocusSlot(this, isHover: true);
        }

        public override void OnPointerExit(PointerEventData e)
        {
            base.OnPointerExit(e);
            Owner.BlurSlot(this);
        }

        public override void OnSelect(BaseEventData e)
        {
            base.OnSelect(e);
            Owner.FocusSlot(this, isHover: false);
        }

        public override void OnDeselect(BaseEventData e)
        {
            base.OnDeselect(e);
            Owner.BlurSlot(this);
        }

        public void OnSubmit(BaseEventData e) => Owner.Use(this);

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left) Owner.Use(this);
        }
    }
}
