using System;
using System.Collections;
using System.Collections.Generic;
using SesiVillage.Items;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SesiVillage.UI
{
    /// <summary>
    /// 인벤토리 화면 (I). 기획 6장: 위쪽 탭 3개(무기 / 옷 / 아이템·재료), 칸에 아이콘·개수, 마우스를 올리면 설명.
    /// 수업 UI 갤러리 InventoryScreen에서 툴팁 지연·포커스 처리는 그대로, 정렬·필터·장비 비교는 빼고 탭을 넣었다.
    ///
    /// 모델-뷰 분리: 데이터(Inventory)는 화면을 모른다. 이 화면이 Inventory.GetSlots로 읽어서 그리고,
    /// Inventory.Changed를 들어 열려 있을 때만 다시 그린다. 칸(SlotView)은 Owner에게 알리기만 한다.
    ///
    /// 옷·무기: 칸을 누르면 PlayerOutfit.Toggle(입기/벗기) / PlayerWeapon.Equip(바꿔 들기) — 화면(UI)이 시스템을 부르는 방향은 괜찮음.
    /// 반대로 두 시스템은 이 화면을 모르고, 이 화면이 Changed를 들어 "착용" 표시를 바꾼다. 다른 종류(먹을 것 등)는 ItemClicked 이벤트만 낸다
    /// </summary>
    public class InventoryScreen : MenuScreen
    {
        [Serializable]
        private struct TabButton
        {
            public InventoryTab tab;
            public Button button;
            public Image background;
        }

        [Header("화면")]
        [SerializeField] private TabButton[] tabButtons;
        [SerializeField] private TMP_Text tabTitle;
        [SerializeField, Tooltip("칸이 놓일 곳 (Grid Layout Group)")]
        private RectTransform grid;
        [SerializeField, Tooltip("복제할 칸 원본 (꺼 둔 상태로 grid 아래)")]
        private SlotView slotTemplate;
        [SerializeField] private ItemTooltipView tooltip;
        [SerializeField, Tooltip("캐릭터 미리보기 카메라 (PreviewStage). 인벤토리가 열려 있을 때만 켜서 그린다")]
        private Camera previewCamera;

        [Header("값")]
        [SerializeField, Range(0f, 0.8f), Tooltip("마우스를 올리고 툴팁이 뜨기까지 (키보드 선택은 절반)")]
        private float tooltipDelay = 0.25f;
        [SerializeField] private Color tabOn = new Color32(196, 150, 90, 255);
        [SerializeField] private Color tabOff = new Color32(70, 52, 36, 255);

        /// <summary>칸을 눌렀을 때 (아이템이 있는 칸만). 옷 입기 등은 이걸 듣는 쪽이 한다</summary>
        public event Action<ItemDefinition> ItemClicked;

        private readonly List<SlotView> slots = new List<SlotView>();
        private InventoryTab currentTab = InventoryTab.Item;
        private SlotView focused;
        private Coroutine pending;

        protected override void Awake()
        {
            base.Awake();
            if (previewCamera != null) previewCamera.enabled = false;
            slotTemplate.gameObject.SetActive(false);

            // 가장 칸이 많은 탭만큼 미리 만들어 두고, 탭마다 필요한 만큼만 켠다 (탭을 바꿀 때마다 만들고 지우지 않음)
            int max = 0;
            foreach (InventoryTab tab in Enum.GetValues(typeof(InventoryTab))) max = Mathf.Max(max, Inventory.SlotCountOf(tab));
            for (int i = 0; i < max; i++)
            {
                var s = Instantiate(slotTemplate, grid);
                s.name = $"Slot {i}";
                s.Owner = this;
                slots.Add(s);
            }

            foreach (var t in tabButtons)
            {
                var tab = t.tab;   // 람다가 반복 변수가 아니라 이번 값을 기억하도록 복사
                t.button.onClick.AddListener(() => SelectTab(tab));
            }
        }

        private void Start()
        {
            if (Inventory.Instance != null) Inventory.Instance.Changed += OnInventoryChanged;
            else Debug.LogError("[InventoryScreen] Inventory 없음", this);
            if (PlayerOutfit.Instance != null) PlayerOutfit.Instance.Changed += OnEquipmentChanged;
            if (PlayerWeapon.Instance != null) PlayerWeapon.Instance.Changed += OnWeaponChanged;
        }

        private void OnDestroy()
        {
            if (Inventory.Instance != null) Inventory.Instance.Changed -= OnInventoryChanged;
            if (PlayerOutfit.Instance != null) PlayerOutfit.Instance.Changed -= OnEquipmentChanged;
            if (PlayerWeapon.Instance != null) PlayerWeapon.Instance.Changed -= OnWeaponChanged;
        }

        protected override void OnOpened()
        {
            // 닫혀 있는 동안 아무도 안 보는 화면을 매 프레임 그리지 않도록 열 때만 켠다
            if (previewCamera != null) previewCamera.enabled = true;
            // 첫 칸을 미리 선택하지 않는다: 마우스가 주 입력이라 열자마자 툴팁이 뜨면 거슬림 (갤러리는 패드용으로 선택해 둠)
            Redraw();
        }

        protected override void OnClosed()
        {
            if (previewCamera != null) previewCamera.enabled = false;
            if (pending != null) StopCoroutine(pending);
            focused = null;
            tooltip.Hide();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        // 닫혀 있을 때는 다시 그리지 않는다 → 열 때 한 번 그림
        private void OnInventoryChanged()
        {
            if (!IsOpen) return;
            Redraw();
            RefreshTooltip();
        }

        // 입은 옷·든 무기가 바뀌면 "착용" 표시를 다시 그린다 (열려 있을 때만 — 닫혀 있으면 열 때 그림)
        private void OnEquipmentChanged()
        {
            if (IsOpen) { Redraw(); RefreshTooltip(); }
        }

        private void OnWeaponChanged(ItemDefinition weapon) => OnEquipmentChanged();

        // "착용" 표시를 할 아이템인가: 입은 옷이거나 든 무기
        private static bool IsEquipped(ItemDefinition item)
        {
            if (item == null) return false;
            if (item.Type == ItemType.Outfit) return PlayerOutfit.Instance != null && PlayerOutfit.Instance.IsWorn(item);
            if (item.Type == ItemType.Weapon) return PlayerWeapon.Instance != null && PlayerWeapon.Instance.Equipped == item;
            return false;
        }

        public void SelectTab(InventoryTab tab)
        {
            if (currentTab == tab) return;
            currentTab = tab;
            tooltip.Hide();
            focused = null;
            Redraw();
        }

        // ───────── 그리기 ─────────

        private void Redraw()
        {
            var data = Inventory.Instance.GetSlots(currentTab);
            for (int i = 0; i < slots.Count; i++)
            {
                bool used = i < data.Count;
                slots[i].gameObject.SetActive(used);
                if (used) slots[i].Bind(data[i], IsEquipped(data[i].Item));
            }
            foreach (var t in tabButtons) t.background.color = t.tab == currentTab ? tabOn : tabOff;
            tabTitle.text = TabName(currentTab);
        }

        // ───────── 포커스 · 툴팁 ─────────

        public void FocusSlot(SlotView s, bool isHover)
        {
            focused = s;
            if (pending != null) StopCoroutine(pending);
            if (s.IsEmpty) { tooltip.Hide(); return; }

            // 툴팁이 이미 떠 있으면 옆 칸으로 옮길 때 기다리지 않고 바로 바꾼다
            if (tooltip.IsOpen) ShowFor(s);
            else pending = StartCoroutine(ShowAfter(s, isHover ? tooltipDelay : tooltipDelay * 0.5f));
        }

        public void BlurSlot(SlotView s)
        {
            if (s != focused) return;
            if (pending != null) StopCoroutine(pending);
            s.SetDelay(0f);
            // 다음 칸의 FocusSlot이 같은 프레임에 오므로, 한 프레임 기다렸다가 아무 칸도 포커스가 없을 때만 닫는다
            StartCoroutine(HideIfNoFocus(s));
        }

        private IEnumerator ShowAfter(SlotView s, float delay)
        {
            for (float t = 0f; t < delay; t += Time.unscaledDeltaTime)
            {
                s.SetDelay(t / delay);
                yield return null;
            }
            s.SetDelay(0f);
            ShowFor(s);
            pending = null;
        }

        private IEnumerator HideIfNoFocus(SlotView s)
        {
            yield return null; // 다음 프레임까지 쉬기
            if (focused == s) { focused = null; tooltip.Hide(); }
        }

        private void ShowFor(SlotView s)
        {
            tooltip.Show(s.Slot, IsEquipped(s.Slot.Item), (RectTransform)s.transform);
        }

        /// <summary>같은 칸이 계속 포커스면 OnSelect가 다시 오지 않으므로, 내용이 바뀌었을 때 툴팁을 직접 갱신</summary>
        private void RefreshTooltip()
        {
            if (focused == null || !tooltip.IsOpen) return;
            if (focused.IsEmpty) tooltip.Hide();
            else ShowFor(focused);
        }

        // ───────── 동작 ─────────

        public void Use(SlotView s)
        {
            if (s.IsEmpty) return;
            var item = s.Slot.Item;
            if (item.Type == ItemType.Outfit && PlayerOutfit.Instance != null) PlayerOutfit.Instance.Toggle(item);
            else if (item.Type == ItemType.Weapon && PlayerWeapon.Instance != null) PlayerWeapon.Instance.Equip(item);
            ItemClicked?.Invoke(item);
        }

        private static string TabName(InventoryTab tab) => tab switch
        {
            InventoryTab.Weapon => "무기",
            InventoryTab.Outfit => "옷",
            _ => "아이템 · 재료",
        };
    }
}
