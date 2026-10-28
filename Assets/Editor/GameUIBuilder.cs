using System.Linq;
using SesiVillage.Items;
using SesiVillage.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SesiVillage.EditorTools
{
    /// <summary>
    /// 메뉴 SesiVillage → UI → Build Game UI (Core 씬을 열고 실행)
    /// 획득 알림·퀘스트 HUD·인벤토리(I)·퀘스트 목록(Q)·툴팁을 만들고 스크립트 칸을 모두 연결한다. 구조:
    ///   HUDCanvas (이미 있음)
    ///    ├ Toasts (ToastStackView + NotificationFeed) ─ Stack ─ ToastCard(원본, 꺼짐) ─ Card ─ Icon / Message / Count / Timer
    ///    │                                             └ Overflow ("+N")
    ///    └ QuestTracker (QuestTrackerView) ─ Entry(원본, 꺼짐) ─ TitleRow ─ Title / Objectives
    ///   MenuCanvas (Sort Order 20 — 대화창(10) 위)
    ///    ├ InventoryScreen (CanvasGroup + InventoryScreen) ─ Dim / Window ─ Preview ─ PreviewImage(RawImage) / Tabs ×3 / TabTitle / Grid ─ Slot(원본, 꺼짐) / Hint
    ///    ├ QuestLogScreen (CanvasGroup + QuestLogScreen) ─ Dim / Window ─ List ─ Header·Entry(원본) / Empty / Detail / Hint
    ///    └ TooltipLayer ─ Tooltip (ItemTooltipView) — 맨 마지막 형제라 항상 위에 그려짐
    /// 이미 있으면 지우고 다시 만든다 (몇 번 눌러도 결과가 같음). Ctrl+Z로 되돌릴 수 있음. 색·크기는 임시 (UI 스킨은 나중에 — 기획 미정 1번)
    /// </summary>
    public static class GameUIBuilder
    {
        private const string FontPath = "Assets/External Assets/Font/Noto_Sans_KR/static/NotoSansKR-Regular SDF.asset";
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";

        private static readonly Color Panel = new Color32(30, 22, 15, 230);
        private static readonly Color PanelLight = new Color32(60, 45, 32, 255);
        private static readonly Color Gold = new Color32(232, 195, 106, 255);

        private static TMP_FontAsset font;
        private static Sprite whiteSprite;

        [MenuItem("SesiVillage/UI/Build Game UI (알림·HUD·인벤토리·퀘스트 목록)")]
        public static void Build()
        {
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null) Debug.LogWarning($"[GameUIBuilder] 한글 폰트를 찾지 못함: {FontPath}");
            // Filled 이미지는 스프라이트가 있어야 채우기가 보인다 → Unity 기본 UI 스프라이트
            whiteSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            var hud = GameObject.Find("HUDCanvas");
            if (hud == null) { Debug.LogError("[GameUIBuilder] HUDCanvas가 없음 — 먼저 Build Interaction Prompt를 실행하세요"); return; }

            Remove(hud.transform.Find("Toasts"));
            Remove(hud.transform.Find("QuestTracker"));
            var oldMenu = GameObject.Find("MenuCanvas");
            if (oldMenu != null) Undo.DestroyObjectImmediate(oldMenu);

            BuildToasts(hud.transform);
            BuildTracker(hud.transform);

            var menu = CreateCanvas("MenuCanvas", 20);
            var tooltip = BuildTooltipLayer(menu);         // 화면들이 툴팁을 참조하므로 먼저 만들고
            BuildInventory(menu, tooltip);
            BuildQuestLog(menu);
            tooltip.transform.parent.SetAsLastSibling();   // 그리는 순서는 맨 위로
            OutfitSetupBuilder.WirePreviewUI();             // 미리보기 무대가 이미 있으면 새 인벤토리 화면에 다시 연결

            EditorSceneManager.MarkSceneDirty(hud.scene);
            Selection.activeGameObject = menu.gameObject;
            Debug.Log("[GameUIBuilder] 게임 UI 생성 완료 — 씬을 저장하세요 (Ctrl+S)");
        }

        /* ───────── 획득 알림 ───────── */

        private static void BuildToasts(Transform hud)
        {
            var root = Rect("Toasts", hud);
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Build Game UI");
            // 왼쪽 아래, 대화창(아래 가운데, 높이 300) 위로 올려서 겹치지 않게
            Anchor(root, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(40f, 320f), new Vector2(460f, 300f));

            var stack = Rect("Stack", root);
            Stretch(stack, 0f, 0f, 0f, 0f);
            var layout = stack.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.LowerLeft;   // 아래에서 위로 쌓임
            layout.childControlWidth = true;
            layout.childControlHeight = true;               // 카드 높이 = LayoutElement.preferredHeight (접기·펼치기 연출)
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            // 카드 원본
            var slot = Rect("ToastCard", stack);
            var slotLayout = slot.gameObject.AddComponent<LayoutElement>();
            slotLayout.preferredHeight = 62f;
            var cardView = slot.gameObject.AddComponent<ToastCardView>();

            var card = Rect("Card", slot);
            Anchor(card, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(420f, 56f));
            Img(card, Panel, false);
            var group = card.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;   // 알림은 보여 주기만 (클릭이 게임으로 통과)

            var icon = Rect("Icon", card);
            Anchor(icon, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(40f, 40f));
            var iconImage = Img(icon, Color.white, false);
            iconImage.preserveAspect = true;

            var message = Text("Message", card, "떡국을 얻었습니다", 24f, TextAlignmentOptions.MidlineLeft);
            Stretch(message.rectTransform, 60f, 70f, 0f, 0f);

            var count = Text("Count", card, "×2", 24f, TextAlignmentOptions.MidlineRight);
            Anchor(count.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Vector2(60f, 40f));
            count.color = Gold;
            count.fontStyle = FontStyles.Bold;

            var timer = Rect("Timer", card);
            timer.anchorMin = new Vector2(0f, 0f);
            timer.anchorMax = new Vector2(1f, 0f);
            timer.pivot = new Vector2(0.5f, 0f);
            timer.offsetMin = new Vector2(0f, 0f);
            timer.offsetMax = new Vector2(0f, 3f);
            var timerFill = Filled(timer, new Color(Gold.r, Gold.g, Gold.b, 0.8f));

            Wire(cardView, ("slot", slotLayout), ("card", card), ("group", group), ("icon", iconImage),
                 ("message", message), ("countBadge", count), ("timerFill", timerFill));
            slot.gameObject.SetActive(false);

            // "+N" — 레이아웃 밖(Stack 형제)이라 카드 위치에 영향 없음
            var overflow = Text("Overflow", root, "+2", 20f, TextAlignmentOptions.BottomLeft);
            Anchor(overflow.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(4f, 4f), new Vector2(120f, 30f));
            overflow.color = Gold;

            var view = root.gameObject.AddComponent<ToastStackView>();
            Wire(view, ("stack", stack), ("cardTemplate", cardView), ("overflowLabel", overflow));
            root.gameObject.AddComponent<NotificationFeed>();
        }

        /* ───────── 퀘스트 HUD ───────── */

        private static void BuildTracker(Transform hud)
        {
            var root = Rect("QuestTracker", hud);
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Build Game UI");
            Anchor(root, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -40f), new Vector2(400f, 600f));
            var layout = root.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperRight;
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            // 블록 원본: 자기 Vertical Layout Group으로 내용 높이를 정하고, LayoutElement로 펼치기·접기 연출
            var entry = Rect("Entry", root);
            Img(entry, new Color32(20, 15, 10, 170), false);
            entry.gameObject.AddComponent<RectMask2D>();   // 접히는 동안 넘치는 글자를 잘라냄
            var entryLayout = entry.gameObject.AddComponent<LayoutElement>();
            var entryGroup = entry.gameObject.AddComponent<CanvasGroup>();
            entryGroup.blocksRaycasts = false;
            var inner = entry.gameObject.AddComponent<VerticalLayoutGroup>();
            inner.padding = new RectOffset(14, 14, 10, 12);
            inner.spacing = 4f;
            inner.childControlWidth = true;
            inner.childControlHeight = true;
            inner.childForceExpandWidth = true;
            inner.childForceExpandHeight = false;
            var entryView = entry.gameObject.AddComponent<QuestTrackerEntry>();

            var titleRow = Rect("TitleRow", entry);
            var highlight = Img(titleRow, Color.clear, false);
            var rowLayout = titleRow.gameObject.AddComponent<VerticalLayoutGroup>();
            rowLayout.padding = new RectOffset(4, 4, 2, 2);
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = true;
            rowLayout.childForceExpandHeight = false;

            var title = Text("Title", titleRow, "잡초 뽑기", 24f, TextAlignmentOptions.TopLeft);
            title.fontStyle = FontStyles.Bold;
            title.color = Gold;

            var objectives = Text("Objectives", entry, "· 묘지의 잡초 5포기 베기", 20f, TextAlignmentOptions.TopLeft);
            objectives.textWrappingMode = TextWrappingModes.Normal;

            Wire(entryView, ("layoutElement", entryLayout), ("group", entryGroup), ("content", entry),
                 ("title", title), ("objectives", objectives), ("titleHighlight", highlight));
            entry.gameObject.SetActive(false);

            var view = root.gameObject.AddComponent<QuestTrackerView>();
            Wire(view, ("listParent", root), ("entryTemplate", entryView));
        }

        /* ───────── 툴팁 ───────── */

        private static ItemTooltipView BuildTooltipLayer(Canvas menu)
        {
            var layer = Rect("TooltipLayer", menu.transform);
            Stretch(layer, 0f, 0f, 0f, 0f);

            var tip = Rect("Tooltip", layer);
            tip.sizeDelta = new Vector2(340f, 0f);   // 폭 고정, 높이는 내용에 맞춤
            Img(tip, new Color32(20, 15, 10, 245), false);
            tip.gameObject.AddComponent<CanvasGroup>();
            var layout = tip.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 14, 16);
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = tip.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var name = Text("ItemName", tip, "떡국", 26f, TextAlignmentOptions.TopLeft);
            name.fontStyle = FontStyles.Bold;
            name.color = Gold;
            var kind = Text("Kind", tip, "먹을 것", 18f, TextAlignmentOptions.TopLeft);
            kind.color = new Color(0.75f, 0.7f, 0.62f);
            var badge = Text("EquippedBadge", tip, "착용 중", 18f, TextAlignmentOptions.TopLeft);
            badge.color = new Color(0.55f, 0.85f, 0.6f);
            var desc = Text("Description", tip, "설명", 20f, TextAlignmentOptions.TopLeft);
            desc.textWrappingMode = TextWrappingModes.Normal;

            var view = tip.gameObject.AddComponent<ItemTooltipView>();
            Wire(view, ("canvas", menu), ("layer", layer), ("itemName", name), ("kind", kind),
                 ("equippedBadge", badge.gameObject), ("description", desc));
            return view;
        }

        /* ───────── 인벤토리 ───────── */

        private static void BuildInventory(Canvas menu, ItemTooltipView tooltip)
        {
            var root = ScreenRoot("InventoryScreen", menu.transform, out var window, new Vector2(1140f, 800f), "소지품", "I / Esc  닫기 · 칸에 마우스를 올리면 설명 · 옷을 누르면 입기/벗기 · 캐릭터를 끌면 회전");

            // 왼쪽: 캐릭터 미리보기 (젤다식). 미리보기 카메라가 그린 RenderTexture를 여기에 보여 준다
            //   RawImage = Texture를 그대로 보여 주는 UI (Image는 Sprite만 받음 → RenderTexture는 RawImage)
            //   Texture 칸과 회전 스크립트(CharacterPreviewDrag)는 OutfitSetupBuilder가 연결 (아래 Build 끝에서도 다시 연결)
            var preview = Rect("Preview", window);
            Anchor(preview, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -80f), new Vector2(380f, 640f));
            Img(preview, new Color32(20, 15, 10, 255), false);
            var previewImage = Rect("PreviewImage", preview);
            Stretch(previewImage, 0f, 0f, 0f, 0f);
            previewImage.gameObject.AddComponent<RawImage>().raycastTarget = true;   // 끌어서 돌리기 입력을 받음

            // 탭 3개
            var tabs = Rect("Tabs", window);
            Anchor(tabs, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(200f, -80f), new Vector2(660f, 50f));
            var tabsLayout = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
            tabsLayout.spacing = 10f;
            tabsLayout.childControlWidth = true;
            tabsLayout.childControlHeight = true;
            tabsLayout.childForceExpandWidth = true;
            tabsLayout.childForceExpandHeight = true;

            var order = new[] { (InventoryTab.Weapon, "무기"), (InventoryTab.Outfit, "옷"), (InventoryTab.Item, "아이템 · 재료") };
            var tabButtons = new (InventoryTab tab, Button button, Image bg)[order.Length];
            for (int i = 0; i < order.Length; i++)
            {
                var t = Rect("Tab " + order[i].Item2, tabs);
                var bg = Img(t, PanelLight, true);
                var button = t.gameObject.AddComponent<Button>();
                button.targetGraphic = bg;
                var label = Text("Label", t, order[i].Item2, 22f, TextAlignmentOptions.Center);
                Stretch(label.rectTransform, 0f, 0f, 0f, 0f);
                tabButtons[i] = (order[i].Item1, button, bg);
            }

            var tabTitle = Text("TabTitle", window, "아이템 · 재료", 22f, TextAlignmentOptions.MidlineLeft);
            Anchor(tabTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(440f, -140f), new Vector2(400f, 36f));
            tabTitle.color = Gold;

            // 칸 격자: 6열, 26칸이면 5줄
            var grid = Rect("Grid", window);
            Anchor(grid, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(200f, -185f), new Vector2(660f, 540f));
            var gridLayout = grid.gameObject.AddComponent<GridLayoutGroup>();
            gridLayout.cellSize = new Vector2(100f, 100f);
            gridLayout.spacing = new Vector2(12f, 12f);
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = 6;
            gridLayout.childAlignment = TextAnchor.UpperLeft;

            var slotView = BuildSlot(grid);

            var view = root.gameObject.AddComponent<InventoryScreen>();
            var so = new SerializedObject(view);
            SetMenuInputs(so, "Inventory");
            so.FindProperty("tabTitle").objectReferenceValue = tabTitle;
            so.FindProperty("grid").objectReferenceValue = grid;
            so.FindProperty("slotTemplate").objectReferenceValue = slotView;
            so.FindProperty("tooltip").objectReferenceValue = tooltip;
            var arr = so.FindProperty("tabButtons");
            arr.arraySize = tabButtons.Length;
            for (int i = 0; i < tabButtons.Length; i++)
            {
                var el = arr.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("tab").enumValueIndex = (int)tabButtons[i].tab;
                el.FindPropertyRelative("button").objectReferenceValue = tabButtons[i].button;
                el.FindPropertyRelative("background").objectReferenceValue = tabButtons[i].bg;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static SlotView BuildSlot(Transform grid)
        {
            var slot = Rect("SlotTemplate", grid);
            var frame = Img(slot, PanelLight, true);   // 마우스를 받는 곳

            var icon = Rect("Icon", slot);
            Stretch(icon, 12f, 12f, 12f, 12f);
            var iconImage = Img(icon, Color.white, false);
            iconImage.preserveAspect = true;

            var initial = Text("Initial", slot, "떡", 40f, TextAlignmentOptions.Center);
            Stretch(initial.rectTransform, 0f, 0f, 0f, 0f);
            initial.color = new Color(1f, 0.95f, 0.85f, 0.9f);

            var count = Text("Count", slot, "5", 20f, TextAlignmentOptions.BottomRight);
            Stretch(count.rectTransform, 6f, 8f, 6f, 4f);
            count.fontStyle = FontStyles.Bold;

            var mark = Text("EquippedMark", slot, "착용", 16f, TextAlignmentOptions.TopLeft);
            Stretch(mark.rectTransform, 6f, 6f, 4f, 6f);
            mark.color = new Color(0.55f, 0.85f, 0.6f);

            var delay = Rect("DelayBar", slot);
            delay.anchorMin = new Vector2(0f, 0f);
            delay.anchorMax = new Vector2(1f, 0f);
            delay.pivot = new Vector2(0.5f, 0f);
            delay.offsetMin = Vector2.zero;
            delay.offsetMax = new Vector2(0f, 4f);
            var delayFill = Filled(delay, Gold);

            var view = slot.gameObject.AddComponent<SlotView>();
            view.targetGraphic = frame;   // 마우스를 올리거나 선택하면 칸 색이 밝아짐 (Selectable 기본 Color Tint)
            Wire(view, ("icon", iconImage), ("initial", initial), ("count", count),
                 ("equippedMark", mark.gameObject), ("delayBar", delayFill));
            slot.gameObject.SetActive(false);
            return view;
        }

        /* ───────── 퀘스트 목록 ───────── */

        private static void BuildQuestLog(Canvas menu)
        {
            var root = ScreenRoot("QuestLogScreen", menu.transform, out var window, new Vector2(1100f, 680f), "부탁 목록", "Q / Esc  닫기 · 부탁을 누르면 할 일");

            // 왼쪽 목록
            var list = Rect("List", window);
            Anchor(list, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -80f), new Vector2(340f, 520f));
            var listLayout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            listLayout.spacing = 4f;
            listLayout.childControlWidth = true;
            listLayout.childControlHeight = true;
            listLayout.childForceExpandWidth = true;
            listLayout.childForceExpandHeight = false;

            var header = Text("HeaderTemplate", list, "진행 중", 20f, TextAlignmentOptions.BottomLeft);
            header.color = Gold;
            header.gameObject.AddComponent<LayoutElement>().preferredHeight = 36f;

            var entry = Rect("EntryTemplate", list);
            var entryBg = Img(entry, Color.clear, true);
            var entryButton = entry.gameObject.AddComponent<Button>();
            entryButton.targetGraphic = entryBg;
            entry.gameObject.AddComponent<LayoutElement>().preferredHeight = 44f;
            var entryLabel = Text("Label", entry, "잡초 뽑기", 22f, TextAlignmentOptions.MidlineLeft);
            Stretch(entryLabel.rectTransform, 14f, 8f, 0f, 0f);

            var empty = Text("Empty", window, "아직 받은 부탁이 없어요.", 22f, TextAlignmentOptions.TopLeft);
            Anchor(empty.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -90f), new Vector2(340f, 40f));
            empty.color = new Color(0.75f, 0.7f, 0.62f);

            // 칸막이
            var divider = Rect("Divider", window);
            Anchor(divider, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(390f, -80f), new Vector2(2f, 520f));
            Img(divider, PanelLight, false);

            // 오른쪽 설명
            var detail = Rect("Detail", window);
            Anchor(detail, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(420f, -80f), new Vector2(650f, 520f));
            var detailLayout = detail.gameObject.AddComponent<VerticalLayoutGroup>();
            detailLayout.spacing = 12f;
            detailLayout.childControlWidth = true;
            detailLayout.childControlHeight = true;
            detailLayout.childForceExpandWidth = true;
            detailLayout.childForceExpandHeight = false;

            var dTitle = Text("Title", detail, "잡초 뽑기", 30f, TextAlignmentOptions.TopLeft);
            dTitle.fontStyle = FontStyles.Bold;
            var dState = Text("State", detail, "진행 중", 20f, TextAlignmentOptions.TopLeft);
            dState.color = Gold;
            var dDesc = Text("Description", detail, "설명", 22f, TextAlignmentOptions.TopLeft);
            dDesc.textWrappingMode = TextWrappingModes.Normal;
            var dObj = Text("Objectives", detail, "· 목표", 22f, TextAlignmentOptions.TopLeft);
            dObj.textWrappingMode = TextWrappingModes.Normal;

            var view = root.gameObject.AddComponent<QuestLogScreen>();
            var so = new SerializedObject(view);
            SetMenuInputs(so, "QuestLog");
            so.FindProperty("list").objectReferenceValue = list;
            so.FindProperty("headerTemplate").objectReferenceValue = header;
            so.FindProperty("entryTemplate").objectReferenceValue = entryButton;
            so.FindProperty("emptyLabel").objectReferenceValue = empty;
            so.FindProperty("detailRoot").objectReferenceValue = detail.gameObject;
            so.FindProperty("detailTitle").objectReferenceValue = dTitle;
            so.FindProperty("detailState").objectReferenceValue = dState;
            so.FindProperty("detailDescription").objectReferenceValue = dDesc;
            so.FindProperty("detailObjectives").objectReferenceValue = dObj;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /* ───────── 공통 ───────── */

        /// <summary>전체 화면 메뉴 뼈대: 어두운 배경(Dim) + 가운데 창(Window) + 제목 + 아래 안내</summary>
        private static RectTransform ScreenRoot(string name, Transform parent, out RectTransform window, Vector2 size, string title, string hint)
        {
            var root = Rect(name, parent);
            Stretch(root, 0f, 0f, 0f, 0f);
            root.gameObject.AddComponent<CanvasGroup>();

            var dim = Rect("Dim", root);
            Stretch(dim, 0f, 0f, 0f, 0f);
            Img(dim, new Color(0f, 0f, 0f, 0.45f), true);   // 뒤의 HUD·세상 클릭을 막음

            window = Rect("Window", root);
            Anchor(window, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            Img(window, Panel, true);

            var t = Text("Title", window, title, 30f, TextAlignmentOptions.MidlineLeft);
            Anchor(t.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -14f), new Vector2(500f, 44f));
            t.fontStyle = FontStyles.Bold;

            var h = Text("Hint", window, hint, 18f, TextAlignmentOptions.MidlineRight);
            Anchor(h.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 14f), new Vector2(700f, 30f));
            h.color = new Color(0.75f, 0.7f, 0.62f);
            return root;
        }

        private static void SetMenuInputs(SerializedObject so, string toggleActionName)
        {
            var toggle = FindActionReference("Player", toggleActionName);
            var cancel = FindActionReference("UI", "Cancel");
            if (toggle == null) Debug.LogWarning($"[GameUIBuilder] Player/{toggleActionName} 입력을 찾지 못함 — Toggle Action을 직접 넣으세요");
            if (cancel == null) Debug.LogWarning("[GameUIBuilder] UI/Cancel 입력을 찾지 못함 — Cancel Action을 직접 넣으세요");
            so.FindProperty("toggleAction").objectReferenceValue = toggle;
            so.FindProperty("cancelAction").objectReferenceValue = cancel;
        }

        private static Canvas CreateCanvas(string name, int sortOrder)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(go, "Build Game UI");
            go.layer = LayerMask.NameToLayer("UI");
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private static void Remove(Transform t)
        {
            if (t != null) Undo.DestroyObjectImmediate(t.gameObject);
        }

        /// <summary>("필드 이름", 값) 묶음으로 [SerializeField] 칸을 채운다</summary>
        private static void Wire(Object target, params (string field, Object value)[] pairs)
        {
            var so = new SerializedObject(target);
            foreach (var (field, value) in pairs)
            {
                var p = so.FindProperty(field);
                if (p == null) { Debug.LogError($"[GameUIBuilder] {target.GetType().Name}에 '{field}' 칸이 없음"); continue; }
                p.objectReferenceValue = value;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static Image Img(RectTransform rt, Color color, bool raycast)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        private static Image Filled(RectTransform rt, Color color)
        {
            var img = Img(rt, color, false);
            img.sprite = whiteSprite;
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillOrigin = (int)Image.OriginHorizontal.Left;
            img.fillAmount = 1f;
            return img;
        }

        private static TextMeshProUGUI Text(string name, Transform parent, string text, float size, TextAlignmentOptions alignment)
        {
            var t = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = text;
            t.fontSize = size;
            t.alignment = alignment;
            t.color = Color.white;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }

        private static void Anchor(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        private static void Stretch(RectTransform rt, float left, float right, float top, float bottom)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        private static InputActionReference FindActionReference(string map, string action)
        {
            return AssetDatabase.LoadAllAssetsAtPath(InputActionsPath)
                .OfType<InputActionReference>()
                .FirstOrDefault(r => r.action != null && r.action.actionMap.name == map && r.action.name == action);
        }
    }
}
