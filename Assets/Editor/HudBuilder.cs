using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace SesiVillage.EditorTools
{
    /// <summary>
    /// 메뉴 SesiVillage → UI → Build Interaction Prompt
    /// 열린 씬(Core)에 HUDCanvas·EventSystem이 없으면 만들고, 그 아래 "E 대화하기" 안내 판(InteractionPrompt + Label)을 만든다.
    /// 이미 InteractionPrompt가 있으면 지우고 다시 만든다 (몇 번 눌러도 결과가 같음). Ctrl+Z로 되돌릴 수 있음.
    /// </summary>
    public static class HudBuilder
    {
        private const string FontPath = "Assets/External Assets/Font/Noto_Sans_KR/static/NotoSansKR-Regular SDF.asset";

        [MenuItem("SesiVillage/UI/Build Interaction Prompt")]
        public static void BuildInteractionPrompt()
        {
            Canvas canvas = FindOrCreateCanvas();
            EnsureEventSystem();

            Transform old = canvas.transform.Find("InteractionPrompt");
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

            // 배경 판: 아래 가운데 기준, 대화창 위(Y 260)에 뜸
            var prompt = new GameObject("InteractionPrompt", typeof(RectTransform), typeof(Image));
            Undo.RegisterCreatedObjectUndo(prompt, "Build Interaction Prompt");
            prompt.layer = LayerMask.NameToLayer("UI");
            prompt.transform.SetParent(canvas.transform, false);

            var rt = (RectTransform)prompt.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);   // 앵커: 화면 아래 가운데
            rt.pivot = new Vector2(0.5f, 0f);                        // 피벗: 판의 아래 가운데
            rt.anchoredPosition = new Vector2(0f, 260f);
            rt.sizeDelta = new Vector2(240f, 56f);

            var image = prompt.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 150f / 255f);       // 반투명 검정
            image.raycastTarget = false;                             // 보여 주기만 → 클릭을 가로채지 않게

            // 글자: 판을 꽉 채움
            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.layer = prompt.layer;
            labelGo.transform.SetParent(prompt.transform, false);

            var lrt = (RectTransform)labelGo.transform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.pivot = new Vector2(0.5f, 0.5f);
            lrt.offsetMin = lrt.offsetMax = Vector2.zero;

            var label = labelGo.GetComponent<TextMeshProUGUI>();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font != null) label.font = font;
            else Debug.LogWarning($"[HudBuilder] 한글 폰트를 찾지 못함: {FontPath}");
            label.text = "E  대화하기";
            label.fontSize = 26;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;

            Selection.activeGameObject = prompt;
            EditorSceneManager.MarkSceneDirty(prompt.scene);
            Debug.Log("[HudBuilder] InteractionPrompt 생성 완료 — 씬을 저장하세요 (Ctrl+S)");
        }

        private static Canvas FindOrCreateCanvas()
        {
            var existing = GameObject.Find("HUDCanvas");
            if (existing != null && existing.TryGetComponent(out Canvas found)) return found;

            var go = new GameObject("HUDCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(go, "Create HUDCanvas");
            go.layer = LayerMask.NameToLayer("UI");

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private static void EnsureEventSystem()
        {
            var es = Object.FindFirstObjectByType<EventSystem>();
            if (es == null)
            {
                var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                Undo.RegisterCreatedObjectUndo(go, "Create EventSystem");
                return;
            }
            // 옛 입력 모듈이면 새 Input System용으로 교체
            if (es.TryGetComponent(out StandaloneInputModule oldModule))
            {
                Undo.DestroyObjectImmediate(oldModule);
                Undo.AddComponent<InputSystemUIInputModule>(es.gameObject);
            }
        }
    }
}
