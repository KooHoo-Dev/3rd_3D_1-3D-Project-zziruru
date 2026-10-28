#nullable enable
using System.Collections.Generic;
using UnityEngine;
using Yarn.Unity;
using System;

namespace SesiVillage.UI
{
    /// <summary>
    /// Yarn Spinner와 우리 대화창(DialogueView)을 잇는 어댑터.
    /// DialogueRunner가 대사·선택지를 보내면 DialogueView에 보여 주고, 플레이어가 넘기거나 고를 때까지 기다린다.
    /// 대화가 시작·끝날 때 UIState를 열고 닫는다 → 커서·이동·카메라·E 상호작용이 잠긴다.
    /// </summary>
    public class YarnDialoguePresenter : DialoguePresenterBase
    {
        [SerializeField] private DialogueView view = null!;   // Inspector에서 연결 (null! = "실행 땐 반드시 채워져 있다"는 표시)
        
        public override YarnTask OnDialogueStartedAsync()
        {
            if (UIState.Instance != null) UIState.Instance.Open(this);
            view.Open();
            return YarnTask.CompletedTask;
        }

        public override YarnTask OnDialogueCompleteAsync()
        {
            view.Close();
            if (UIState.Instance != null) UIState.Instance.Close(this);
            return YarnTask.CompletedTask;
        }
        
        public override async YarnTask RunLineAsync(LocalizedLine line, LineCancellationToken token)
        {
            try
            {
                await view.ShowLineAsync(line.CharacterName, line.TextWithoutCharacterName.Text, token.NextContentToken);
            }
            catch (OperationCanceledException) { }   // Yarn이 "이 줄은 넘어가" 하고 취소함 → 그냥 끝
        }

        public override async YarnTask<DialogueOption?> RunOptionsAsync(DialogueOption[] options, LineCancellationToken token)
        {
            // 조건으로 막힌 선택지(IsAvailable = false)는 보여 주지 않는다
            var available = new List<DialogueOption>();
            var texts = new List<string>();
            foreach (var option in options)
            {
                if (!option.IsAvailable) continue;
                available.Add(option);
                texts.Add(option.Line.TextWithoutCharacterName.Text);
            }

            try
            {
                int chosen = await view.ShowChoicesAsync(texts, token.NextContentToken);
                return available[chosen];
            }
            catch (OperationCanceledException) { return null; }
        }
    }
}
