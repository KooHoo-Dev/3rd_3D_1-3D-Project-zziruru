using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace SesiVillage.UI
{
    /// <summary>
    /// 대화창 + 선택지 (UGUI). 수업 UI 갤러리의 DialogueView를 우리 게임에 맞게 줄인 것.
    /// 대사가 어디서 오는지(Yarn 등)는 모른다 → 바깥에서 ShowLine / ShowChoices를 부르고, 끝나면 콜백으로 알려 준다.
    ///  · 타자기: 문장 전체를 넣고 maxVisibleCharacters만 늘린다 (태그·줄바꿈이 깨지지 않음)
    ///  · 입력 소비: 타이핑 중 첫 입력 = 문장 완성, 다음 입력 = 다음 줄. 입력을 처리한 프레임은 거기서 끝낸다
    ///  · 입력 잠금: 선택지가 열린 직후 inputLockSeconds 동안 확정을 무시 → 연타가 선택지를 관통하지 않는다
    ///  · 시간: Time.unscaledDeltaTime으로 흐른다 (일시정지 중에도 대화는 진행)
    /// </summary>
    public class DialogueView : MonoBehaviour, IPointerClickHandler
    {
        public enum State { Hidden, Typing, LineComplete, ChoiceOpen, ChoiceConfirmed }

        [Header("패널")]
        [SerializeField] private CanvasGroup root;              // 대화창 전체 (투명도)
        [SerializeField] private RectTransform panel;           // 아래에서 올라오는 상자
        [SerializeField] private TMP_Text speakerName;          // 왼쪽 위 NPC 이름
        [SerializeField] private TMP_Text body;                 // 대사
        [SerializeField] private GameObject nextIndicator;      // ▼ — 문장이 다 나왔을 때만

        [Header("선택지")]
        [SerializeField] private ChoiceButton[] choiceButtons;  // 최대 개수만큼 미리 만들어 둔다

        [Header("입력")]
        [Tooltip("다음 대사로 넘기는 입력 (E, Space 등). 마우스 클릭은 대화창을 누르면 된다")]
        [SerializeField] private InputActionReference[] advanceActions;

        [Header("값")]
        [SerializeField, Tooltip("초당 글자 수. 0 = 타자기 없이 즉시")] private float typeSpeedCps = 30f;
        [SerializeField, Range(0f, 0.6f), Tooltip("선택지가 열린 직후 확정을 무시하는 시간")] private float inputLockSeconds = 0.3f;
        [SerializeField, Tooltip("문장 부호 뒤에 잠깐 쉬는 시간 (말하는 리듬)")] private float punctuationPause = 0.16f;
        [SerializeField] private float openTime = 0.22f;
        [SerializeField] private float closeTime = 0.16f;

        public State Current => state;
        public bool IsOpen => state != State.Hidden;

        /// <summary>대화창이 열릴 때 / 닫힐 때 (UI 열림 관리 → 커서·이동 잠금이 듣는다)</summary>
        public event Action Opened;
        public event Action Closed;

        private State state = State.Hidden;
        private int focus;
        private int visible, total;
        private float typeAcc, delay, lockLeft, confirmLeft;
        private int choiceCount;
        private int chosen;
        private int ignoreInputFrame = -1;     // 이 프레임의 입력은 무시 (E로 대화를 연 그 프레임)
        private GameObject lastSelected;
        private Coroutine slide;

        private Action onLineDone;             // 이번 줄을 넘겼을 때 부를 함수
        private Action<int> onChoiceDone;      // 선택지를 골랐을 때 부를 함수 (고른 번호)

        private void Awake()
        {
            for (int i = 0; i < choiceButtons.Length; i++)
            {
                int index = i;   // 람다가 반복 변수 i가 아니라 이번 값을 기억하도록 복사
                choiceButtons[i].Button.onClick.AddListener(() => OnChoice(index));
                choiceButtons[i].Focused += f => focus = f;
            }
            SetChoicesVisible(false);
            root.alpha = 0f;
            root.blocksRaycasts = false;
            nextIndicator.SetActive(false);
        }

        private void OnEnable()
        {
            foreach (var a in advanceActions)
                if (a != null) a.action.Enable();
        }

        /* ───────── 바깥에서 부르는 것 (Yarn 연결부가 쓴다) ───────── */

        /// <summary>대화창을 연다 (이미 열려 있으면 아무것도 안 함)</summary>
        public void Open()
        {
            if (IsOpen) return;
            state = State.LineComplete;        // 첫 줄이 오기 전까지 잠깐 비어 있는 상태
            body.text = "";
            speakerName.text = "";
            ignoreInputFrame = Time.frameCount;
            root.blocksRaycasts = true;
            PlaySlide(true);
            Opened?.Invoke();
        }

        /// <summary>대화창을 닫는다</summary>
        public void Close()
        {
            if (!IsOpen) return;
            state = State.Hidden;
            SetChoicesVisible(false);
            nextIndicator.SetActive(false);
            root.blocksRaycasts = false;
            onLineDone = null;
            onChoiceDone = null;
            PlaySlide(false);
            Closed?.Invoke();
        }

        /// <summary>한 줄을 보여 준다. 플레이어가 다 읽고 넘기면 onDone이 불린다</summary>
        public void ShowLine(string speaker, string text, Action onDone)
        {
            Open();
            onLineDone = onDone;
            speakerName.text = speaker ?? "";
            speakerName.gameObject.SetActive(!string.IsNullOrEmpty(speaker));   // 이름 없는 줄(해설)은 이름표를 숨김

            body.text = text;
            body.maxVisibleCharacters = 0;
            body.ForceMeshUpdate();                   // textInfo를 채운다
            total = body.textInfo.characterCount;     // string.Length는 리치 텍스트 태그까지 센다 — 쓰지 않는다
            visible = 0;
            typeAcc = 0f;
            delay = 0f;
            state = State.Typing;
            nextIndicator.SetActive(false);
            if (typeSpeedCps <= 0f) Complete();
        }

        /// <summary>선택지를 보여 준다. 고르면 onChosen(고른 번호)이 불린다</summary>
        public void ShowChoices(IReadOnlyList<string> options, Action<int> onChosen)
        {
            Open();
            if (options.Count > choiceButtons.Length)
                Debug.LogWarning($"[DialogueView] 선택지 {options.Count}개 중 {choiceButtons.Length}개만 표시 (버튼을 더 만들어야 함)", this);

            onChoiceDone = onChosen;
            choiceCount = Mathf.Min(options.Count, choiceButtons.Length);
            state = State.ChoiceOpen;
            nextIndicator.SetActive(false);
            lockLeft = inputLockSeconds;

            SetChoicesVisible(false);
            for (int i = 0; i < choiceCount; i++)
            {
                choiceButtons[i].gameObject.SetActive(true);
                choiceButtons[i].Setup(i, options[i]);
            }
            StartCoroutine(RevealChoices());
            focus = 0;
            Select(choiceButtons[0].gameObject);      // 열리면 첫 항목에 포커스
        }

        /// <summary>넘기기 입력 하나 — 한 단계만 처리한다 (입력 소비)</summary>
        public void Advance()
        {
            switch (state)
            {
                case State.Typing:
                    Complete();                       // 첫 입력 = 문장 완성
                    break;
                case State.LineComplete:
                    FinishLine();                     // 다음 입력 = 다음 줄
                    break;
                // ChoiceOpen은 버튼(Submit·클릭) → OnChoice 한 곳에서만 확정한다
            }
        }

        /// <summary>대화창을 클릭하면 넘기기 (선택지 버튼 클릭은 버튼이 먼저 받아서 여기로 오지 않음)</summary>
        public void OnPointerClick(PointerEventData e)
        {
            if (state != State.ChoiceOpen) Advance();
        }

        /// <summary>버튼 클릭 · Submit이 모두 여기로 — 확정의 단일 진입점</summary>
        public void OnChoice(int index)
        {
            if (state != State.ChoiceOpen) return;
            if (lockLeft > 0f) return;                // 입력 잠금 중: 무시
            chosen = index;
            state = State.ChoiceConfirmed;
            confirmLeft = 0.25f;
            // 고른 항목만 남기고 나머지는 먼저 사라진다
            for (int i = 0; i < choiceCount; i++)
                if (i != index) choiceButtons[i].Group.alpha = 0f;
        }

        /* ───────── 상태 전환 ───────── */

        private void Complete()
        {
            visible = total;
            body.maxVisibleCharacters = total;
            state = State.LineComplete;
            nextIndicator.SetActive(true);
        }

        private void FinishLine()
        {
            if (onLineDone == null) return;           // 아직 다음 줄을 기다리는 중
            var done = onLineDone;
            onLineDone = null;                        // 먼저 비운다 → 콜백 안에서 바로 다음 ShowLine이 와도 안전
            nextIndicator.SetActive(false);
            done();
        }

        /* ───────── 매 프레임 ───────── */

        private void Update()
        {
            if (state == State.Hidden) return;
            float dt = Time.unscaledDeltaTime;   // 일시정지(timeScale 0) 중에도 대화는 흐른다

            if (state != State.ChoiceOpen && Time.frameCount != ignoreInputFrame && AdvancePressed())
            {
                Advance();
                return;                          // 같은 프레임에 '완성'과 '다음 줄'이 함께 일어나지 않게
            }

            switch (state)
            {
                case State.Typing:
                    if (delay > 0f) { delay -= dt; break; }
                    typeAcc += dt;
                    float step = 1f / Mathf.Max(1f, typeSpeedCps);
                    while (visible < total && typeAcc >= step)
                    {
                        typeAcc -= step;
                        visible++;
                        char ch = body.textInfo.characterInfo[visible - 1].character;
                        if (visible < total && IsPause(ch)) typeAcc -= punctuationPause;   // 말하는 리듬
                    }
                    body.maxVisibleCharacters = visible;
                    if (visible >= total) Complete();
                    break;

                case State.ChoiceOpen:
                    lockLeft = Mathf.Max(0f, lockLeft - dt);
                    // 빈 곳을 클릭해 선택이 풀리면 마지막 선택지로 되돌린다 (키보드 입력이 먹히지 않는 문제 방지)
                    var es = EventSystem.current;
                    if (es != null && es.currentSelectedGameObject == null && lastSelected != null) es.SetSelectedGameObject(lastSelected);
                    break;

                case State.ChoiceConfirmed:
                    confirmLeft -= dt;
                    if (confirmLeft <= 0f)
                    {
                        SetChoicesVisible(false);
                        state = State.LineComplete;
                        var done = onChoiceDone;
                        onChoiceDone = null;
                        done?.Invoke(chosen);
                    }
                    break;
            }
        }

        private bool AdvancePressed()
        {
            foreach (var a in advanceActions)
                if (a != null && a.action.WasPressedThisFrame()) return true;
            return false;
        }

        private static bool IsPause(char c) => c == '.' || c == ',' || c == '!' || c == '?' || c == '…';

        private void Select(GameObject go)
        {
            lastSelected = go;
            var es = EventSystem.current;
            if (es == null) return;
            // 지난번에 고른 버튼이 아직 '선택됨'으로 남아 있으면 SetSelectedGameObject가 같은 오브젝트라며
            // 바로 반환해 OnSelect가 오지 않는다(포커스 연출이 사라짐) → 먼저 비우고 다시 선택한다
            es.SetSelectedGameObject(null);
            es.SetSelectedGameObject(go);
        }

        private void SetChoicesVisible(bool on)
        {
            foreach (var b in choiceButtons)
            {
                b.gameObject.SetActive(on);
                b.Group.alpha = on ? 0f : 1f;
            }
            // 꺼진 버튼이 '선택됨'으로 남지 않게 (다음 Select가 OnSelect를 다시 받도록)
            if (!on)
            {
                lastSelected = null;
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            }
        }

        /* ───────── 연출 (코루틴, unscaled) ───────── */

        private IEnumerator RevealChoices()
        {
            // 항목마다 50ms 시차, 각 180ms ease-out 페이드
            float t = 0f;
            while (t < 0.18f + 0.05f * choiceCount)
            {
                t += Time.unscaledDeltaTime;
                for (int i = 0; i < choiceCount; i++)
                {
                    float k = Mathf.Clamp01((t - 0.05f * i) / 0.18f);
                    if (state == State.ChoiceOpen) choiceButtons[i].Group.alpha = 1f - (1f - k) * (1f - k);
                }
                yield return null;
            }
        }

        private void PlaySlide(bool show)
        {
            if (slide != null) StopCoroutine(slide);   // 열리는 중에 닫혀도 두 연출이 싸우지 않게
            slide = StartCoroutine(Slide(show));
        }

        private IEnumerator Slide(bool show)
        {
            float dur = show ? openTime : closeTime;
            float from = show ? -60f : 0f, to = show ? 0f : -70f;
            for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
            {
                float k = t / dur;
                float e = show ? 1f - Mathf.Pow(1f - k, 3f) : k * k;   // 열 때 ease-out, 닫을 때 ease-in
                panel.anchoredPosition = new Vector2(panel.anchoredPosition.x, Mathf.Lerp(from, to, e));
                root.alpha = show ? e : 1f - e;
                yield return null;
            }
            panel.anchoredPosition = new Vector2(panel.anchoredPosition.x, to);
            root.alpha = show ? 1f : 0f;
            slide = null;
        }
    }
}
