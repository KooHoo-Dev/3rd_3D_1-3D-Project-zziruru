using SesiVillage.Interaction;
using TMPro;
using UnityEngine;

namespace SesiVillage.UI
{
    /// <summary>
    /// 주인공이 고른 상호작용 대상이 있으면 "E 대화하기"를 보여 준다.
    /// PlayerInteractor가 대상이 바뀔 때 알려 주면 그때만 갱신한다 (매 프레임 검사하지 않음).
    /// </summary>
    public class InteractionPromptView : MonoBehaviour
    {
        [SerializeField] private PlayerInteractor interactor;   // Core 씬의 Player
        [SerializeField] private GameObject root;                // 켜고 끌 판 (InteractionPrompt)
        [SerializeField] private TMP_Text label;
        [SerializeField] private string keyText = "E";

        private void Start()
        {
            root.SetActive(false);
            if (interactor == null) { Debug.LogError("[InteractionPromptView] PlayerInteractor가 연결되지 않음", this); return; }
            interactor.FocusChanged += OnFocusChanged;
        }

        private void OnDestroy()
        {
            if (interactor != null) interactor.FocusChanged -= OnFocusChanged;
        }

        private void OnFocusChanged(IInteractable target)
        {
            bool show = target != null;
            root.SetActive(show);
            if (show) label.text = $"{keyText}  {target.Prompt}";
        }
    }
}