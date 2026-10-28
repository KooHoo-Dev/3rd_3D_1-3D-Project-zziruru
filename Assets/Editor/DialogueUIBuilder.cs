using System.Linq;
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
    /// 메뉴 SesiVillage → UI → Build Dialogue UI (Core 씬을 열고 실행)
    /// 대화창 UI를 만들고 DialogueView·ChoiceButton 칸을 연결한다. 구조:
    ///   DialogueCanvas (Sort Order 10, HUD와 같은 Canvas Scaler)
    ///    └ DialogueRoot (stretch, CanvasGroup + DialogueView)
    ///       ├ Panel (아래 가운데 1400×260) ─ SpeakerName / Body / NextIndicator
    ///       └ Choices (Vertical Layout Group) ─ ChoiceButton ×4 ─ Content ─ Marker(▶) / Label
    /// 이미 DialogueCanvas가 있으면 지우고 다시 만든다 (몇 번 눌러도 결과가 같음). Ctrl+Z로 되돌릴 수 있음.
    /// Presenter(Yarn 연결부)는 붙이지 않는다 — NextSteps_Dialogue 4단계 할 일 ④에서 직접 붙인다.
    /// </summary>
    public static class DialogueUIBuilder
    {
        private const string FontPath = "Assets/External Assets/Font/Noto_Sans_KR/static/NotoSansKR-Regular SDF.asset";
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
        private const int ChoiceCount = 4;

        private static TMP_FontAsset font;

        [MenuItem("SesiVillage/UI/Build Dialogue UI")]
        public static void Build()
        {
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null) Debug.LogWarning($"[DialogueUIBuilder] 한글 폰트를 찾지 못함: {FontPath}");

            var old = GameObject.Find("DialogueCanvas");
            if (old != null) Undo.DestroyObjectImmediate(old);

            // 1. DialogueCanvas — 타자기로 매 프레임 바뀌므로 HUD와 Canvas를 나눈다 (HUD까지 다시 계산하지 않게)
            var canvasGo = new GameObject("DialogueCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(canvasGo, "Build Dialogue UI");
            canvasGo.layer = LayerMask.NameToLayer("UI");
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;   // HUD(0) 위에 그림
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // 2. DialogueRoot — 전체를 덮지만 Image가 없어 클릭을 막지 않는다
            var root = Rect("DialogueRoot", canvasGo.transform);
            Stretch(root, 0f, 0f, 0f, 0f);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            var view = root.gameObject.AddComponent<DialogueView>();

            // 3. Panel — 클릭하면 부모의 DialogueView까지 전달되어 다음 대사로
            var panel = Rect("Panel", root);
            Anchor(panel, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(1400f, 260f));
            var panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = new Color32(30, 22, 15, 220);
            panelImage.raycastTarget = true;

            // 4. 글자 3개 (모두 Raycast Target 끔 → Panel 클릭을 가로채지 않게)
            var speaker = Text("SpeakerName", panel, "정어언", 30f, TextAlignmentOptions.TopLeft);
            Anchor(speaker.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -20f), new Vector2(400f, 44f));
            speaker.fontStyle = FontStyles.Bold;
            speaker.color = new Color(1f, 0.93f, 0.7f);

            var body = Text("Body", panel, "대사가 여기에 나와요", 28f, TextAlignmentOptions.TopLeft);
            Stretch(body.rectTransform, 40f, 40f, 75f, 30f);
            body.textWrappingMode = TextWrappingModes.Normal;

            var next = Text("NextIndicator", panel, "▼", 28f, TextAlignmentOptions.Center);
            Anchor(next.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-30f, 20f), new Vector2(40f, 40f));

            // 5. Choices — 대사 상자 위 오른쪽, 아래에서부터 쌓음
            var choices = Rect("Choices", root);
            Anchor(choices, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-280f, 320f), new Vector2(520f, 260f));
            var layout = choices.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.LowerRight;
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            // 6. ChoiceButton ×4
            var buttons = new ChoiceButton[ChoiceCount];
            for (int i = 0; i < ChoiceCount; i++) buttons[i] = ChoiceButtonUI(choices, i);

            // 7. DialogueView 연결
            var so = new SerializedObject(view);
            so.FindProperty("root").objectReferenceValue = group;
            so.FindProperty("panel").objectReferenceValue = panel;
            so.FindProperty("speakerName").objectReferenceValue = speaker;
            so.FindProperty("body").objectReferenceValue = body;
            so.FindProperty("nextIndicator").objectReferenceValue = next.gameObject;
            var buttonsProp = so.FindProperty("choiceButtons");
            buttonsProp.arraySize = ChoiceCount;
            for (int i = 0; i < ChoiceCount; i++) buttonsProp.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];
            var advance = so.FindProperty("advanceActions");
            var interact = FindActionReference("Player", "Interact");
            advance.arraySize = interact != null ? 1 : 0;
            if (interact != null) advance.GetArrayElementAtIndex(0).objectReferenceValue = interact;
            else Debug.LogWarning("[DialogueUIBuilder] Player/Interact 입력을 찾지 못함 — DialogueView의 Advance Actions를 직접 넣으세요");
            so.ApplyModifiedPropertiesWithoutUndo();

            // 8. 저장은 사용자가 (씬을 '변경됨'으로 표시만)
            Selection.activeGameObject = root.gameObject;
            EditorSceneManager.MarkSceneDirty(canvasGo.scene);
            Debug.Log("[DialogueUIBuilder] 대화창 UI 생성 완료 — 씬을 저장하세요 (Ctrl+S)");
        }

        private static ChoiceButton ChoiceButtonUI(Transform parent, int index)
        {
            var rt = Rect("ChoiceButton" + (index == 0 ? "" : $" ({index})"), parent);
            rt.sizeDelta = new Vector2(0f, 56f);   // 높이만, 폭은 Layout Group이 정함
            var frame = rt.gameObject.AddComponent<Image>();
            var button = rt.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;   // 색은 ChoiceButton 스크립트가 직접 바꿈
            var choice = rt.gameObject.AddComponent<ChoiceButton>();

            var content = Rect("Content", rt);
            Stretch(content, 0f, 0f, 0f, 0f);

            var marker = Text("Marker", content, "▶", 24f, TextAlignmentOptions.Center);
            Anchor(marker.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(30f, 40f));

            var label = Text("Label", content, "선택지", 26f, TextAlignmentOptions.MidlineLeft);
            Stretch(label.rectTransform, 50f, 16f, 0f, 0f);

            var so = new SerializedObject(choice);
            so.FindProperty("frame").objectReferenceValue = frame;
            so.FindProperty("label").objectReferenceValue = label;
            so.FindProperty("content").objectReferenceValue = content;
            so.FindProperty("marker").objectReferenceValue = marker.gameObject;
            so.ApplyModifiedPropertiesWithoutUndo();
            return choice;
        }

        /* ───────── 작은 도우미 ───────── */

        private static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
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
            return t;
        }

        // 한 점 앵커: 앵커·피벗을 같은 곳에 두고 그 점에서의 위치·크기
        private static void Anchor(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        // 부모를 꽉 채우고 안쪽 여백만 둠 (Left, Right, Top, Bottom)
        private static void Stretch(RectTransform rt, float left, float right, float top, float bottom)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        // 입력 에셋 안의 액션 참조(InputActionReference)는 하위 에셋으로 들어 있다
        private static InputActionReference FindActionReference(string map, string action)
        {
            return AssetDatabase.LoadAllAssetsAtPath(InputActionsPath)
                .OfType<InputActionReference>()
                .FirstOrDefault(r => r.action != null && r.action.actionMap.name == map && r.action.name == action);
        }
    }
}
