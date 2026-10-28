using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SesiVillage.UI
{
    /// <summary>
    /// HUD 퀘스트 블록 1개 (수업 UI 갤러리 QuestEntryView를 줄인 것). 들어올 때 높이를 펼치고,
    /// 끝나면 잠깐 "완료"를 보여 준 뒤 접힌다. 높이는 LayoutElement.preferredHeight로 바꾼다 (위치는 Vertical Layout Group이 맡음).
    /// </summary>
    public class QuestTrackerEntry : MonoBehaviour
    {
        [SerializeField] private LayoutElement layoutElement;
        [SerializeField] private CanvasGroup group;
        [SerializeField, Tooltip("실제 내용 (높이 재기용, Vertical Layout Group + Content Size Fitter)")]
        private RectTransform content;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text objectives;
        [SerializeField] private Image titleHighlight;

        private static readonly Color HighlightColor = new Color(1f, 0.75f, 0.35f, 0.32f);

        public string QuestId { get; private set; }
        public bool Finished { get; private set; }

        public void Init(string questId, string titleText)
        {
            QuestId = questId;
            title.text = titleText;
            titleHighlight.color = Color.clear;
        }

        public void SetObjectives(string text) => objectives.text = text;

        /// <summary>새 퀘스트: 높이 0에서 펼치며 나타나고, 제목이 잠깐 빛났다 꺼진다</summary>
        public IEnumerator Expand(float seconds = 0.32f)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            float h = LayoutUtility.GetPreferredHeight(content);
            titleHighlight.color = HighlightColor;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / seconds)
            {
                layoutElement.preferredHeight = Mathf.Lerp(0f, h, 1f - Mathf.Pow(1f - t, 3f));
                group.alpha = t;
                yield return null;
            }
            layoutElement.preferredHeight = -1f;   // -1 = 다시 내용 크기를 따름 (목표 줄 수가 바뀌어도 맞춰짐)
            group.alpha = 1f;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.6f)
            {
                titleHighlight.color = new Color(HighlightColor.r, HighlightColor.g, HighlightColor.b, HighlightColor.a * (1f - t));
                yield return null;
            }
            titleHighlight.color = Color.clear;
        }

        /// <summary>끝난 퀘스트: hold초 머문 뒤 접힌다. 게임 시간이라 UI가 열려 있거나 일시정지 중이면 기다림</summary>
        public IEnumerator HoldAndCollapse(float hold = 1.5f, float collapse = 0.25f)
        {
            Finished = true;
            yield return new WaitForSeconds(hold);
            float h = ((RectTransform)transform).rect.height;
            for (float t = 0f; t < 1f; t += Time.deltaTime / collapse)
            {
                layoutElement.preferredHeight = Mathf.Lerp(h, 0f, Mathf.SmoothStep(0f, 1f, t));
                group.alpha = 1f - t;
                yield return null;
            }
        }
    }
}
